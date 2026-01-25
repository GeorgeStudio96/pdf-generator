namespace PdfService.Features.ProposalUpdate;

using System.Text;
using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using PdfService.Features.ProposalGeneration;
using PdfService.Features.JobQueue;

public class UpdateService
{
    private readonly ClassifierService _classifier;
    private readonly ClaudeService _claudeService;
    private readonly AnthropicClient _client;
    private readonly ILogger<UpdateService> _logger;

    public UpdateService(
        ClassifierService classifier,
        ClaudeService claudeService,
        string apiKey,
        ILogger<UpdateService> logger)
    {
        _classifier = classifier;
        _claudeService = claudeService;
        _client = new AnthropicClient(new APIAuthentication(apiKey));
        _logger = logger;
    }

    public async Task<ProposalData> UpdateProposalAsync(
        Job existingJob,
        UpdateRequest updateRequest)
    {
        if (existingJob.ProposalData == null)
        {
            throw new InvalidOperationException("Cannot update proposal: no ProposalData found in job");
        }

        // Classify the update type
        var classification = await _classifier.ClassifyUpdateAsync(
            existingJob.ProposalData,
            updateRequest
        );

        _logger.LogInformation(
            "Update type: {Type} - {Reasoning}",
            classification.Type,
            classification.Reasoning
        );

        return classification.Type switch
        {
            UpdateType.Simple => await ApplySimpleUpdateAsync(
                existingJob.ProposalData,
                updateRequest.ChangeDescription!
            ),

            UpdateType.Complex => await ApplyComplexUpdateAsync(
                existingJob.ProposalData,
                updateRequest.ChangeDescription!
            ),

            UpdateType.FullRegen => await RegenerateProposalAsync(
                existingJob,
                updateRequest
            ),

            _ => throw new ArgumentException($"Unknown update type: {classification.Type}")
        };
    }

    private async Task<ProposalData> ApplySimpleUpdateAsync(
        ProposalData existingData,
        string changeDescription)
    {
        _logger.LogInformation("Applying simple update (direct edits)");

        var prompt = BuildSimpleUpdatePrompt(existingData, changeDescription);

        var parameters = new MessageParameters
        {
            Messages = new List<Message>
            {
                new Message(RoleType.User, prompt)
            },
            MaxTokens = 3000,
            Model = "claude-haiku-4-5",  // Fast for simple edits
            Stream = false,
            Temperature = 0.0m
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters);
        var content = response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? "";

        var updatedData = ParseProposalDataFromResponse(content, existingData);
        return updatedData;
    }

    private async Task<ProposalData> ApplyComplexUpdateAsync(
        ProposalData existingData,
        string changeDescription)
    {
        _logger.LogInformation("Applying complex update (reformulation)");

        var prompt = BuildComplexUpdatePrompt(existingData, changeDescription);

        var parameters = new MessageParameters
        {
            Messages = new List<Message>
            {
                new Message(RoleType.User, prompt)
            },
            MaxTokens = 4000,
            Model = "claude-haiku-4-5",  // Still Haiku - good enough for reformulation
            Stream = false,
            Temperature = 0.3m  // Slight creativity for reformulation
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters);
        var content = response.Content.OfType<TextContent>().FirstOrDefault()?.Text ?? "";

        var updatedData = ParseProposalDataFromResponse(content, existingData);
        return updatedData;
    }

    private async Task<ProposalData> RegenerateProposalAsync(
        Job existingJob,
        UpdateRequest updateRequest)
    {
        _logger.LogInformation("Full regeneration required");

        // Use the original ClaudeService with new request or updated request
        var request = updateRequest.UpdatedBaseRequest ?? existingJob.RequestData;

        // Full regeneration with RAG if new document provided
        var projectId = !string.IsNullOrEmpty(updateRequest.NewDocumentPath)
            ? Guid.NewGuid().ToString()  // New project ID for new RAG context
            : existingJob.RequestData.ProjectId;

        return await _claudeService.GenerateProposal(request, projectId);
    }

    private string BuildSimpleUpdatePrompt(ProposalData existingData, string changeDescription)
    {
        var proposalJson = System.Text.Json.JsonSerializer.Serialize(existingData, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        return $@"You are updating an existing proposal with simple changes. Make MINIMAL edits - only change what was explicitly requested.

CURRENT PROPOSAL (JSON):
```json
{proposalJson}
```

USER'S REQUESTED CHANGE:
""{changeDescription}""

INSTRUCTIONS:
1. Make ONLY the requested changes
2. Keep all other content EXACTLY the same
3. If changing numbers, update related calculations
4. Return the FULL updated proposal in the SAME JSON format

Return updated ProposalData JSON ONLY:";
    }

    private string BuildComplexUpdatePrompt(ProposalData existingData, string changeDescription)
    {
        var proposalJson = System.Text.Json.JsonSerializer.Serialize(existingData, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        return $@"You are updating an existing proposal with complex changes. Reformulate/restructure as requested, but maintain the overall proposal structure.

CURRENT PROPOSAL (JSON):
```json
{proposalJson}
```

USER'S REQUESTED CHANGE:
""{changeDescription}""

INSTRUCTIONS:
1. Reformulate/restructure the requested sections
2. Maintain consistency with unchanged parts
3. Keep the same JSON structure
4. Ensure numbers still add up correctly
5. Return the FULL updated proposal

Return updated ProposalData JSON ONLY:";
    }

    private ProposalData ParseProposalDataFromResponse(string response, ProposalData fallback)
    {
        try
        {
            // Extract JSON from response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = System.Text.Json.JsonSerializer.Deserialize<ProposalData>(jsonStr, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed != null)
                {
                    _logger.LogInformation("Successfully parsed updated ProposalData");
                    return parsed;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse updated ProposalData, returning original");
        }

        return fallback;
    }
}
