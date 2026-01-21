namespace PdfService.PdfGeneration.Components;

using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using PdfService.Models;

public class PricingBreakdownComponent : IComponent
{
    private ProposalData Data { get; }

    public PricingBreakdownComponent(ProposalData data)
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
                // Заголовок секции
                col.Item()
                    .Text("Обоснование стоимости")
                    .FontFamily(Brand.FontFamily)
                    .FontSize(Brand.Type.Body)
                    .Bold()
                    .FontColor(Brand.Colors.Text);

                // Тип ценообразования
                col.Item()
                    .PaddingTop(Brand.Spacing.Small)
                    .Background(Brand.Colors.Beige)
                    .Padding(Brand.Spacing.Small)
                    .Row(row =>
                    {
                        row.AutoItem()
                            .PaddingRight(Brand.Spacing.Small)
                            .Text("Модель ценообразования:")
                            .FontFamily(Brand.FontFamily)
                            .FontSize(Brand.Type.SmallLabel)
                            .FontColor(Brand.Colors.Text);

                        row.AutoItem()
                            .Text(GetPricingTypeName(Data.Pricing.Type))
                            .FontFamily(Brand.FontFamily)
                            .FontSize(Brand.Type.SmallLabel)
                            .Bold()
                            .FontColor(Brand.Colors.PrimaryOrange);
                    });

