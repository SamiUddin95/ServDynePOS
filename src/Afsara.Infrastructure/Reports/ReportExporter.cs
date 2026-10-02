using Afsara.Application.Reports;
using Afsara.Domain.Entities;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Afsara.Infrastructure.Reports;

public class ReportExporter : IReportExporter
{
    private const string HeaderColor = "#1E293B";
    private const string MoneyFormat = "#,##0";

    static ReportExporter() => QuestPDF.Settings.License = LicenseType.Community;

    private static string Period(SalesReport r) => r.From == r.To
        ? r.From.ToString("dd-MMM-yyyy")
        : $"{r.From:dd-MMM-yyyy} to {r.To:dd-MMM-yyyy}";

    private static IEnumerable<(string Label, decimal Value, bool IsCount)> SummaryLines(SalesReport r) => new[]
    {
        ("Completed Orders", (decimal)r.CompletedOrders, true),
        ("Cancelled Orders", (decimal)r.CancelledOrders, true),
        ("Gross Sales", r.GrossSales, false),
        ("Discounts", r.Discounts, false),
        ("Service Charges", r.ServiceCharges, false),
        ("Sales Tax", r.Taxes, false),
        ("Delivery Charges", r.DeliveryCharges, false),
        ("Net Sales", r.NetSales, false),
        ("Cash", r.CashSales, false),
        ("Card", r.CardSales, false),
        ("Online", r.OnlineSales, false),
        ("Dine In", r.DineInSales, false),
        ("Take Away", r.TakeAwaySales, false),
        ("Delivery", r.DeliverySales, false)
    };

    public void ExportExcel(SalesReport report, RestaurantSetting settings, string path)
    {
        using var workbook = new XLWorkbook();

        var summary = workbook.Worksheets.Add("Summary");
        summary.Cell(1, 1).Value = settings.RestaurantName;
        summary.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16);
        summary.Cell(2, 1).Value = $"Sales Report: {Period(report)}";
        summary.Cell(2, 1).Style.Font.SetBold().Font.SetFontSize(12);
        summary.Cell(3, 1).Value = $"Generated: {DateTime.Now:dd-MMM-yyyy hh:mm tt}";
        summary.Cell(3, 1).Style.Font.SetItalic();

        var row = 5;
        WriteHeader(summary, row++, "Description", $"Amount ({settings.CurrencySymbol})");
        foreach (var (label, value, isCount) in SummaryLines(report))
        {
            summary.Cell(row, 1).Value = label;
            summary.Cell(row, 2).Value = value;
            summary.Cell(row, 2).Style.NumberFormat.Format = MoneyFormat;
            if (isCount) summary.Cell(row, 2).Style.NumberFormat.Format = "0";
            if (label == "Net Sales") summary.Range(row, 1, row, 2).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCFCE7"));
            row++;
        }
        summary.Columns().AdjustToContents();

        var orders = workbook.Worksheets.Add("Orders");
        WriteHeader(orders, 1, "Invoice", "Order #", "Date", "Type", "Table", "Status", "Payment", "Total", "Cashier");
        row = 2;
        foreach (var o in report.Orders)
        {
            orders.Cell(row, 1).Value = o.Invoice;
            orders.Cell(row, 2).Value = o.OrderNo;
            orders.Cell(row, 3).Value = o.Date;
            orders.Cell(row, 3).Style.DateFormat.Format = "dd-MMM-yyyy hh:mm AM/PM";
            orders.Cell(row, 4).Value = o.Type;
            orders.Cell(row, 5).Value = o.Table ?? "-";
            orders.Cell(row, 6).Value = o.Status;
            orders.Cell(row, 7).Value = o.Payment ?? "-";
            orders.Cell(row, 8).Value = o.Total;
            orders.Cell(row, 8).Style.NumberFormat.Format = MoneyFormat;
            orders.Cell(row, 9).Value = o.Cashier;
            row++;
        }
        WriteTotalRow(orders, row, 7, 8, report.Orders.Sum(o => o.Total));
        orders.SheetView.FreezeRows(1);
        orders.Columns().AdjustToContents();

        var items = workbook.Worksheets.Add("Item Sales");
        WriteHeader(items, 1, "#", "Item", "Quantity", "Amount");
        row = 2;
        foreach (var (item, index) in report.Items.Select((x, i) => (x, i + 1)))
        {
            items.Cell(row, 1).Value = index;
            items.Cell(row, 2).Value = item.Item;
            items.Cell(row, 3).Value = item.Quantity;
            items.Cell(row, 4).Value = item.Amount;
            items.Cell(row, 4).Style.NumberFormat.Format = MoneyFormat;
            row++;
        }
        items.Cell(row, 3).Value = report.Items.Sum(i => i.Quantity);
        items.Cell(row, 3).Style.Font.SetBold();
        WriteTotalRow(items, row, 2, 4, report.Items.Sum(i => i.Amount));
        items.SheetView.FreezeRows(1);
        items.Columns().AdjustToContents();

