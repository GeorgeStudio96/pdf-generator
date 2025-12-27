namespace PdfService.Services;

using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using System.Text.Json;
using PdfService.Models;

public class ClaudeService
{
    private readonly AnthropicClient _client;

    public ClaudeService(string apiKey)
    {
        _client = new AnthropicClient(apiKey);
    }

    public async Task<ProposalData> GenerateProposal(ProposalRequest request)
    {
        var jsonStructure = @"{
  ""projectName"": ""string"",
  ""totalBudget"": number,
  ""timeline"": ""string"",
  ""executiveSummary"": ""string"",
  ""stages"": [
    {
      ""name"": ""string"",
      ""duration"": ""string"",
      ""cost"": number,
      ""tasks"": [""string"", ""string""]
    }
  ],
  ""budgetDetails"": {
    ""items"": [
      {
        ""category"": ""string"",
        ""amount"": number,
        ""percentage"": number
      }
    ],
    ""justification"": ""string""
  }
}";

        var prompt = $@"Analyze this project and generate a detailed commercial proposal.

Project: {request.ProjectName}
Budget: ${request.Budget}
Deadline: {request.Deadline}
Description: {request.Description}

Generate ONLY valid JSON (no extra text) with this exact structure:
{jsonStructure}

Requirements:
- Generate 3-5 project stages
- Each stage should have 2-4 tasks
- Budget breakdown items must sum to exactly ${request.Budget}
- Percentages must sum to 100
- Write in Russian language
- Be professional and detailed";

        var messages = new List<Message>
        {
            new Message(RoleType.User, prompt)
        };

        var parameters = new MessageParameters
        {
            Messages = messages,
            Model = "claude-haiku-4-5",
            MaxTokens = 2000,
            Stream = false
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters);
        var textContent = response.Content.OfType<TextContent>().FirstOrDefault();
        var rawText = textContent?.Text ?? throw new Exception("No text content in AI response");

        Console.WriteLine("=== RAW CLAUDE RESPONSE ===");
        Console.WriteLine(rawText);
        Console.WriteLine("=== END RAW RESPONSE ===");

        return ParseProposalFromResponse(rawText);
    }

    private ProposalData ParseProposalFromResponse(string rawText)
    {
        var cleanJson = CleanJson(rawText);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var result = JsonSerializer.Deserialize<ProposalData>(cleanJson, options) ?? new();

        Console.WriteLine($"[DEBUG] Deserialized - ProjectName: {result.ProjectName}");
        Console.WriteLine($"[DEBUG] Deserialized - TotalBudget: {result.TotalBudget}");
        Console.WriteLine($"[DEBUG] Deserialized - Timeline: {result.Timeline}");
        Console.WriteLine($"[DEBUG] Deserialized - Stages count: {result.Stages.Count}");
        Console.WriteLine($"[DEBUG] Deserialized - BudgetDetails items count: {result.BudgetDetails.Items.Count}");

        return result;
    }

    private string CleanJson(string rawText)
    {
        // Remove markdown code blocks if present
        var cleaned = rawText.Trim();
        if (cleaned.StartsWith("```json"))
        {
            cleaned = cleaned.Substring(7); // Remove ```json
        }
        else if (cleaned.StartsWith("```"))
        {
            cleaned = cleaned.Substring(3); // Remove ```
        }

        if (cleaned.EndsWith("```"))
        {
            cleaned = cleaned.Substring(0, cleaned.Length - 3); // Remove trailing ```
        }

        cleaned = cleaned.Trim();

        // Find JSON boundaries
        var firstBrace = cleaned.IndexOf('{');
        var lastBrace = cleaned.LastIndexOf('}');

        if (firstBrace == -1 || lastBrace == -1 || firstBrace >= lastBrace)
        {
            Console.WriteLine($"[DEBUG] Raw response: {rawText}");
            throw new Exception("No valid JSON found in AI response");
        }

        var json = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
        Console.WriteLine($"[DEBUG] Cleaned JSON: {json}");
        return json;
    }
}
