namespace PdfService.Features.ProposalGeneration.Components;


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
                                });
                        }
                    });
            });
    }
}
