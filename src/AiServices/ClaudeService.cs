namespace PdfService.AiServices;

using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Anthropic.SDK.Common;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PdfService.Models;
using PdfService.RagDocuments;
using PdfService.Shared;

public class ClaudeService
{
    private readonly AnthropicClient _client;
    private readonly DocumentRepository? _documentRepository;
    private readonly EmbeddingService? _embeddingService;

    // Pre-built JSON schema for proposal tool (avoid rebuilding on every call)
    private static readonly JsonNode ProposalSchema = BuildProposalSchema();

    public ClaudeService(
        string apiKey,
        DocumentRepository? documentRepository = null,
        EmbeddingService? embeddingService = null)
    {
        _client = new AnthropicClient(apiKey);
        _documentRepository = documentRepository;
        _embeddingService = embeddingService;
    }

    public async Task<ProposalData> GenerateProposal(
        ProposalRequest request,
        string? projectId = null)
    {
        // Step 1: Retrieve relevant document context if projectId is provided
        var contextSection = "";
        if (!string.IsNullOrEmpty(projectId) &&
            _documentRepository != null &&
            _embeddingService != null)
        {
            try
            {
                var queryEmbedding = await _embeddingService.GenerateAsync(request.Description);
                var relevantChunks = await _documentRepository.SearchAsync(projectId, queryEmbedding, topK: 3);
                contextSection = FormatContextSection(relevantChunks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Failed to retrieve RAG context: {ex.Message}");
            }
        }

        // Simplified prompt - no JSON structure needed, Tool Use handles it
        var prompt = $@"{contextSection}

Создай коммерческое предложение:
- Проект: {request.ProjectName}
- Бюджет: {request.Budget}₽
- Дедлайн: {request.Deadline}
- Описание: {request.Description}

Требования: 3-5 этапов, сумма бюджета={request.Budget}₽, проценты=100%, на русском.";

        var messages = new List<Message>
        {
            new Message(RoleType.User, prompt)
        };

        // Tool Use for structured output
        var tools = new List<Anthropic.SDK.Common.Tool>
        {
            new Anthropic.SDK.Common.Function(
                "generate_proposal",
                "Генерирует структурированное коммерческое предложение",
                ProposalSchema)
        };

        var parameters = new MessageParameters
        {
            Messages = messages,
            Model = "claude-haiku-4-5",
            MaxTokens = 4000,
            Stream = false,
            Tools = tools,
            ToolChoice = new ToolChoice
            {
                Type = ToolChoiceType.Tool,
                Name = "generate_proposal"
            }
        };

        var response = await _client.Messages.GetClaudeMessageAsync(parameters);

        // Extract tool use result directly - no JSON cleaning needed
        var toolUse = response.Content.OfType<ToolUseContent>().FirstOrDefault()
            ?? throw new Exception("No tool use in AI response");

        var json = toolUse.Input.ToJsonString();
        Console.WriteLine($"[DEBUG] Tool Use JSON: {json}");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var result = JsonSerializer.Deserialize<ProposalData>(json, options) ?? new();

        Console.WriteLine($"[DEBUG] Deserialized - ProjectName: {result.ProjectName}");
        Console.WriteLine($"[DEBUG] Deserialized - TotalBudget: {result.TotalBudget}");
        Console.WriteLine($"[DEBUG] Deserialized - Timeline: {result.Timeline}");
        Console.WriteLine($"[DEBUG] Deserialized - Stages count: {result.Stages.Count}");
        Console.WriteLine($"[DEBUG] Deserialized - BudgetDetails items count: {result.BudgetDetails.Items.Count}");

        return result;
    }

    private static JsonNode BuildProposalSchema()
    {
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["projectName"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Название проекта"
                },
                ["totalBudget"] = new JsonObject
                {
                    ["type"] = "number",
                    ["description"] = "Общий бюджет в рублях"
                },
                ["timeline"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Сроки выполнения проекта"
                },
                ["executiveSummary"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Краткое описание предложения"
                },
                ["stages"] = new JsonObject
                {
                    ["type"] = "array",
                    ["description"] = "Этапы проекта",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["name"] = new JsonObject { ["type"] = "string" },
                            ["duration"] = new JsonObject { ["type"] = "string" },
                            ["cost"] = new JsonObject { ["type"] = "number" },
                            ["tasks"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["items"] = new JsonObject { ["type"] = "string" }
                            }
                        },
                        ["required"] = new JsonArray { "name", "duration", "cost", "tasks" }
                    }
                },
                ["budgetDetails"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["items"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JsonObject
                                {
                                    ["category"] = new JsonObject { ["type"] = "string" },
                                    ["amount"] = new JsonObject { ["type"] = "number" },
                                    ["percentage"] = new JsonObject { ["type"] = "number" }
                                },
                                ["required"] = new JsonArray { "category", "amount", "percentage" }
                            }
                        },
                        ["justification"] = new JsonObject { ["type"] = "string" }
                    },
                    ["required"] = new JsonArray { "items", "justification" }
                }
            },
            ["required"] = new JsonArray
            {
                "projectName", "totalBudget", "timeline",
                "executiveSummary", "stages", "budgetDetails"
            }
        };

        return schema;
    }

    private string FormatContextSection(List<SearchResult> chunks)
    {
        if (chunks.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("КОНТЕКСТ ИЗ ДОКУМЕНТОВ:");

        for (int i = 0; i < chunks.Count; i++)
        {
            sb.AppendLine($"[{i + 1}] {chunks[i].Content}");
        }

        return sb.ToString();
    }
}
