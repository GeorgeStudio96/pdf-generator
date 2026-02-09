namespace PdfService.Features.JobQueue;

using System.Diagnostics;
using PdfService.Shared;
using PdfService.Features.ProposalGeneration;
using QuestPDF.Fluent;
using StackExchange.Redis;

public class JobProcessorBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<JobProcessorBackgroundService> _logger;
    private readonly RedisConfiguration _config;
    private readonly SemaphoreSlim _semaphore;
    private readonly SemaphoreSlim _jobSignal = new(0);
    private DateTime _lastCleanup = DateTime.UtcNow;

    public JobProcessorBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer redis,
        ILogger<JobProcessorBackgroundService> logger,
        RedisConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _redis = redis;
        _logger = logger;
        _config = config;
        _semaphore = new SemaphoreSlim(_config.ProcessorConcurrency);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Job Processor started with concurrency: {Concurrency}, fallback poll interval: {Interval}s",
            _config.ProcessorConcurrency,
            _config.ProcessorPollIntervalSeconds
        );

        // Subscribe to Pub/Sub notifications for instant wake-up
        var subscriber = _redis.GetSubscriber();
        await subscriber.SubscribeAsync(RedisChannel.Literal(RedisJobRepository.JobNotifyChannel), (_, message) =>
        {
            _logger.LogDebug("Received job notification for job {JobId}", message);
            _jobSignal.Release();
        });

        _logger.LogInformation("Subscribed to Redis channel '{Channel}' for job notifications",
            RedisJobRepository.JobNotifyChannel);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
                await PeriodicCleanupAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in job processor main loop");
            }

            // Wait for either: Pub/Sub notification (instant) OR fallback timeout
            try
            {
                await _jobSignal.WaitAsync(
                    TimeSpan.FromSeconds(_config.ProcessorPollIntervalSeconds),
                    stoppingToken
                );
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            // Drain any extra signals that accumulated while we were processing
            while (_jobSignal.CurrentCount > 0)
                _jobSignal.Wait(0);
        }

        await subscriber.UnsubscribeAsync(RedisChannel.Literal(RedisJobRepository.JobNotifyChannel));
        _logger.LogInformation("Job Processor stopped");
    }

    private async Task ProcessPendingJobsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();

        var pendingJobIds = await jobRepository.GetPendingJobIdsAsync(_config.ProcessorConcurrency);

        if (pendingJobIds.Count == 0)
            return;

        _logger.LogInformation("Processing {Count} pending jobs", pendingJobIds.Count);

        var tasks = pendingJobIds.Select(jobId => ProcessJobAsync(jobId, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task ProcessJobAsync(string jobId, CancellationToken stoppingToken)
    {
        await _semaphore.WaitAsync(stoppingToken);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();
            var claudeService = scope.ServiceProvider.GetRequiredService<ClaudeService>();

            var job = await jobRepository.GetJobAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning("Job {JobId} not found, skipping", jobId);
                return;
            }

            _logger.LogInformation("Processing job {JobId} for project '{ProjectName}'", jobId, job.RequestData.ProjectName);
            var stopwatch = Stopwatch.StartNew();

            // Move to processing queue
            await jobRepository.MoveJobToProcessingAsync(jobId);
            await jobRepository.UpdateJobStatusAsync(jobId, JobStatus.Processing);

            try
            {
                var proposalData = await claudeService.GenerateProposal(job.RequestData, job.RequestData.ProjectId);

                var logoBytes = File.Exists("logo.png") ? File.ReadAllBytes("logo.png") : Array.Empty<byte>();
                var document = new ProposalDocument(proposalData, logoBytes);
                var pdfBytes = document.GeneratePdf();

                // Save both PDF and ProposalData for future updates
                await jobRepository.SaveJobResultAsync(jobId, pdfBytes, proposalData);
                await jobRepository.UpdateJobStatusAsync(jobId, JobStatus.Completed);
                await jobRepository.MoveJobToCompletedAsync(jobId);

                stopwatch.Stop();
                _logger.LogInformation(
                    "Job {JobId} completed successfully in {Duration}ms",
                    jobId,
                    stopwatch.ElapsedMilliseconds
                );
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Job {JobId} failed after {Duration}ms", jobId, stopwatch.ElapsedMilliseconds);

                await jobRepository.UpdateJobStatusAsync(
                    jobId,
                    JobStatus.Failed,
                    ex.Message
                );
                await jobRepository.MoveJobToCompletedAsync(jobId);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task PeriodicCleanupAsync()
    {
        if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(15))
            return;

        _logger.LogInformation("Running periodic cleanup of expired jobs");

        using var scope = _scopeFactory.CreateScope();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();

        await jobRepository.DeleteExpiredJobsAsync();
        _lastCleanup = DateTime.UtcNow;
    }
}
