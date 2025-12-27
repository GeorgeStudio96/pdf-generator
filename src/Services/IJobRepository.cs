namespace PdfService.Services;

using PdfService.Models;

public interface IJobRepository
{
    Task<Job> SaveJobAsync(Job job);
    Task<Job?> GetJobAsync(string jobId);
    Task UpdateJobStatusAsync(string jobId, JobStatus status, string? errorMessage = null);
    Task SaveJobResultAsync(string jobId, byte[] pdfBytes);
    Task<List<string>> GetPendingJobIdsAsync(int count);
    Task MoveJobToPendingAsync(string jobId);
    Task MoveJobToProcessingAsync(string jobId);
    Task MoveJobToCompletedAsync(string jobId);
    Task DeleteExpiredJobsAsync();
}