        workbook.SaveAs(path);
    }

    private static void WriteHeader(IXLWorksheet sheet, int row, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++) sheet.Cell(row, i + 1).Value = headers[i];
        sheet.Range(row, 1, row, headers.Length).Style
            .Font.SetBold()
            .Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(XLColor.FromHtml(HeaderColor));
    }

    private static void WriteTotalRow(IXLWorksheet sheet, int row, int labelColumn, int valueColumn, decimal total)
    {
        sheet.Cell(row, labelColumn).Value = "TOTAL";
        sheet.Cell(row, valueColumn).Value = total;
        sheet.Cell(row, valueColumn).Style.NumberFormat.Format = MoneyFormat;
        sheet.Range(row, 1, row, valueColumn).Style.Font.SetBold().Border.SetTopBorder(XLBorderStyleValues.Thin);
    }

    public void ExportPdf(SalesReport report, RestaurantSetting settings, string path)
    {
        string Money(decimal v) => v.ToString("N0");

        Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(x => x.FontSize(9));

            page.Header().Column(col =>
            {
                col.Item().Text(settings.RestaurantName).FontSize(18).Bold();
                if (!string.IsNullOrWhiteSpace(settings.Address)) col.Item().Text(settings.Address).FontColor(Colors.Grey.Darken1);
                col.Item().PaddingTop(6).Row(r =>
                {
                    r.RelativeItem().Text($"Sales Report: {Period(report)}").FontSize(12).SemiBold();
                    r.RelativeItem().AlignRight().Text($"Generated: {DateTime.Now:dd-MMM-yyyy hh:mm tt}").FontColor(Colors.Grey.Darken1);
                });
                col.Item().PaddingTop(4).LineHorizontal(1).LineColor(HeaderColor);
            });

            page.Content().PaddingVertical(10).Column(col =>
            {
                col.Spacing(14);

                col.Item().Text("Summary").FontSize(12).Bold();
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                        c.ConstantColumn(20);
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                    });

                    var lines = SummaryLines(report).ToList();
                    var half = (lines.Count + 1) / 2;
                    for (var i = 0; i < half; i++)
                    {
                        SummaryCell(table, lines[i]);
                        table.Cell();
                        if (i + half < lines.Count) SummaryCell(table, lines[i + half]);
                        else { table.Cell(); table.Cell(); }
                    }
                });

                col.Item().Text($"Orders ({report.Orders.Count})").FontSize(12).Bold();
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(1);
                        c.RelativeColumn(2.4f);
                        c.RelativeColumn(1.4f);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1.5f);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.4f);
                        c.RelativeColumn(1.8f);
                    });
                    HeaderRow(table, "Invoice", "Order #", "Date", "Type", "Table", "Status", "Payment", "Total", "Cashier");

                    foreach (var o in report.Orders)
                    {
                        var cancelled = o.Status != "Completed";
                        BodyCell(table, o.Invoice, cancelled);
                        BodyCell(table, o.OrderNo, cancelled);
                        BodyCell(table, o.Date.ToString("dd-MMM-yy hh:mm tt"), cancelled);
                        BodyCell(table, o.Type, cancelled);
                        BodyCell(table, o.Table ?? "-", cancelled);
                        BodyCell(table, o.Status, cancelled);
                        BodyCell(table, o.Payment ?? "-", cancelled);
                        BodyCell(table, Money(o.Total), cancelled, alignRight: true);
                        BodyCell(table, o.Cashier, cancelled);
                    }

                    table.Cell().ColumnSpan(7).PaddingVertical(4).AlignRight().Text("TOTAL").Bold();
                    table.Cell().PaddingVertical(4).AlignRight().Text(Money(report.Orders.Sum(o => o.Total))).Bold();
                    table.Cell();
                });

                col.Item().Text("Item-wise Sales").FontSize(12).Bold();
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(30);
                        c.RelativeColumn(5);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1.8f);
                    });
                    HeaderRow(table, "#", "Item", "Qty", "Amount");

                    var index = 1;
                    foreach (var item in report.Items)
                    {
                        BodyCell(table, (index++).ToString(), false);
                        BodyCell(table, item.Item, false);
                        BodyCell(table, item.Quantity.ToString(), false, alignRight: true);
                        BodyCell(table, Money(item.Amount), false, alignRight: true);
                    }

                    table.Cell().ColumnSpan(2).PaddingVertical(4).AlignRight().Text("TOTAL").Bold();
                    table.Cell().PaddingVertical(4).AlignRight().Text(report.Items.Sum(i => i.Quantity).ToString()).Bold();
                    table.Cell().PaddingVertical(4).AlignRight().Text(Money(report.Items.Sum(i => i.Amount))).Bold();
                });
            });

            page.Footer().Row(r =>
            {
                r.RelativeItem().Text("Powered by ServDyne POS").FontColor(Colors.Grey.Darken1);
                r.RelativeItem().AlignRight().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        })).GeneratePdf(path);

        void SummaryCell(TableDescriptor table, (string Label, decimal Value, bool IsCount) line)
        {
            var highlight = line.Label == "Net Sales";
            var labelCell = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3);
            var valueCell = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).AlignRight();
            var label = labelCell.Text(line.Label);
            var value = valueCell.Text(line.IsCount ? ((int)line.Value).ToString() : $"{settings.CurrencySymbol} {Money(line.Value)}");
            if (highlight)
            {
                label.Bold().FontColor(Colors.Green.Darken2);
                value.Bold().FontColor(Colors.Green.Darken2);
            }
        }
    }

    private static void HeaderRow(TableDescriptor table, params string[] headers)
    {
        table.Header(header =>
        {
            foreach (var h in headers)
                header.Cell().Background(HeaderColor).Padding(4).Text(h).FontColor(Colors.White).Bold();
        });
    }

    private static void BodyCell(TableDescriptor table, string text, bool muted, bool alignRight = false)
    {
        var cell = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3);
        if (alignRight) cell = cell.AlignRight();
        var span = cell.Text(text);
        if (muted) span.FontColor(Colors.Grey.Medium);
    }
}
