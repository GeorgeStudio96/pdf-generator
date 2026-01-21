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

        // Получить шаблоны для индустрии
        var industryTemplate = IndustryTemplates.GetTemplate(request.Industry);
        var pricingGuidelines = IndustryTemplates.GetPricingGuidelines(request.Industry);

        // Расширенный промпт с учетом индустрии и новых требований
        var prompt = $@"{contextSection}

Создай детальное коммерческое предложение для проекта:
- Название проекта: {request.ProjectName}
- Общий бюджет: {request.Budget}₽
- Дедлайн: {request.Deadline}
- Описание: {request.Description}
- Индустрия: {request.Industry}

{industryTemplate}

{pricingGuidelines}

ОБЯЗАТЕЛЬНЫЕ ТРЕБОВАНИЯ:

1. ЭТАПЫ РАБОТЫ (3-5 этапов):
   - Каждый этап должен иметь понятное название
   - Указать продолжительность (например: ""2-3 недели"", ""1 месяц"")
   - Стоимость этапа
   - Список конкретных задач (3-6 задач на этап)
   - Список deliverables - что именно получит клиент (минимум 2-3 deliverables)
   - Количество включенных раундов правок (обычно 2-3)
   - Политика дополнительных правок (что будет, если нужно больше правок)

2. ЦЕНООБРАЗОВАНИЕ:
   - Определи тип ценообразования (фиксированная цена, почасовая или гибридная)
   - Если почасовая - укажи роли специалистов, часы и ставки
   - Распиши обоснование: почему именно такая стоимость
   - Укажи распределение затрат:
     * Прямые затраты (оплата специалистов)
     * Накладные расходы (10-15%)
     * Буфер на риски (5-10%)
     * Маржа (если применимо)

3. БЮДЖЕТ:
   - Общая сумма ДОЛЖНА равняться {request.Budget}₽
   - Сумма всех этапов = {request.Budget}₽
   - Проценты всех категорий бюджета = 100%

4. СТИЛЬ:
   - На русском языке
   - Профессиональный, но понятный стиль
   - Конкретика, без воды
   - Обоснование каждой цифры

Создай предложение, которое вызовет доверие клиента и покажет прозрачность ценообразования.";

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
                            ["name"] = new JsonObject {
                                ["type"] = "string",
                                ["description"] = "Название этапа"
                            },
                            ["duration"] = new JsonObject {
                                ["type"] = "string",
                                ["description"] = "Длительность этапа (например: '2-3 недели')"
                            },
                            ["cost"] = new JsonObject {
                                ["type"] = "number",
                                ["description"] = "Стоимость этапа в рублях"
                            },
                            ["tasks"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["description"] = "Список задач на этом этапе",
                                ["items"] = new JsonObject { ["type"] = "string" }
                            },
                            ["deliverables"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["description"] = "Что получит клиент после завершения этапа",
                                ["items"] = new JsonObject
                                {
                                    ["type"] = "object",
                                    ["properties"] = new JsonObject
                                    {
                                        ["name"] = new JsonObject { ["type"] = "string" },
                                        ["status"] = new JsonObject {
                                            ["type"] = "string",
                                            ["description"] = "Статус выполнения (по умолчанию: 'Не начато')"
                                        }
                                    },
                                    ["required"] = new JsonArray { "name", "status" }
                                }
                            },
                            ["revisionsIncluded"] = new JsonObject
                            {
                                ["type"] = "number",
                                ["description"] = "Количество включенных раундов правок (обычно 2-3)"
                            },
                            ["revisionPolicy"] = new JsonObject
                            {
                                ["type"] = "string",
                                ["description"] = "Политика дополнительных правок (стоимость, условия)"
                            }
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
                                },
                                ["required"] = new JsonArray { "category", "amount", "percentage" }
                            }
                        },
                        ["justification"] = new JsonObject { ["type"] = "string" }
                    },
                    ["required"] = new JsonArray { "items", "justification" }
                },
                ["pricing"] = new JsonObject
                {
                    ["type"] = "object",
                    ["description"] = "Детальная информация о ценообразовании",
                    ["properties"] = new JsonObject
                    {
                        ["type"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Тип ценообразования: FixedPrice, TimeAndMaterial или Hybrid",
                            ["enum"] = new JsonArray { "FixedPrice", "TimeAndMaterial", "Hybrid" }
                        },
                        ["hourlyRate"] = new JsonObject
                        {
                            ["type"] = "number",
                            ["description"] = "Общая часовая ставка (если применимо)"
                        },
                        ["seniorRate"] = new JsonObject
                        {
                            ["type"] = "number",
                            ["description"] = "Ставка Senior специалиста (если почасовая)"
                        },
                        ["middleRate"] = new JsonObject
                        {
                            ["type"] = "number",
                            ["description"] = "Ставка Middle специалиста (если почасовая)"
                        },
                        ["juniorRate"] = new JsonObject
                        {
                            ["type"] = "number",
                            ["description"] = "Ставка Junior специалиста (если почасовая)"
                        },
                        ["totalHours"] = new JsonObject
                        {
                            ["type"] = "number",
                            ["description"] = "Общее количество часов (если почасовая)"
                        },
                        ["resources"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "Распределение ресурсов по ролям",
                            ["items"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JsonObject
                                {
                                    ["role"] = new JsonObject {
                                        ["type"] = "string",
                                        ["description"] = "Роль специалиста (например: 'Senior Frontend Developer')"
                                    },
                                    ["hours"] = new JsonObject {
                                        ["type"] = "number",
                                        ["description"] = "Количество часов работы"
                                    },
                                    ["rate"] = new JsonObject {
                                        ["type"] = "number",
                                        ["description"] = "Ставка за час в рублях"
                                    }
                                },
                                ["required"] = new JsonArray { "role", "hours", "rate" }
                            }
                        },
                        ["justification"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Обоснование стоимости: почему именно такая цена"
                        },
                        ["directCosts"] = new JsonObject
                        {
                            ["type"] = "number",
                            ["description"] = "Прямые затраты (оплата специалистов)"
                        },
                        ["overheadCosts"] = new JsonObject
                        {
                            ["type"] = "number",
                            ["description"] = "Накладные расходы (офис, налоги, и т.д.)"
                        },
                        ["riskBuffer"] = new JsonObject
                        {
                            ["type"] = "number",
                            ["description"] = "Буфер на риски и непредвиденные ситуации"
                        },
                        ["profitMargin"] = new JsonObject
                        {
                            ["type"] = "number",
                            ["description"] = "Маржа агентства/фрилансера"
                        }
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
