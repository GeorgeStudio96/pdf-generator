namespace PdfService.Shared;

using StackExchange.Redis;

public class RedisConfiguration
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public int JobTtlHours { get; set; } = 1;
    public int FailedJobTtlHours { get; set; } = 24;
    public int ProcessorConcurrency { get; set; } = 5;
    public int ProcessorPollIntervalSeconds { get; set; } = 2;
}

public static class RedisVectorSetup
{
    public static async Task InitializeVectorIndexAsync(
        IConnectionMultiplexer redis,
        ILogger logger)
    {
        logger.LogInformation("Redis vector index setup skipped (simplified MVP mode)");
        await Task.CompletedTask;
    }
}
