namespace PdfService.Components;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using PdfService.Models;
using PdfService.Configuration;

public class HeaderComponent : IComponent
{
    private ProposalData Data { get; }
    private byte[] LogoBytes { get; }

    public HeaderComponent(ProposalData data, byte[] logoBytes)
    {
        Data = data;
        LogoBytes = logoBytes;
    }

    public void Compose(IContainer container)
    {
        container
            .Background(Brand.Colors.Background)
            .Padding(Brand.Spacing.Large)
            .Column(col =>
            {
                if (LogoBytes.Length > 0)
                {
                    col.Item()
                        .AlignCenter()
                        .Width(100)
                        .Image(LogoBytes);
                }

                col.Item()
                    .PaddingTop(Brand.Spacing.Medium)
                    .AlignCenter()
                    .Text("Commercial Proposal")
                    .FontFamily(Brand.FontFamily)
                    .FontSize(Brand.Type.SmallLabel)
                    .FontColor(Brand.Colors.Text);

                col.Item()
                    .PaddingTop(Brand.Spacing.Tiny)
                    .AlignCenter()
                    .Text(Data.ProjectName)
                    .FontFamily(Brand.FontFamily)
                    .FontSize(Brand.Type.SubHeadline)
                    .Bold()
                    .FontColor(Brand.Colors.Text);

                col.Item()
                    .PaddingTop(Brand.Spacing.Small)
                    .AlignCenter()
                    .Row(row =>
                    {
                        row.AutoItem()
                            .Text($"${Data.TotalBudget:N0}")
                            .FontFamily(Brand.FontFamily)
                            .FontSize(Brand.Type.SubHeadline)
                            .FontColor(Brand.Colors.PrimaryOrange)
                            .Bold();

                        row.AutoItem()
                            .PaddingLeft(Brand.Spacing.Medium)
                            .Text($"• {Data.Timeline}")
                            .FontFamily(Brand.FontFamily)
                            .FontSize(Brand.Type.Body)
                            .FontColor(Brand.Colors.Text);
                    });
            });
    }
}
