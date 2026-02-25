namespace PdfService.Features.ProposalGeneration;

using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using Anthropic.SDK.Common;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PdfService.Features.DocumentProcessing;
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

        var budgetLine = request.Budget > 0
            ? $"- Бюджет: {request.Budget}₽"
            : "- Бюджет: не указан — рассчитай рыночную стоимость самостоятельно, исходя из объёма работ, стека технологий и рыночных ставок для фрилансеров";

        var budgetConstraint = request.Budget > 0
            ? $"- Суммы этапов должны давать итог ровно {request.Budget}₽, проценты=100%"
            : "- Рассчитай реалистичную рыночную стоимость каждого этапа, проценты=100%";

        // Simplified prompt - no JSON structure needed, Tool Use handles it
        var prompt = $@"{contextSection}
Составь живое, профессиональное коммерческое предложение от первого лица для следующего проекта:
- Проект: {request.ProjectName}
{budgetLine}
- Дедлайн: {request.Deadline}
- Описание: {request.Description}

Требования:
- 3-5 этапов, на русском
{budgetConstraint}
- Пиши от первого лица: «я разработаю», «предлагаю», «мой опыт позволяет»
- Если в документах есть портфолио или прошлые проекты — ссылайся на них как на свои: «я уже реализовывал подобные задачи», «в моей практике был аналогичный проект»
- Не упоминай своё имя, не ссылайся на себя в третьем лице";

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

        var systemPrompt = @"Ты — профессиональный фрилансер, составляющий коммерческое предложение для своего клиента.

ВАЖНО:
- Пиши строго от первого лица (я, мне, мой, буду, разработаю, предлагаю)
- НИКОГДА не упоминай себя по имени и не ссылайся на себя в третьем лице
- Тон: официально-деловой, но живой и персональный — как письмо профессионала клиенту
- Не используй шаблонные канцелярские обороты
- Опыт и портфолио из документов используй как доказательства своей компетентности, говоря «я реализовал», «в моей практике», «аналогичные задачи я уже решал» и т.п.";

        var parameters = new MessageParameters
        {
            Messages = messages,
            Model = "claude-haiku-4-5",
            MaxTokens = 4000,
            Stream = false,
            System = [new SystemMessage(systemPrompt)],
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
                    ["description"] = "Общий бюджет в рублях. Если бюджет не задан клиентом — самостоятельно рассчитай реалистичную рыночную стоимость исходя из объёма работ, стека и своего опыта. НИКОГДА не ставь 0."
                },
                ["timeline"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Сроки выполнения проекта"
                },
                ["executiveSummary"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Краткое описание предложения от первого лица (я/мне/мой). Живой, деловой текст — как будто пишешь клиенту. Не упоминай себя по имени, не ссылайся на себя в третьем лице."
                },
                ["stages"] = new JsonObject
                {
                    ["type"] = "array",
                    ["description"] = "Этапы проекта. Названия и задачи — от первого лица, где уместно.",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["name"] = new JsonObject { ["type"] = "string", ["description"] = "Название этапа" },
                            ["duration"] = new JsonObject { ["type"] = "string", ["description"] = "Длительность этапа" },
                            ["cost"] = new JsonObject { ["type"] = "number", ["description"] = "Стоимость этапа в рублях. Должна быть больше 0. Если общий бюджет не задан — оцени самостоятельно по рыночным ставкам." },
                            ["tasks"] = new JsonObject
                            {
                                ["type"] = "array",
                                ["description"] = "Конкретные задачи, которые я буду выполнять на этом этапе",
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
                        ["justification"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Обоснование бюджета от первого лица. Можно ссылаться на прошлые проекты как 'я реализовывал', 'в моей практике'. Не писать 'на основе опыта [имя]' или любые упоминания себя в третьем лице."
                        }
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
        sb.AppendLine("<documents>");

        for (int i = 0; i < chunks.Count; i++)
        {
            sb.AppendLine($"  <document index=\"{i + 1}\">");
            sb.AppendLine($"    <document_content>{chunks[i].Content}</document_content>");
            sb.AppendLine($"  </document>");
        }

        sb.AppendLine("</documents>");
        return sb.ToString();
    }
}
