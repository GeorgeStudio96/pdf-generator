namespace PdfService.Features.ProposalGeneration.Components;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using PdfService.Models;


public class SummaryComponent : IComponent
{
    private ProposalData Data { get; }

    public SummaryComponent(ProposalData data)
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
                    .Text("Executive Summary")
                    .FontFamily(Brand.FontFamily)
                    .FontSize(Brand.Type.Body)
                    .Bold()
                    .FontColor(Brand.Colors.Text);

                col.Item()
                    .PaddingTop(Brand.Spacing.Tiny)
                    .Text(Data.ExecutiveSummary)
                    .FontFamily(Brand.FontFamily)
                    .FontSize(Brand.Type.SmallLabel)
                    .FontColor(Brand.Colors.Text)
                    .LineHeight(1.4f);
            });
    }
}
