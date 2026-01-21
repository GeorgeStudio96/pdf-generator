namespace PdfService.Jobs;

using PdfService.Models;

public class JobService : IJobService
{
    private readonly IJobRepository _jobRepository;
    private readonly ILogger<JobService> _logger;

    public JobService(IJobRepository jobRepository, ILogger<JobService> logger)
    {
        _jobRepository = jobRepository;
        _logger = logger;
    }

    public async Task<CreateJobResponse> CreateJobAsync(ProposalRequest request)
    {
        var job = new Job
        {
            Id = Guid.NewGuid().ToString(),
            Status = JobStatus.Pending,
            RequestData = request,
            CreatedAt = DateTime.UtcNow
        };

        await _jobRepository.SaveJobAsync(job);
        await _jobRepository.MoveJobToPendingAsync(job.Id);

        _logger.LogInformation("Job {JobId} created for project '{ProjectName}'", job.Id, request.ProjectName);

        return new CreateJobResponse
        {
            JobId = job.Id,
            Status = job.Status,
            CreatedAt = job.CreatedAt
        };
    }

    public async Task<JobStatusResponse?> GetJobStatusAsync(string jobId)
    {
        var job = await _jobRepository.GetJobAsync(jobId);

        if (job == null)
        {
            _logger.LogWarning("Job {JobId} not found when requesting status", jobId);
            return null;
        }

        return new JobStatusResponse
        {
            JobId = job.Id,
            Status = job.Status,
            CreatedAt = job.CreatedAt,
            CompletedAt = job.CompletedAt,
            ErrorMessage = job.ErrorMessage
        };
    }

    public async Task<byte[]?> GetJobResultAsync(string jobId)
    {
        var job = await _jobRepository.GetJobAsync(jobId);

        if (job == null)
        {
            _logger.LogWarning("Job {JobId} not found when requesting result", jobId);
            return null;
        }

        if (job.Status != JobStatus.Completed)
        {
            _logger.LogWarning("Job {JobId} is not completed (status: {Status})", jobId, job.Status);
            return null;
        }

        return job.PdfBytes;
    }
}
