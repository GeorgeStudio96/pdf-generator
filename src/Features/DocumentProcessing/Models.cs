namespace PdfService.Features.DocumentProcessing;

public record UploadDocumentRequest(
    string ProjectId,
    IFormFile File
);

public record UploadDocumentResponse(
    int ChunksProcessed,
    string ProjectId,
    long FileSizeBytes
);

public record DocumentChunk(
    string Content,
    int Index,
    string DocumentType
);

public record SearchResult(
    string Content,
    float Relevance
);
