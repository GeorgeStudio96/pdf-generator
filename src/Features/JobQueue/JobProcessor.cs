namespace PdfService.Features.JobQueue;

using System.Diagnostics;
using PdfService.Shared;
using PdfService.Features.ProposalGeneration;
using QuestPDF.Fluent;

public class JobProcessorBackgroundService : BackgroundService
{
    // ИЗМЕНЕНИЕ 1: Используем IServiceScopeFactory вместо IServiceProvider
    private readonly IServiceScopeFactory _scopeFactory; 
    private readonly ILogger<JobProcessorBackgroundService> _logger;
    private readonly RedisConfiguration _config;
    private readonly SemaphoreSlim _semaphore;
    private DateTime _lastCleanup = DateTime.UtcNow;

    // ИЗМЕНЕНИЕ 2: Обновляем конструктор
    public JobProcessorBackgroundService(
        IServiceScopeFactory scopeFactory, // <--- Было IServiceProvider
        ILogger<JobProcessorBackgroundService> logger,
        RedisConfiguration config)
    {
        _scopeFactory = scopeFactory; // <--- Сохраняем фабрику
        _logger = logger;
        _config = config;
        _semaphore = new SemaphoreSlim(_config.ProcessorConcurrency);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Job Processor started with concurrency: {Concurrency}, poll interval: {Interval}s",
            _config.ProcessorConcurrency,
            _config.ProcessorPollIntervalSeconds
        );

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

            await Task.Delay(
                TimeSpan.FromSeconds(_config.ProcessorPollIntervalSeconds),
                stoppingToken
            );
        }

        _logger.LogInformation("Job Processor stopped");
    }

    private async Task ProcessPendingJobsAsync(CancellationToken stoppingToken)
    {
        // ИЗМЕНЕНИЕ 3: Создаем scope через фабрику
        using var scope = _scopeFactory.CreateScope(); 
        var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();

        var pendingJobIds = await jobRepository.GetPendingJobIdsAsync(_config.ProcessorConcurrency);

        if (pendingJobIds.Count == 0)
        {
            // _logger.LogDebug("No pending jobs to process"); // Можно раскомментировать для дебага
            return;
        }

        _logger.LogInformation("Processing {Count} pending jobs", pendingJobIds.Count);

        var tasks = pendingJobIds.Select(jobId => ProcessJobAsync(jobId, stoppingToken));
        await Task.WhenAll(tasks);
    }

    private async Task ProcessJobAsync(string jobId, CancellationToken stoppingToken)
    {
        await _semaphore.WaitAsync(stoppingToken);

        try
        {
            // ИЗМЕНЕНИЕ 4: Тут тоже создаем scope через фабрику
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
        {
            return;
        }

        _logger.LogInformation("Running periodic cleanup of expired jobs");

        // ИЗМЕНЕНИЕ 5: И тут тоже через фабрику
        using var scope = _scopeFactory.CreateScope();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();

        await jobRepository.DeleteExpiredJobsAsync();
        _lastCleanup = DateTime.UtcNow;
    }
}