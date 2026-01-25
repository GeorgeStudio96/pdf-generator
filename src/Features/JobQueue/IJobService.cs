namespace PdfService.Features.JobQueue;

using PdfService.Features.ProposalGeneration;

public interface IJobService
{
    Task<CreateJobResponse> CreateJobAsync(ProposalRequest request);
    Task<JobStatusResponse?> GetJobStatusAsync(string jobId);
    Task<byte[]?> GetJobResultAsync(string jobId);
}
