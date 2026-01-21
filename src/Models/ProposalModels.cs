namespace PdfService.Models;

public record ProposalRequest
{
    public string ProjectName { get; init; } = "";
    public decimal Budget { get; init; }
    public string Deadline { get; init; } = "";
    public string Description { get; init; } = "";
    public string? ProjectId { get; init; } = null;
    public IndustryType Industry { get; init; } = IndustryType.General;
}

public record ProposalData
{
    public string ProjectName { get; init; } = "";
    public decimal TotalBudget { get; init; }
    public string Timeline { get; init; } = "";
    public string ExecutiveSummary { get; init; } = "";
    public List<ProjectStage> Stages { get; init; } = new();
    public BudgetBreakdown BudgetDetails { get; init; } = new();
    public PricingModel Pricing { get; init; } = new();
}

public record ProjectStage
{
    public string Name { get; init; } = "";
    public string Duration { get; init; } = "";
    public decimal Cost { get; init; }
    public List<string> Tasks { get; init; } = new();
    public List<Deliverable> Deliverables { get; init; } = new();
    public int RevisionsIncluded { get; init; } = 2;
    public string RevisionPolicy { get; init; } = "";
}

public record BudgetBreakdown
{
    public List<BudgetItem> Items { get; init; } = new();
    public string Justification { get; init; } = "";
}

public record BudgetItem
{
    public string Category { get; init; } = "";
    public decimal Amount { get; init; }
    public decimal Percentage { get; init; }
}

// Новые модели для ценообразования
public record PricingModel
{
    public PricingType Type { get; init; } = PricingType.FixedPrice;
    public decimal HourlyRate { get; init; } = 0;
    public decimal SeniorRate { get; init; } = 0;
    public decimal MiddleRate { get; init; } = 0;
    public decimal JuniorRate { get; init; } = 0;
    public int TotalHours { get; init; } = 0;
    public List<ResourceAllocation> Resources { get; init; } = new();
    public string Justification { get; init; } = "";
    public decimal DirectCosts { get; init; } = 0;
    public decimal OverheadCosts { get; init; } = 0;
    public decimal RiskBuffer { get; init; } = 0;
    public decimal ProfitMargin { get; init; } = 0;
}

public record ResourceAllocation
{
    public string Role { get; init; } = "";
    public int Hours { get; init; }
    public decimal Rate { get; init; }
    public decimal Total => Hours * Rate;
}

public record Deliverable
{
    public string Name { get; init; } = "";
    public string Status { get; init; } = "Не начато";
}

public enum PricingType
{
    FixedPrice,      // Фиксированная цена
    TimeAndMaterial, // Почасовая оплата
    Hybrid           // Смешанная модель
}

public enum IndustryType
{
    General,      // Общий
    Development,  // Разработка
    Design,       // Дизайн
    Marketing,    // Маркетинг
    SEO          // SEO оптимизация
}
