namespace PdfService.Features.JobQueue;

using PdfService.Features.ProposalGeneration;

public record Job
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public JobStatus Status { get; init; } = JobStatus.Pending;
    public ProposalRequest RequestData { get; init; } = new();
    public ProposalData? ProposalData { get; init; } = null;  // Save AI-generated data for updates
    public byte[]? PdfBytes { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum JobStatus
{
    Pending,
    Processing,
    DataReady,
    Completed,
    Failed
}

public record CreateJobResponse
{
    public string JobId { get; init; } = "";
    public JobStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record JobStatusResponse
{
    public string JobId { get; init; } = "";
    public JobStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string? ErrorMessage { get; init; }
}
