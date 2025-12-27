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
            Model = "claude-3-haiku-20240307",
            MaxTokens = 2000,
            Stream = false
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters);
        var textContent = response.Content.OfType<TextContent>().FirstOrDefault();
        var rawText = textContent?.Text ?? throw new Exception("No text content in AI response");

        return ParseProposalFromResponse(rawText);
    }

    private ProposalData ParseProposalFromResponse(string rawText)
    {
        var cleanJson = CleanJson(rawText);
        return JsonSerializer.Deserialize<ProposalData>(cleanJson) ?? new();
    }

    private string CleanJson(string rawText)
    {
        var firstBrace = rawText.IndexOf('{');
        var lastBrace = rawText.LastIndexOf('}');

        if (firstBrace == -1 || lastBrace == -1 || firstBrace >= lastBrace)
        {
            throw new Exception("No valid JSON found in AI response");
        }

        return rawText.Substring(firstBrace, lastBrace - firstBrace + 1);
    }
}
