namespace PdfService.Shared;

using StackExchange.Redis;

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
