namespace PdfService.Jobs;

using PdfService.Models;

public interface IJobService
{
    Task<CreateJobResponse> CreateJobAsync(ProposalRequest request);
    Task<JobStatusResponse?> GetJobStatusAsync(string jobId);
    Task<byte[]?> GetJobResultAsync(string jobId);
}
