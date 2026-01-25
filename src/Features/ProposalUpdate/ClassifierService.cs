namespace PdfService.Features.ProposalUpdate;

using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using PdfService.Features.ProposalGeneration;

public class ClassifierService
{
    private readonly AnthropicClient _client;
    private readonly ILogger<ClassifierService> _logger;

    public ClassifierService(string apiKey, ILogger<ClassifierService> logger)
    {
        _client = new AnthropicClient(new APIAuthentication(apiKey));
        _logger = logger;
    }

    public async Task<UpdateClassification> ClassifyUpdateAsync(
        ProposalData existingProposal,
        UpdateRequest updateRequest)
    {
        // Fast check: If new document uploaded = FullRegen
        if (!string.IsNullOrEmpty(updateRequest.NewDocumentPath))
        {
            return new UpdateClassification
            {
                Type = UpdateType.FullRegen,
                Reasoning = "New document uploaded - full regeneration required"
            };
        }

        // Fast check: If full UpdatedBaseRequest provided = FullRegen
        if (updateRequest.UpdatedBaseRequest != null)
        {
            return new UpdateClassification
            {
                Type = UpdateType.FullRegen,
                Reasoning = "Full request data override provided - regeneration required"
            };
        }

        // Use Claude Haiku to classify the change description
        if (string.IsNullOrEmpty(updateRequest.ChangeDescription))
        {
            return new UpdateClassification
            {
                Type = UpdateType.Simple,
                Reasoning = "No change description provided"
            };
        }

        var prompt = BuildClassificationPrompt(existingProposal, updateRequest.ChangeDescription);

        try
        {
            var parameters = new MessageParameters
            {
                Messages = new List<Message>
                {
                    new Message(RoleType.User, prompt)
                },
                MaxTokens = 500,
                Model = "claude-haiku-4-5",  // Fast & cheap
                Stream = false,
                Temperature = 0.0m  // Deterministic
            };

            var response = await _client.Messages.GetClaudeMessageAsync(parameters);
            var content = response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? "";

            var classification = ParseClassificationResponse(content);

            _logger.LogInformation(
                "Update classified as {Type}: {Reasoning}",
                classification.Type,
                classification.Reasoning
            );

            return classification;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying update, defaulting to Complex");
            return new UpdateClassification
            {
                Type = UpdateType.Complex,
                Reasoning = $"Classification failed: {ex.Message}"
            };
        }
    }

    private string BuildClassificationPrompt(ProposalData existingProposal, string changeDescription)
    {
        return $@"You are a proposal update classifier. Analyze the user's requested change and classify it.

EXISTING PROPOSAL:
- Project: {existingProposal.ProjectName}
- Budget: ${existingProposal.TotalBudget:N0}
- Timeline: {existingProposal.Timeline}
- Stages: {string.Join(", ", existingProposal.Stages.Select(s => s.Name))}

USER'S REQUESTED CHANGE:
""{changeDescription}""

CLASSIFICATION RULES:

1. SIMPLE - Choose if change is:
   - Fixing typos or minor text corrections
   - Changing specific numbers (timeline, budget, cost)
   - Replacing specific phrases or words
   - Minor adjustments to existing content
   Examples: ""change deadline to 3 weeks"", ""fix typo in summary"", ""budget should be $60k""

2. COMPLEX - Choose if change is:
   - Reformulating or rewriting sections
   - Changing tone or style
   - Adding/removing content without new context
   - Restructuring information
   Examples: ""make summary more professional"", ""rewrite stage 2 description"", ""add more detail to tasks""

3. FULLREGEN - Choose if change is:
   - Requires complete regeneration
   - Mentions new documents or external context
   - Fundamental changes to project scope
   Examples: ""I uploaded new requirements"", ""completely change the approach"", ""new TZ attached""

Respond in JSON format ONLY:
{{
  ""type"": ""Simple"" | ""Complex"" | ""FullRegen"",
  ""reasoning"": ""brief explanation""
}}";
    }

    private UpdateClassification ParseClassificationResponse(string response)
    {
        try
        {
            // Try to extract JSON from response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<ClassificationJson>(jsonStr, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed != null && Enum.TryParse<UpdateType>(parsed.Type, true, out var updateType))
                {
                    return new UpdateClassification
                    {
                        Type = updateType,
                        Reasoning = parsed.Reasoning ?? ""
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse classification response, defaulting to Complex");
        }

        // Default to Complex if parsing fails
        return new UpdateClassification
        {
            Type = UpdateType.Complex,
            Reasoning = "Failed to parse classification"
        };
    }

    private class ClassificationJson
    {
        public string Type { get; set; } = "";
        public string? Reasoning { get; set; }
    }
}
