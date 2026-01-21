namespace PdfService.PdfGeneration.Components;


using QuestPDF.Fluent;
using PdfService.Models;


using QuestPDF.Infrastructure;

public class StagesComponent : IComponent
{
    private ProposalData Data { get; }

    public StagesComponent(ProposalData data)
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
                    .Text("Detailed Stages")
                    .FontFamily(Brand.FontFamily)
                    .FontSize(Brand.Type.Body)
                    .Bold()
                    .FontColor(Brand.Colors.Text);

                col.Item()
                    .PaddingTop(Brand.Spacing.Tiny)
                    .Column(stagesCol =>
                    {
                        foreach (var stage in Data.Stages)
                        {
                            stagesCol.Item()
                                .PaddingTop(Brand.Spacing.Small)
                                .Column(stageCol =>
                                {
                                    stageCol.Item()
                                        .Row(row =>
                                        {
                                            row.RelativeItem()
                                                .Text(stage.Name)
                                                .FontFamily(Brand.FontFamily)
                                                .FontSize(Brand.Type.SmallLabel)
                                                .Bold()
                                                .FontColor(Brand.Colors.Text);

                                            row.AutoItem()
                                                .Text($"${stage.Cost:N0}")
                                                .FontFamily(Brand.FontFamily)
                                                .FontSize(Brand.Type.SmallLabel)
                                                .FontColor(Brand.Colors.AccentGreen);
                                        });

                                    stageCol.Item()
                                        .PaddingTop(2)
                                        .Row(row =>
                                        {
                                            row.AutoItem()
                                                .Text($"Длительность: {stage.Duration}")
                                                .FontFamily(Brand.FontFamily)
                                                .FontSize(Brand.Type.SmallLabel - 2)
                                                .Italic()
                                                .FontColor(Brand.Colors.Text);

                                            row.AutoItem()
                                                .PaddingLeft(Brand.Spacing.Small)
                                                .Text($"| Правки: {stage.RevisionsIncluded} раунда включены")
                                                .FontFamily(Brand.FontFamily)
                                                .FontSize(Brand.Type.SmallLabel - 2)
                                                .Italic()
                                                .FontColor(Brand.Colors.AccentGreen);
                                        });

                                    stageCol.Item()
                                        .PaddingTop(4)
                                        .Column(tasksCol =>
                                        {
                                            foreach (var task in stage.Tasks)
                                            {
                                                tasksCol.Item()
                                                    .PaddingLeft(Brand.Spacing.Small)
                                                    .Text($"• {task}")
                                                    .FontFamily(Brand.FontFamily)
                                                    .FontSize(Brand.Type.SmallLabel)
                                                    .FontColor(Brand.Colors.Text);
                                            }
                                        });

                                    // Deliverables (что получит клиент)
                                    if (stage.Deliverables.Any())
                                    {
                                        stageCol.Item()
                                            .PaddingTop(Brand.Spacing.Tiny)
                                            .Column(delivCol =>
                                            {
                                                delivCol.Item()
                                                    .PaddingLeft(Brand.Spacing.Small)
                                                    .Text("Результаты этапа:")
                                                    .FontFamily(Brand.FontFamily)
                                                    .FontSize(Brand.Type.SmallLabel - 2)
                                                    .Bold()
                                                    .FontColor(Brand.Colors.PrimaryOrange);

                                                foreach (var deliverable in stage.Deliverables)
                                                {
                                                    delivCol.Item()
                                                        .PaddingLeft(Brand.Spacing.Medium)
                                                        .Text($"☐ {deliverable.Name}")
                                                        .FontFamily(Brand.FontFamily)
                                                        .FontSize(Brand.Type.SmallLabel - 2)
                                                        .FontColor(Brand.Colors.Text);
                                                }
                                            });
                                    }

                                    // Политика правок
                                    if (!string.IsNullOrWhiteSpace(stage.RevisionPolicy))
                                    {
                                        stageCol.Item()
                                            .PaddingTop(Brand.Spacing.Tiny)
                                            .PaddingLeft(Brand.Spacing.Small)
                                            .Background(Brand.Colors.Beige)
                                            .Padding(6)
                                            .Text(stage.RevisionPolicy)
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 3)
                                            .Italic()
                                            .FontColor(Brand.Colors.Text)
                                            .LineHeight(1.2f);
                                    }
                                });
                        }
                    });
            });
    }
}
