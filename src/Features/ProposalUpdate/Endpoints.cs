namespace PdfService.Features.ProposalUpdate;

using Microsoft.AspNetCore.Mvc;
using PdfService.Features.JobQueue;
using PdfService.Features.ProposalGeneration;
using QuestPDF.Fluent;

public static class ProposalUpdateEndpoints
{
    public static void MapProposalUpdateEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/jobs/proposal");

        // Update existing proposal
        group.MapPost("/{jobId}/update", async (
            string jobId,
            [FromBody] UpdateRequest request,
            [FromServices] IJobRepository jobRepository,
            [FromServices] UpdateService updateService,
            [FromServices] ILogger<UpdateService> logger) =>
        {
            // Get existing job
            var existingJob = await jobRepository.GetJobAsync(jobId);
            if (existingJob == null)
            {
                return Results.NotFound(new { error = "Job not found" });
            }

            if (existingJob.ProposalData == null)
            {
                return Results.BadRequest(new { error = "No ProposalData found - cannot update" });
            }

            try
            {
                logger.LogInformation("Updating proposal {JobId}", jobId);

                // Create new job for the update
                var updateJobId = Guid.NewGuid().ToString();
                var updateJob = new Job
                {
                    Id = updateJobId,
                    Status = JobStatus.Pending,
                    RequestData = existingJob.RequestData,
                    CreatedAt = DateTime.UtcNow
                };

                await jobRepository.SaveJobAsync(updateJob);
                await jobRepository.MoveJobToPendingAsync(updateJobId);

                // Process update immediately in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await jobRepository.MoveJobToProcessingAsync(updateJobId);
                        await jobRepository.UpdateJobStatusAsync(updateJobId, JobStatus.Processing);

                        // Apply update using UpdateService
                        var updatedProposalData = await updateService.UpdateProposalAsync(
                            existingJob,
                            request
                        );

                        // Generate new PDF
                        var logoBytes = File.Exists("logo.png")
                            ? File.ReadAllBytes("logo.png")
                            : Array.Empty<byte>();

                        var document = new ProposalDocument(updatedProposalData, logoBytes);
                        var pdfBytes = document.GeneratePdf();

                        // Save updated result
                        await jobRepository.SaveJobResultAsync(updateJobId, pdfBytes, updatedProposalData);
                        await jobRepository.UpdateJobStatusAsync(updateJobId, JobStatus.Completed);
                        await jobRepository.MoveJobToCompletedAsync(updateJobId);

                        logger.LogInformation("Update job {UpdateJobId} completed", updateJobId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Update job {UpdateJobId} failed", updateJobId);
                        await jobRepository.UpdateJobStatusAsync(
                            updateJobId,
                            JobStatus.Failed,
                            ex.Message
                        );
                        await jobRepository.MoveJobToCompletedAsync(updateJobId);
                    }
                });

                return Results.Ok(new UpdateResponse
                {
                    JobId = updateJobId,
                    Status = "pending",
                    Message = "Update job created - poll /jobs/{id}/status for progress"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating update job");
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });

        // Get ProposalData from completed job (for editing form)
        group.MapGet("/{jobId}/data", async (
            string jobId,
            [FromServices] IJobRepository jobRepository) =>
        {
            var job = await jobRepository.GetJobAsync(jobId);
            if (job == null)
            {
                return Results.NotFound(new { error = "Job not found" });
            }

            if (job.ProposalData == null)
            {
                return Results.BadRequest(new { error = "No ProposalData available" });
            }

            return Results.Ok(new
            {
                jobId = job.Id,
                proposalData = job.ProposalData,
                createdAt = job.CreatedAt,
                completedAt = job.CompletedAt
            });
        });
    }
}
