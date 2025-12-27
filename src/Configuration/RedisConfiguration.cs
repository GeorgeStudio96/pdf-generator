namespace PdfService.Configuration;

public class RedisConfiguration
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public int JobTtlHours { get; set; } = 1;
    public int FailedJobTtlHours { get; set; } = 24;
    public int ProcessorConcurrency { get; set; } = 5;
    public int ProcessorPollIntervalSeconds { get; set; } = 2;
}
