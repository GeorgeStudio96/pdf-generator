namespace PdfService.Features.ProposalGeneration.Components;


using QuestPDF.Fluent;
using PdfService.Models;


using QuestPDF.Infrastructure;

public class TimelineComponent : IComponent
{
    private ProposalData Data { get; }

    public TimelineComponent(ProposalData data)
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
                    .Text("Project Timeline")
                    .FontFamily(Brand.FontFamily)
                    .FontSize(Brand.Type.Body)
                    .Bold()
                    .FontColor(Brand.Colors.Text);

                col.Item()
                    .PaddingTop(Brand.Spacing.Tiny)
                    .Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell()
                                .Background(Brand.Colors.Beige)
                                .Padding(Brand.Spacing.Tiny)
                                .Text("Stage")
                                .FontFamily(Brand.FontFamily)
                                .FontSize(Brand.Type.SmallLabel)
                                .Bold()
                                .FontColor(Brand.Colors.Text);

                            header.Cell()
                                .Background(Brand.Colors.Beige)
                                .Padding(Brand.Spacing.Tiny)
                                .Text("Duration")
                                .FontFamily(Brand.FontFamily)
                                .FontSize(Brand.Type.SmallLabel)
                                .Bold()
                                .FontColor(Brand.Colors.Text);

                            header.Cell()
                                .Background(Brand.Colors.Beige)
                                .Padding(Brand.Spacing.Tiny)
                                .Text("Cost")
                                .FontFamily(Brand.FontFamily)
                                .FontSize(Brand.Type.SmallLabel)
                                .Bold()
                                .FontColor(Brand.Colors.Text);
                        });

                        foreach (var stage in Data.Stages)
                        {
                            table.Cell()
                                .BorderBottom(1)
                                .BorderColor(Brand.Colors.Beige)
                                .Padding(Brand.Spacing.Tiny)
                                .Text(stage.Name)
                                .FontFamily(Brand.FontFamily)
                                .FontSize(Brand.Type.SmallLabel)
                                .FontColor(Brand.Colors.Text);

                            table.Cell()
                                .BorderBottom(1)
                                .BorderColor(Brand.Colors.Beige)
                                .Padding(Brand.Spacing.Tiny)
                                .Text(stage.Duration)
                                .FontFamily(Brand.FontFamily)
                                .FontSize(Brand.Type.SmallLabel)
                                .FontColor(Brand.Colors.Text);

                            table.Cell()
                                .BorderBottom(1)
                                .BorderColor(Brand.Colors.Beige)
                                .Padding(Brand.Spacing.Tiny)
                                .Text($"${stage.Cost:N0}")
                                .FontFamily(Brand.FontFamily)
                                .FontSize(Brand.Type.SmallLabel)
                                .FontColor(Brand.Colors.AccentGreen);
                        }
                    });
            });
    }
}