                // Если почасовая оплата - показываем ресурсы
                if (Data.Pricing.Type == PricingType.TimeAndMaterial && Data.Pricing.Resources.Any())
                {
                    col.Item()
                        .PaddingTop(Brand.Spacing.Small)
                        .Column(resourceCol =>
                        {
                            resourceCol.Item()
                                .Text("Распределение ресурсов:")
                                .FontFamily(Brand.FontFamily)
                                .FontSize(Brand.Type.SmallLabel)
                                .Bold()
                                .FontColor(Brand.Colors.Text);

                            resourceCol.Item()
                                .PaddingTop(Brand.Spacing.Tiny)
                                .Table(table =>
                                {
                                    // Определяем колонки
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(3); // Роль
                                        columns.RelativeColumn(1); // Часы
                                        columns.RelativeColumn(1); // Ставка
                                        columns.RelativeColumn(1); // Итого
                                    });

                                    // Заголовок таблицы
                                    table.Header(header =>
                                    {
                                        header.Cell().Background(Brand.Colors.PrimaryOrange).Padding(4)
                                            .Text("Роль")
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .FontColor("#FFFFFF");

                                        header.Cell().Background(Brand.Colors.PrimaryOrange).Padding(4)
                                            .Text("Часы")
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .FontColor("#FFFFFF");

                                        header.Cell().Background(Brand.Colors.PrimaryOrange).Padding(4)
                                            .Text("Ставка")
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .FontColor("#FFFFFF");

                                        header.Cell().Background(Brand.Colors.PrimaryOrange).Padding(4)
                                            .Text("Итого")
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .FontColor("#FFFFFF");
                                    });

                                    // Строки с ресурсами
                                    foreach (var resource in Data.Pricing.Resources)
                                    {
                                        table.Cell().Background(Brand.Colors.Beige).Padding(4)
                                            .Text(resource.Role)
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .FontColor(Brand.Colors.Text);

                                        table.Cell().Background(Brand.Colors.Beige).Padding(4)
                                            .Text($"{resource.Hours}ч")
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .FontColor(Brand.Colors.Text);

                                        table.Cell().Background(Brand.Colors.Beige).Padding(4)
                                            .Text($"{resource.Rate:N0}₽")
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .FontColor(Brand.Colors.Text);

                                        table.Cell().Background(Brand.Colors.Beige).Padding(4)
                                            .Text($"{resource.Total:N0}₽")
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .Bold()
                                            .FontColor(Brand.Colors.PrimaryOrange);
                                    }
                                });
                        });
                }

                // Разбивка затрат (если заполнено)
                if (Data.Pricing.DirectCosts > 0 || Data.Pricing.OverheadCosts > 0 ||
                    Data.Pricing.RiskBuffer > 0 || Data.Pricing.ProfitMargin > 0)
                {
                    col.Item()
                        .PaddingTop(Brand.Spacing.Small)
                        .Column(breakdownCol =>
                        {
                            breakdownCol.Item()
                                .Text("Структура затрат:")
                                .FontFamily(Brand.FontFamily)
                                .FontSize(Brand.Type.SmallLabel)
                                .Bold()
                                .FontColor(Brand.Colors.Text);

                            if (Data.Pricing.DirectCosts > 0)
                            {
                                breakdownCol.Item()
                                    .PaddingTop(Brand.Spacing.Tiny)
                                    .Row(row =>
                                    {
                                        row.RelativeItem()
                                            .Text("Прямые затраты (оплата специалистов)")
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .FontColor(Brand.Colors.Text);

                                        row.AutoItem()
                                            .Text($"{Data.Pricing.DirectCosts:N0}₽")
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .Bold()
                                            .FontColor(Brand.Colors.Text);
                                    });
                            }

                            if (Data.Pricing.OverheadCosts > 0)
                            {
                                breakdownCol.Item()
                                    .PaddingTop(Brand.Spacing.Tiny)
                                    .Row(row =>
                                    {
                                        row.RelativeItem()
                                            .Text("Накладные расходы (офис, налоги)")
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .FontColor(Brand.Colors.Text);

                                        row.AutoItem()
                                            .Text($"{Data.Pricing.OverheadCosts:N0}₽")
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .Bold()
                                            .FontColor(Brand.Colors.Text);
                                    });
                            }

                            if (Data.Pricing.RiskBuffer > 0)
                            {
                                breakdownCol.Item()
                                    .PaddingTop(Brand.Spacing.Tiny)
                                    .Row(row =>
                                    {
                                        row.RelativeItem()
                                            .Text("Буфер на риски и непредвиденные ситуации")
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .FontColor(Brand.Colors.Text);

                                        row.AutoItem()
                                            .Text($"{Data.Pricing.RiskBuffer:N0}₽")
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .Bold()
                                            .FontColor(Brand.Colors.Text);
                                    });
                            }

                            if (Data.Pricing.ProfitMargin > 0)
                            {
                                breakdownCol.Item()
                                    .PaddingTop(Brand.Spacing.Tiny)
                                    .Row(row =>
                                    {
                                        row.RelativeItem()
                                            .Text("Маржа")
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .FontColor(Brand.Colors.Text);

                                        row.AutoItem()
                                            .Text($"{Data.Pricing.ProfitMargin:N0}₽")
                                            .FontFamily(Brand.FontFamily)
                                            .FontSize(Brand.Type.SmallLabel - 2)
                                            .Bold()
                                            .FontColor(Brand.Colors.Text);
                                    });
                            }
                        });
                }

                // Обоснование
                if (!string.IsNullOrWhiteSpace(Data.Pricing.Justification))
                {
                    col.Item()
                        .PaddingTop(Brand.Spacing.Small)
                        .Background(Brand.Colors.Beige)
                        .Padding(Brand.Spacing.Small)
                        .Text(Data.Pricing.Justification)
                        .FontFamily(Brand.FontFamily)
                        .FontSize(Brand.Type.SmallLabel)
                        .FontColor(Brand.Colors.Text)
                        .Italic()
                        .LineHeight(1.3f);
                }
            });
    }

    private string GetPricingTypeName(PricingType type)
    {
        return type switch
        {
            PricingType.FixedPrice => "Фиксированная цена",
            PricingType.TimeAndMaterial => "Почасовая оплата",
            PricingType.Hybrid => "Гибридная модель",
            _ => "Не указано"
        };
    }
}
