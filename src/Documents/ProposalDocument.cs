namespace PdfService.Documents;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfService.Models;
using PdfService.Components;

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

            page.Content().Column(col =>
            {
                col.Item().Component(new HeaderComponent(Data, LogoBytes));
                col.Item().Component(new SummaryComponent(Data));
                col.Item().Component(new TimelineComponent(Data));
                col.Item().Component(new BudgetComponent(Data));
                col.Item().Component(new StagesComponent(Data));
            });
        });
    }
}
