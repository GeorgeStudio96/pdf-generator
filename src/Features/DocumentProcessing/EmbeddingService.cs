namespace PdfService.Features.DocumentProcessing;

using System.Text;
using System.Text.Json;

public class EmbeddingService(
    HttpClient httpClient,
    ILogger<EmbeddingService> logger)
{
    private const string Endpoint = "https://api.openai.com/v1/embeddings";
    private const string Model = "text-embedding-3-small";
    private const int Dimensions = 1536;

    private readonly string _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
        ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable not set");

    public async Task<float[]> GenerateAsync(string text)
    {
        var result = await GenerateBatchAsync([text]);
        return result[0];
    }

    public async Task<List<float[]>> GenerateBatchAsync(List<string> texts)
    {
        if (texts.Count == 0)
            throw new ArgumentException("Texts list is empty");

        logger.LogInformation("Generating embeddings for {Count} texts via API", texts.Count);

        var requestBody = new
        {
            input = texts,
            model = Model,
            dimensions = Dimensions
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = content
        };
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var data = JsonDocument.Parse(responseJson);

        var embeddings = new List<float[]>();
        foreach (var item in data.RootElement.GetProperty("data").EnumerateArray())
        {
            var embedding = item
                .GetProperty("embedding")
                .EnumerateArray()
                .Select(x => x.GetSingle())
                .ToArray();

            embeddings.Add(embedding);
        }

        logger.LogInformation("Generated {Count} embeddings successfully", embeddings.Count);
        return embeddings;
    }
}
