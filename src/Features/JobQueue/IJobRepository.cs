namespace PdfService.Features.JobQueue;

using PdfService.Features.ProposalGeneration;

public interface IJobRepository
{
    Task<Job> SaveJobAsync(Job job);
    Task<Job?> GetJobAsync(string jobId);
    Task UpdateJobStatusAsync(string jobId, JobStatus status, string? errorMessage = null);
    Task SaveJobResultAsync(string jobId, byte[] pdfBytes, ProposalData? proposalData = null);
    Task<List<string>> GetPendingJobIdsAsync(int count);
    Task MoveJobToPendingAsync(string jobId);
    Task MoveJobToProcessingAsync(string jobId);
    Task MoveJobToCompletedAsync(string jobId);
    Task DeleteExpiredJobsAsync();
    Task PublishJobNotificationAsync(string jobId);
}
