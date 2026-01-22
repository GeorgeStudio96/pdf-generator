namespace PdfService.Features.DocumentProcessing;

using PdfService.Shared;
using StackExchange.Redis;

public static class DocumentEndpoints
{
    public static void MapDocumentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/documents");

        group.MapPost("/upload", UploadDocument)
            .WithName("UploadDocument")
            .WithSummary("Upload a document for RAG context")
            .WithDescription("Accepts PDF, DOCX, MD, or JSON files. Max 10MB.")
            .DisableAntiforgery();

        group.MapDelete("/{projectId}", DeleteDocuments)
            .WithName("DeleteDocuments")
            .WithSummary("Delete all documents for a project");
    }

    private static async Task<IResult> UploadDocument(
        IFormFile file,
        string projectId,
        DocumentService documentService,
        EmbeddingService embeddingService,
        DocumentRepository documentRepository,
        ILogger<Program> logger)
    {
        try
        {
            logger.LogInformation(
                "Uploading document {FileName} for project {ProjectId}",
                file.FileName,
                projectId);

            // Step 1: Extract text and create chunks
            var chunks = await documentService.ProcessAsync(file, projectId);

            // Step 2: Generate embeddings for all chunks
            var texts = chunks.Select(c => c.Content).ToList();
            var embeddings = await embeddingService.GenerateBatchAsync(texts);

            // Step 3: Save chunks with embeddings to Redis
            await documentRepository.SaveChunksAsync(projectId, chunks, embeddings);

            var response = new UploadDocumentResponse(
                chunks.Count,
                projectId,
                file.Length
            );

            logger.LogInformation(
                "Successfully processed {ChunkCount} chunks from {FileName}",
                chunks.Count,
                file.FileName);

            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Upload validation error: {Message}", ex.Message);
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (NotSupportedException ex)
        {
            logger.LogWarning("Unsupported file format: {Message}", ex.Message);
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status415UnsupportedMediaType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload document");
            return Results.Problem(
                detail: "An error occurred while processing the document",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> DeleteDocuments(
        string projectId,
        DocumentRepository documentRepository,
        ILogger<Program> logger)
    {
        try
        {
            logger.LogInformation(
                "Deleting documents for project {ProjectId}",
                projectId);

            var deletedCount = await documentRepository.DeleteProjectDocumentsAsync(projectId);

            return Results.Ok(new { deletedChunks = deletedCount });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete documents for project {ProjectId}", projectId);
            return Results.Problem(
                detail: "An error occurred while deleting documents",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
