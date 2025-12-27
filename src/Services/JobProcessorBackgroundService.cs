namespace PdfService.Services;

using System.Diagnostics;
using PdfService.Configuration;
using PdfService.Documents;
using PdfService.Models;
using QuestPDF.Fluent;

public class JobProcessorBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobProcessorBackgroundService> _logger;
    private readonly RedisConfiguration _config;
    private readonly SemaphoreSlim _semaphore;
    private DateTime _lastCleanup = DateTime.UtcNow;

    public JobProcessorBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<JobProcessorBackgroundService> logger,
        RedisConfiguration config)
    {
        _serviceProvider = serviceProvider;
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
        using var scope = _serviceProvider.CreateScope();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();

        var pendingJobIds = await jobRepository.GetPendingJobIdsAsync(_config.ProcessorConcurrency);

        if (pendingJobIds.Count == 0)
        {
            _logger.LogDebug("No pending jobs to process");
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
            using var scope = _serviceProvider.CreateScope();
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
                // Generate proposal content with Claude
                var proposalData = await claudeService.GenerateProposal(job.RequestData);

                // Generate PDF
                var logoBytes = File.Exists("logo.png") ? File.ReadAllBytes("logo.png") : [];
                var document = new ProposalDocument(proposalData, logoBytes);
                var pdfBytes = document.GeneratePdf(); // QuestPDF extension method

                // Save result
                await jobRepository.SaveJobResultAsync(jobId, pdfBytes);
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
                await jobRepository.MoveJobToCompletedAsync(jobId); // Failed jobs also go to completed queue
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task PeriodicCleanupAsync()
    {
        // Run cleanup every 15 minutes
        if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(15))
        {
            return;
        }

        _logger.LogInformation("Running periodic cleanup of expired jobs");

        using var scope = _serviceProvider.CreateScope();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IJobRepository>();

        await jobRepository.DeleteExpiredJobsAsync();
        _lastCleanup = DateTime.UtcNow;
    }
}
