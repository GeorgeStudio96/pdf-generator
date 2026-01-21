namespace PdfService.PdfGeneration;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfService.Models;
using PdfService.PdfGeneration.Components;

public class ProposalDocument : IDocument
{
    private ProposalData Data { get; }
    private byte[] LogoBytes { get; }

    public ProposalDocument(ProposalData data, byte[] logoBytes)
    {
        Data = data;
        LogoBytes = logoBytes;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);

            page.Content()
                .PaddingVertical(0)
                .Column(col =>
                {
                    // Header - всегда на первой странице
                    col.Item().Component(new HeaderComponent(Data, LogoBytes));

                    // Остальные компоненты с автоматическим переносом на новые страницы
                    col.Item().ShowOnce().Component(new SummaryComponent(Data));
                    col.Item().ShowOnce().Component(new TimelineComponent(Data));
                    col.Item().ShowOnce().Component(new BudgetComponent(Data));
                    col.Item().ShowOnce().Component(new StagesComponent(Data));
                });
        });
    }
}
