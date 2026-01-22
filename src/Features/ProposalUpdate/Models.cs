namespace PdfService.Features.ProposalUpdate;

using PdfService.Features.ProposalGeneration;

public record UpdateRequest
{
    public string? ChangeDescription { get; init; }  // User's description of what to change
    public ProposalRequest? UpdatedBaseRequest { get; init; }  // Optional: full request data override
    public string? NewDocumentPath { get; init; }  // Optional: path to new uploaded document
}

public record UpdateClassification
{
    public UpdateType Type { get; init; }
    public string Reasoning { get; init; } = "";
}

public enum UpdateType
{
    Simple,      // Minor text edits - fast, no RAG
    Complex,     // Reformulation/restructure - uses Haiku
    FullRegen    // New document uploaded or major changes - full regeneration with RAG
}

public record UpdateResponse
{
    public string JobId { get; init; } = "";
    public string Status { get; init; } = "";
    public string Message { get; init; } = "";
}
