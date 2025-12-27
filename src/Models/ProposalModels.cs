namespace PdfService.Models;

public record ProposalRequest
{
    public string ProjectName { get; init; } = "";
    public decimal Budget { get; init; }
    public string Deadline { get; init; } = "";
    public string Description { get; init; } = "";
}

public record ProposalData
{
    public string ProjectName { get; init; } = "";
    public decimal TotalBudget { get; init; }
    public string Timeline { get; init; } = "";
    public string ExecutiveSummary { get; init; } = "";
    public List<ProjectStage> Stages { get; init; } = new();
    public BudgetBreakdown BudgetDetails { get; init; } = new();
}

public record ProjectStage
{
    public string Name { get; init; } = "";
    public string Duration { get; init; } = "";
    public decimal Cost { get; init; }
    public List<string> Tasks { get; init; } = new();
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
