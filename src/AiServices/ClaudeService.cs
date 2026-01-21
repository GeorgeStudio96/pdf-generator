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
using PdfService.Templates;

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

        // Получить краткие шаблоны для индустрии (оптимизировано для скорости)
        var industryHints = GetIndustryHints(request.Industry);

        // Оптимизированный промпт (сокращен для ускорения генерации)
        var prompt = $@"{contextSection}

Коммерческое предложение:
- Проект: {request.ProjectName}
- Бюджет: {request.Budget}₽
- Срок: {request.Deadline}
- Описание: {request.Description}
- Индустрия: {request.Industry}

{industryHints}

ТРЕБОВАНИЯ:
1. 3-5 этапов с названием, длительностью, стоимостью, задачами (3-6), deliverables (2-4), правками (2-3), политикой правок
2. Ценообразование: тип (FixedPrice/TimeAndMaterial/Hybrid), обоснование, ресурсы (роли/часы/ставки), затраты (прямые/накладные/риски/маржа)
3. Сумма этапов = {request.Budget}₽, проценты = 100%
4. Русский язык, конкретика, обоснование цифр

Создай профессиональное предложение с прозрачным ценообразованием.";

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
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
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
                ["projectName"] = new JsonObject { ["type"] = "string" },
                ["totalBudget"] = new JsonObject { ["type"] = "number" },
                ["timeline"] = new JsonObject { ["type"] = "string" },
                ["executiveSummary"] = new JsonObject { ["type"] = "string" },
                ["stages"] = new JsonObject
                {
                    ["type"] = "array",
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
                            },
                            ["deliverables"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["items"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["properties"] = new JsonObject
                                    {
                                        ["name"] = new JsonObject { ["type"] = "string" },
                                        ["status"] = new JsonObject { ["type"] = "string" }
                                    },
                                    ["required"] = new JsonArray { "name", "status" }
                                }
                            },
                            ["revisionsIncluded"] = new JsonObject { ["type"] = "number" },
                            ["revisionPolicy"] = new JsonObject { ["type"] = "string" }
                        },
                        ["required"] = new JsonArray { "name", "duration", "cost", "tasks", "deliverables", "revisionsIncluded", "revisionPolicy" }
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
                                }
                            }
                        },
                        ["justification"] = new JsonObject { ["type"] = "string" }
                    }
                },
                ["pricing"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["type"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["enum"] = new JsonArray { "FixedPrice", "TimeAndMaterial", "Hybrid" }
                        },
                        ["hourlyRate"] = new JsonObject { ["type"] = "number" },
                        ["seniorRate"] = new JsonObject { ["type"] = "number" },
                        ["middleRate"] = new JsonObject { ["type"] = "number" },
                        ["juniorRate"] = new JsonObject { ["type"] = "number" },
                        ["totalHours"] = new JsonObject { ["type"] = "number" },
                        ["resources"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["items"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JsonObject
                                {
                                    ["role"] = new JsonObject { ["type"] = "string" },
                                    ["hours"] = new JsonObject { ["type"] = "number" },
                                    ["rate"] = new JsonObject { ["type"] = "number" }
                                },
                                ["required"] = new JsonArray { "role", "hours", "rate" }
                            }
                        },
                        ["justification"] = new JsonObject { ["type"] = "string" },
                        ["directCosts"] = new JsonObject { ["type"] = "number" },
                        ["overheadCosts"] = new JsonObject { ["type"] = "number" },
                        ["riskBuffer"] = new JsonObject { ["type"] = "number" },
                        ["profitMargin"] = new JsonObject { ["type"] = "number" }
                    },
                    ["required"] = new JsonArray { "type", "justification" }
                }
            },
            ["required"] = new JsonArray
            {
                "projectName", "totalBudget", "timeline",
                "executiveSummary", "stages", "budgetDetails", "pricing"
            }
        };

        return schema;
    }

    private string GetIndustryHints(IndustryType industry)
    {
        return industry switch
        {
            IndustryType.Development => "Этапы: Архитектура → Frontend → Backend → QA → Деплой. Ставки: Senior 3000-5000₽, Middle 2000-3000₽, Junior 1000-1500₽.",
            IndustryType.Design => "Этапы: Исследование → Концепт → UI дизайн → Финализация. Ставки: Art Director 3000-5000₽, Senior 2500-4000₽, Middle 1500-2500₽. 3-4 раунда правок.",
            IndustryType.Marketing => "Этапы: Аудит → Контент → Кампании → Аналитика. Ставки: Strategist 3000-5000₽, Content Manager 1500-2500₽, SMM 1500-2500₽.",
            IndustryType.SEO => "Этапы: Аудит → Техоптимизация → Контент → Линкбилдинг → Мониторинг. Ставки: SEO Strategist 3000-5000₽, Specialist 2000-3000₽.",
            _ => "Создай логичные этапы на основе описания проекта с реалистичными сроками и ценами."
        };
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
