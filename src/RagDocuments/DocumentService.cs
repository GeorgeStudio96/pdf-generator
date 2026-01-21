namespace PdfService.RagDocuments;

using System.Text;
using UglyToad.PdfPig;
using NPOI.XWPF.UserModel;

public class DocumentService(ILogger<DocumentService> logger)
{
    private const int ChunkSize = 800;
    private const int OverlapSize = 100;
    private const long MaxFileSize = 10_000_000;

    public async Task<List<DocumentChunk>> ProcessAsync(IFormFile file, string projectId)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("File is empty");

        if (file.Length > MaxFileSize)
            throw new ArgumentException($"File too large. Maximum size is {MaxFileSize / 1_000_000}MB");

        var mimeType = file.ContentType?.ToLower() ?? "application/octet-stream";

        logger.LogInformation(
            "Processing file {FileName} ({Size} bytes, type: {MimeType}) for project {ProjectId}",
            file.FileName,
            file.Length,
            mimeType,
            projectId);

        var text = mimeType switch
        {
            "application/pdf" => ExtractPdf(file),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                => await ExtractDocxAsync(file),
            "text/plain" or "text/markdown"
                => await ExtractTextAsync(file),
            "application/json"
                => await ExtractJsonAsync(file),
            _ => throw new NotSupportedException($"File format not supported: {mimeType}")
        };

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("No text content found in document");

        logger.LogInformation(
            "Extracted {CharCount} characters from {FileName}",
            text.Length,
            file.FileName);

        return ChunkText(text);
    }

    private string ExtractPdf(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var document = PdfDocument.Open(stream);

        var sb = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return sb.ToString();
    }

    private async Task<string> ExtractDocxAsync(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        var document = new XWPFDocument(stream);

        var sb = new StringBuilder();
        foreach (var paragraph in document.Paragraphs)
        {
            if (!string.IsNullOrWhiteSpace(paragraph.Text))
                sb.AppendLine(paragraph.Text);
        }

        return sb.ToString();
    }

    private async Task<string> ExtractTextAsync(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private async Task<string> ExtractJsonAsync(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private List<DocumentChunk> ChunkText(string text)
    {
        var chunks = new List<DocumentChunk>();
        var sentences = text.Split(
            new[] { ". ", ".\n", "? ", "! " },
            StringSplitOptions.RemoveEmptyEntries);

        var current = new StringBuilder();
        int index = 0;

        foreach (var sentence in sentences)
        {
            if (current.Length + sentence.Length > ChunkSize && current.Length > 0)
            {
                chunks.Add(new DocumentChunk(
                    current.ToString().Trim(),
                    index++,
                    "text"
                ));

                var overlap = current.ToString();
                current.Clear();
                if (overlap.Length > OverlapSize)
                {
                    current.Append(overlap.Substring(overlap.Length - OverlapSize));
                }
            }

            current.Append(sentence).Append(". ");
        }

        if (current.Length > 0)
        {
            chunks.Add(new DocumentChunk(
                current.ToString().Trim(),
                index,
                "text"
            ));
        }

        logger.LogInformation("Created {ChunkCount} chunks", chunks.Count);
        return chunks;
    }
}
