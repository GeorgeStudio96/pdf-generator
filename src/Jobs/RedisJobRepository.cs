namespace PdfService.Jobs;

using System.Text.Json;
using PdfService.Configuration;
using PdfService.Models;
using StackExchange.Redis;

public class RedisJobRepository : IJobRepository
{
    private readonly IDatabase _db;
    private readonly RedisConfiguration _config;
    private readonly ILogger<RedisJobRepository> _logger;

    private const string JobKeyPrefix = "job:";
    private const string PendingQueueKey = "jobs:pending";
    private const string ProcessingQueueKey = "jobs:processing";
    private const string CompletedQueueKey = "jobs:completed";

    public RedisJobRepository(
        IConnectionMultiplexer redis,
        RedisConfiguration config,
        ILogger<RedisJobRepository> logger)
    {
        _db = redis.GetDatabase();
        _config = config;
        _logger = logger;
    }

    public async Task<Job> SaveJobAsync(Job job)
    {
        var jobKey = $"{JobKeyPrefix}{job.Id}";
        var hashEntries = new[]
        {
            new HashEntry("id", job.Id),
            new HashEntry("status", job.Status.ToString()),
            new HashEntry("requestData", JsonSerializer.Serialize(job.RequestData)),
            new HashEntry("createdAt", job.CreatedAt.ToString("O"))
        };

        await _db.HashSetAsync(jobKey, hashEntries);
        _logger.LogInformation("Job {JobId} saved to Redis", job.Id);

        return job;
    }

    public async Task<Job?> GetJobAsync(string jobId)
    {
        var jobKey = $"{JobKeyPrefix}{jobId}";
        var exists = await _db.KeyExistsAsync(jobKey);

        if (!exists)
        {
            _logger.LogWarning("Job {JobId} not found in Redis", jobId);
            return null;
        }

        var hash = await _db.HashGetAllAsync(jobKey);
        var hashDict = hash.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString()
        );

        var job = new Job
        {
            Id = hashDict["id"],
            Status = Enum.Parse<JobStatus>(hashDict["status"]),
            RequestData = JsonSerializer.Deserialize<ProposalRequest>(hashDict["requestData"]) ?? new(),
            CreatedAt = DateTime.Parse(hashDict["createdAt"]),
            CompletedAt = hashDict.ContainsKey("completedAt") && !string.IsNullOrEmpty(hashDict["completedAt"])
                ? DateTime.Parse(hashDict["completedAt"])
                : null,
            ErrorMessage = hashDict.ContainsKey("errorMessage") ? hashDict["errorMessage"] : null,
            PdfBytes = hashDict.ContainsKey("pdfBytes") && !string.IsNullOrEmpty(hashDict["pdfBytes"])
                ? Convert.FromBase64String(hashDict["pdfBytes"])
                : null
        };

        return job;
    }

    public async Task UpdateJobStatusAsync(string jobId, JobStatus status, string? errorMessage = null)
    {
        var jobKey = $"{JobKeyPrefix}{jobId}";

        await _db.HashSetAsync(jobKey, "status", status.ToString());

        if (status == JobStatus.Completed || status == JobStatus.Failed)
        {
            await _db.HashSetAsync(jobKey, "completedAt", DateTime.UtcNow.ToString("O"));

            // Set TTL based on status
            var ttlHours = status == JobStatus.Completed
                ? _config.JobTtlHours
                : _config.FailedJobTtlHours;
            await _db.KeyExpireAsync(jobKey, TimeSpan.FromHours(ttlHours));
        }

        if (!string.IsNullOrEmpty(errorMessage))
        {
            await _db.HashSetAsync(jobKey, "errorMessage", errorMessage);
        }

        _logger.LogInformation("Job {JobId} status updated to {Status}", jobId, status);
    }

    public async Task SaveJobResultAsync(string jobId, byte[] pdfBytes)
    {
        var jobKey = $"{JobKeyPrefix}{jobId}";
        var base64Pdf = Convert.ToBase64String(pdfBytes);

        await _db.HashSetAsync(jobKey, "pdfBytes", base64Pdf);

        _logger.LogInformation("Job {JobId} PDF result saved ({Size} bytes)", jobId, pdfBytes.Length);
    }

    public async Task<List<string>> GetPendingJobIdsAsync(int count)
    {
        var jobIds = await _db.SortedSetRangeByScoreAsync(
            PendingQueueKey,
            take: count,
            order: Order.Ascending
        );

        return jobIds.Select(x => x.ToString()).ToList();
    }

    public async Task MoveJobToPendingAsync(string jobId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await _db.SortedSetRemoveAsync(ProcessingQueueKey, jobId);
        await _db.SortedSetAddAsync(PendingQueueKey, jobId, timestamp);

        _logger.LogDebug("Job {JobId} moved to pending queue", jobId);
    }

    public async Task MoveJobToProcessingAsync(string jobId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await _db.SortedSetRemoveAsync(PendingQueueKey, jobId);
        await _db.SortedSetAddAsync(ProcessingQueueKey, jobId, timestamp);

        _logger.LogDebug("Job {JobId} moved to processing queue", jobId);
    }

    public async Task MoveJobToCompletedAsync(string jobId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await _db.SortedSetRemoveAsync(ProcessingQueueKey, jobId);
        await _db.SortedSetAddAsync(CompletedQueueKey, jobId, timestamp);

        _logger.LogDebug("Job {JobId} moved to completed queue", jobId);
    }

    public async Task DeleteExpiredJobsAsync()
    {
        // This method could be enhanced to clean up completed queue entries
        // For now, TTL on job keys handles expiration automatically

        var cutoffTime = DateTimeOffset.UtcNow.AddHours(-_config.JobTtlHours).ToUnixTimeSeconds();

        var expiredJobs = await _db.SortedSetRangeByScoreAsync(
            CompletedQueueKey,
            stop: cutoffTime
        );

        if (expiredJobs.Length > 0)
        {
            await _db.SortedSetRemoveAsync(CompletedQueueKey, expiredJobs);
            _logger.LogInformation("Cleaned up {Count} expired jobs from completed queue", expiredJobs.Length);
        }
    }
}
