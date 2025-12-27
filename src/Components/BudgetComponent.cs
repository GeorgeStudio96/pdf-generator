namespace PdfService.Components;


using QuestPDF.Fluent;
using PdfService.Models;
using PdfService.Configuration;

using QuestPDF.Infrastructure;

public class BudgetComponent : IComponent
{
    private ProposalData Data { get; }

    public BudgetComponent(ProposalData data)
    {
        Data = data;
    }

    public void Compose(IContainer container)
    {
        container
            .PaddingHorizontal(Brand.Spacing.Medium)
            .PaddingVertical(Brand.Spacing.Small)
            .Column(col =>
            {
                col.Item()
                    .Text("Budget Breakdown")
                    .FontFamily(Brand.FontFamily)
                    .FontSize(Brand.Type.Body)
                    .Bold()
                    .FontColor(Brand.Colors.Text);

                col.Item()
                    .PaddingTop(Brand.Spacing.Small)
                    .Column(innerCol =>
                    {
                        foreach (var item in Data.BudgetDetails.Items)
                        {
                            innerCol.Item()
                                .PaddingBottom(Brand.Spacing.Tiny)
                                .Column(itemCol =>
                                {
                                    itemCol.Item()
                                        .Row(row =>
                                        {
                                            row.RelativeItem()
                                                .Text(item.Category)
                                                .FontFamily(Brand.FontFamily)
                                                .FontSize(Brand.Type.SmallLabel)
                                                .FontColor(Brand.Colors.Text);

                                            row.AutoItem()
                                                .Text($"{item.Percentage:F1}%")
                                                .FontFamily(Brand.FontFamily)
                                                .FontSize(Brand.Type.SmallLabel)
                                                .FontColor(Brand.Colors.Text);

                                            row.AutoItem()
                                                .PaddingLeft(Brand.Spacing.Small)
                                                .Text($"${item.Amount:N0}")
                                                .FontFamily(Brand.FontFamily)
                                                .FontSize(Brand.Type.SmallLabel)
                                                .Bold()
                                                .FontColor(Brand.Colors.PrimaryOrange);
                                        });

                                    itemCol.Item()
                                        .PaddingTop(2)
                                        .Height(8)
                                        .Background(Brand.Colors.Beige)
                                        .ExtendHorizontal()
                                        .Column(stack =>
                                        {
                                            stack.Item()
                                                .Width((float)item.Percentage / 100.0f)
                                                .Height(8)
                                                .Background(Brand.Colors.PrimaryOrange);
                                        });
                                });
                        }
                    });

                col.Item()
                    .PaddingTop(Brand.Spacing.Small)
                    .Background(Brand.Colors.Beige)
                    .Padding(Brand.Spacing.Small)
                    .Text(Data.BudgetDetails.Justification)
                    .FontFamily(Brand.FontFamily)
                    .FontSize(Brand.Type.SmallLabel)
                    .FontColor(Brand.Colors.Text)
                    .Italic()
                    .LineHeight(1.3f);
            });
    }
}
