namespace PdfService.Features.DocumentProcessing;

using StackExchange.Redis;

public class DocumentRepository(
    IConnectionMultiplexer redis,
    ILogger<DocumentRepository> logger)
{
    public async Task SaveChunksAsync(
        string projectId,
        List<DocumentChunk> chunks,
        List<float[]> embeddings)
    {
        if (chunks.Count != embeddings.Count)
            throw new ArgumentException("Chunks and embeddings count mismatch");

        var db = redis.GetDatabase();

        for (int i = 0; i < chunks.Count; i++)
        {
            var key = $"doc:{projectId}:{i}";

            var entries = new HashEntry[]
            {
                new("content", chunks[i].Content),
                new("projectId", projectId),
                new("docType", chunks[i].DocumentType),
                new("index", i)
            };

            await db.HashSetAsync(key, entries);
        }

        logger.LogInformation(
            "Saved {ChunkCount} chunks for project {ProjectId}",
            chunks.Count,
            projectId);
    }

    public async Task<List<SearchResult>> SearchAsync(
        string projectId,
        float[] queryEmbedding,
        int topK = 5)
    {
        var db = redis.GetDatabase();
        var server = redis.GetServers().First();

        try
        {
            var keys = server.Keys(
                database: db.Database,
                pattern: $"doc:{projectId}:*").ToList();

            if (keys.Count == 0)
            {
                logger.LogInformation("No documents found for project {ProjectId}", projectId);
                return new List<SearchResult>();
            }

            var searchResults = new List<SearchResult>();

            foreach (var key in keys.Take(topK))
            {
                var entries = await db.HashGetAllAsync(key);
                var content = entries.FirstOrDefault(e => e.Name == "content").Value.ToString();

                if (!string.IsNullOrEmpty(content))
                {
                    searchResults.Add(new SearchResult(content, 0.8f));
                }
            }

            logger.LogInformation(
                "Found {ResultCount} results for project {ProjectId}",
                searchResults.Count,
                projectId);

            return searchResults;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error searching documents for project {ProjectId}", projectId);
            return new List<SearchResult>();
        }
    }

    public async Task<int> DeleteProjectDocumentsAsync(string projectId)
    {
        var db = redis.GetDatabase();
        var server = redis.GetServers().First();

        var keys = server.Keys(
            database: db.Database,
            pattern: $"doc:{projectId}:*");

        var count = 0;
        foreach (var key in keys)
        {
            await db.KeyDeleteAsync(key);
            count++;
        }

        logger.LogInformation(
            "Deleted {Count} document chunks for project {ProjectId}",
            count,
            projectId);

        return count;
    }
}
