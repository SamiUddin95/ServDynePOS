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

        WriteOrdersSheet(workbook, report.Orders);

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

    private static void WriteOrdersSheet(XLWorkbook workbook, List<ReportOrderRow> list)
    {
        var orders = workbook.Worksheets.Add("Orders");
        WriteHeader(orders, 1, "Invoice", "Order #", "Date", "Type", "Table", "Status", "Payment", "Total", "Cashier");
        var row = 2;
        foreach (var o in list)
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
        WriteTotalRow(orders, row, 7, 8, list.Sum(o => o.Total));
        orders.SheetView.FreezeRows(1);
        orders.Columns().AdjustToContents();
    }

    private static IEnumerable<(string Label, decimal Value, bool IsCount)> DaySummaryLines(DaySalesReport day, SalesReport sales) =>
        new[]
        {
            ("Business Days", (decimal)day.Sessions.Count, true),
            ("Items Sold", (decimal)day.ItemsSold, true),
            ("Unique Items", (decimal)day.Items.Count, true),
            ("Average Order", day.AverageOrder, false)
        }.Concat(SummaryLines(sales));

    private static string SessionClose(DaySessionRow s) => s.IsClosed ? $"{s.ClosedAt:dd-MMM-yyyy hh:mm tt}" : "OPEN";

    public void ExportDaySalesExcel(DaySalesReport report, SalesReport sales, RestaurantSetting settings, string path)
    {
        using var workbook = new XLWorkbook();
        const string percent = "0.0\"%\"";

        var summary = workbook.Worksheets.Add("Summary");
        summary.Cell(1, 1).Value = settings.RestaurantName;
        summary.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16);
        summary.Cell(2, 1).Value = $"Day Sales Report: {report.Period}";
        summary.Cell(2, 1).Style.Font.SetBold().Font.SetFontSize(12);
        summary.Cell(3, 1).Value = $"Generated: {DateTime.Now:dd-MMM-yyyy hh:mm tt}";
        summary.Cell(3, 1).Style.Font.SetItalic();

        var row = 5;
        WriteHeader(summary, row++, "Business Date", "Day Open", "Opened By", "Day Close", "Closed By", "Orders", "Net Sales");
        foreach (var s in report.Sessions)
        {
            summary.Cell(row, 1).Value = s.BusinessDate;
            summary.Cell(row, 1).Style.DateFormat.Format = "dd-MMM-yyyy";
            summary.Cell(row, 2).Value = s.OpenedAt.ToString("dd-MMM-yyyy hh:mm tt");
            summary.Cell(row, 3).Value = s.OpenedBy;
            summary.Cell(row, 4).Value = SessionClose(s);
            summary.Cell(row, 5).Value = s.ClosedBy ?? "-";
            summary.Cell(row, 6).Value = s.CompletedOrders;
            summary.Cell(row, 7).Value = s.NetSales;
            summary.Cell(row, 7).Style.NumberFormat.Format = MoneyFormat;
            row++;
        }
        if (report.Sessions.Count == 0) summary.Cell(row++, 1).Value = "No business day was opened in this period.";

        row++;
        WriteHeader(summary, row++, "Description", $"Amount ({settings.CurrencySymbol})");
        foreach (var (label, value, isCount) in DaySummaryLines(report, sales))
        {
            summary.Cell(row, 1).Value = label;
            summary.Cell(row, 2).Value = value;
            summary.Cell(row, 2).Style.NumberFormat.Format = isCount ? "0" : MoneyFormat;
            if (label == "Net Sales") summary.Range(row, 1, row, 2).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCFCE7"));
            row++;
        }
        if (report.TopItem is { } top)
        {
            summary.Cell(row, 1).Value = "Top Seller";
            summary.Cell(row++, 2).Value = $"{top.Item} ({top.Quantity})";
        }
        if (report.PeakHour is { } peak)
        {
            summary.Cell(row, 1).Value = "Peak Hour";
            summary.Cell(row, 2).Value = peak.Label;
        }
        summary.Columns().AdjustToContents();

        var items = workbook.Worksheets.Add("Item Sales");
        WriteHeader(items, 1, "Rank", "Item", "Category", "Qty", "Orders", "Avg Price", "Amount", "Qty Share", "Sales Share", "First Sold", "Last Sold");
        row = 2;
        foreach (var i in report.Items)
        {
            items.Cell(row, 1).Value = i.Rank;
            items.Cell(row, 2).Value = i.Item;
            items.Cell(row, 3).Value = i.Category;
            items.Cell(row, 4).Value = i.Quantity;
            items.Cell(row, 5).Value = i.Orders;
            items.Cell(row, 6).Value = i.AveragePrice;
            items.Cell(row, 7).Value = i.Amount;
            items.Cell(row, 8).Value = i.QuantityShare;
            items.Cell(row, 9).Value = i.AmountShare;
            items.Cell(row, 10).Value = i.FirstSold;
            items.Cell(row, 11).Value = i.LastSold;
            items.Range(row, 6, row, 7).Style.NumberFormat.Format = MoneyFormat;
            items.Range(row, 8, row, 9).Style.NumberFormat.Format = percent;
            items.Range(row, 10, row, 11).Style.DateFormat.Format = "dd-MMM-yyyy hh:mm AM/PM";
            if (i.Rank <= 3) items.Range(row, 1, row, 11).Style.Font.SetBold();
            row++;
        }
        items.Cell(row, 4).Value = report.ItemsSold;
        items.Cell(row, 4).Style.Font.SetBold();
        WriteTotalRow(items, row, 2, 7, report.Items.Sum(i => i.Amount));
        items.SheetView.FreezeRows(1);
        items.Columns().AdjustToContents();

        var categories = workbook.Worksheets.Add("Categories");
        WriteHeader(categories, 1, "Category", "Items", "Qty", "Amount", "Sales Share");
        row = 2;
        foreach (var c in report.Categories)
        {
            categories.Cell(row, 1).Value = c.Category;
            categories.Cell(row, 2).Value = c.Items;
            categories.Cell(row, 3).Value = c.Quantity;
            categories.Cell(row, 4).Value = c.Amount;
            categories.Cell(row, 4).Style.NumberFormat.Format = MoneyFormat;
            categories.Cell(row, 5).Value = c.AmountShare;
            categories.Cell(row, 5).Style.NumberFormat.Format = percent;
            row++;
        }
        WriteTotalRow(categories, row, 1, 4, report.Categories.Sum(c => c.Amount));
        categories.Columns().AdjustToContents();

        var hourly = workbook.Worksheets.Add("Hourly");
        WriteHeader(hourly, 1, "Hour", "Orders", "Items", "Sales");
        row = 2;
        foreach (var h in report.Hours)
        {
            hourly.Cell(row, 1).Value = h.Label;
            hourly.Cell(row, 2).Value = h.Orders;
            hourly.Cell(row, 3).Value = h.Quantity;
            hourly.Cell(row, 4).Value = h.Amount;
            hourly.Cell(row, 4).Style.NumberFormat.Format = MoneyFormat;
            row++;
        }
        WriteTotalRow(hourly, row, 1, 4, report.Hours.Sum(h => h.Amount));
        hourly.Columns().AdjustToContents();

        WriteOrdersSheet(workbook, sales.Orders);
        workbook.SaveAs(path);
    }

    public void ExportDaySalesPdf(DaySalesReport report, SalesReport sales, RestaurantSetting settings, string path)
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
                    r.RelativeItem().Text($"Day Sales Report: {report.Period}").FontSize(12).SemiBold();
                    r.RelativeItem().AlignRight().Text($"Generated: {DateTime.Now:dd-MMM-yyyy hh:mm tt}").FontColor(Colors.Grey.Darken1);
                });
                col.Item().PaddingTop(4).LineHorizontal(1).LineColor(HeaderColor);
            });

            page.Content().PaddingVertical(10).Column(col =>
            {
                col.Spacing(14);

                col.Item().Text("Day Open / Close").FontSize(12).Bold();
                if (report.Sessions.Count == 0) col.Item().Text("No business day was opened in this period.").FontColor(Colors.Grey.Darken1);
                else col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(1.6f);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(1.6f);
                        c.RelativeColumn(2.2f);
                        c.RelativeColumn(1.6f);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1.4f);
                    });
                    HeaderRow(table, "Business Date", "Day Open", "Opened By", "Day Close", "Closed By", "Orders", "Net Sales");
                    foreach (var s in report.Sessions)
                    {
                        BodyCell(table, s.BusinessDate.ToString("dd-MMM-yyyy"), false);
                        BodyCell(table, s.OpenedAt.ToString("dd-MMM hh:mm tt"), false);
                        BodyCell(table, s.OpenedBy, false);
                        BodyCell(table, s.IsClosed ? $"{s.ClosedAt:dd-MMM hh:mm tt}" : "OPEN", !s.IsClosed);
                        BodyCell(table, s.ClosedBy ?? "-", false);
                        BodyCell(table, s.CompletedOrders.ToString(), false, alignRight: true);
                        BodyCell(table, Money(s.NetSales), false, alignRight: true);
                    }
                });

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

                    var lines = DaySummaryLines(report, sales).ToList();
                    var half = (lines.Count + 1) / 2;
                    for (var i = 0; i < half; i++)
                    {
                        SummaryCell(table, lines[i]);
                        table.Cell();
                        if (i + half < lines.Count) SummaryCell(table, lines[i + half]);
                        else { table.Cell(); table.Cell(); }
                    }
                });
                var highlights = string.Join("     |     ", new[]
                {
                    report.TopItem is { } top ? $"Top Seller: {top.Item} ({top.Quantity} sold)" : null,
                    report.PeakHour is { } peak ? $"Peak Hour: {peak.Label} ({peak.Orders} orders)" : null
                }.Where(t => t is not null));
                if (highlights.Length > 0) col.Item().Text(highlights).SemiBold().FontColor(Colors.Orange.Darken2);

                col.Item().Text("Item-wise Sales (most sold first)").FontSize(12).Bold();
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(30);
                        c.RelativeColumn(4);
                        c.RelativeColumn(2);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(1.4f);
                        c.RelativeColumn(1.6f);
                        c.RelativeColumn(1.2f);
                    });
                    HeaderRow(table, "#", "Item", "Category", "Qty", "Orders", "Avg Price", "Amount", "Share");
                    foreach (var i in report.Items)
                    {
                        BodyCell(table, i.Rank.ToString(), false);
                        BodyCell(table, i.Item, false);
                        BodyCell(table, i.Category, false);
                        BodyCell(table, i.Quantity.ToString(), false, alignRight: true);
                        BodyCell(table, i.Orders.ToString(), false, alignRight: true);
                        BodyCell(table, Money(i.AveragePrice), false, alignRight: true);
                        BodyCell(table, Money(i.Amount), false, alignRight: true);
                        BodyCell(table, $"{i.QuantityShare:0.0}%", false, alignRight: true);
                    }
                    table.Cell().ColumnSpan(3).PaddingVertical(4).AlignRight().Text("TOTAL").Bold();
                    table.Cell().PaddingVertical(4).AlignRight().Text(report.ItemsSold.ToString()).Bold();
                    table.Cell().ColumnSpan(3).PaddingVertical(4).AlignRight().Text(Money(report.Items.Sum(i => i.Amount))).Bold();
                    table.Cell();
                });

                col.Item().Row(r =>
                {
                    r.Spacing(16);
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().PaddingBottom(4).Text("Category Summary").FontSize(12).Bold();
                        c.Item().Table(table =>
                        {
                            table.ColumnsDefinition(d =>
                            {
                                d.RelativeColumn(3);
                                d.RelativeColumn(1);
                                d.RelativeColumn(1.6f);
                                d.RelativeColumn(1.2f);
                            });
                            HeaderRow(table, "Category", "Qty", "Amount", "Share");
                            foreach (var cat in report.Categories)
                            {
                                BodyCell(table, cat.Category, false);
                                BodyCell(table, cat.Quantity.ToString(), false, alignRight: true);
                                BodyCell(table, Money(cat.Amount), false, alignRight: true);
                                BodyCell(table, $"{cat.AmountShare:0.0}%", false, alignRight: true);
                            }
                        });
                    });
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().PaddingBottom(4).Text("Hourly Sales").FontSize(12).Bold();
                        c.Item().Table(table =>
                        {
                            table.ColumnsDefinition(d =>
                            {
                                d.RelativeColumn(2.4f);
                                d.RelativeColumn(1);
                                d.RelativeColumn(1);
                                d.RelativeColumn(1.6f);
                            });
                            HeaderRow(table, "Hour", "Orders", "Items", "Sales");
                            foreach (var h in report.Hours)
                            {
                                BodyCell(table, h.Label, false);
                                BodyCell(table, h.Orders.ToString(), false, alignRight: true);
                                BodyCell(table, h.Quantity.ToString(), false, alignRight: true);
                                BodyCell(table, Money(h.Amount), false, alignRight: true);
                            }
                        });
                    });
                });

                col.Item().Text($"Orders ({sales.Orders.Count})").FontSize(12).Bold();
                col.Item().Element(e => OrdersTable(e, sales.Orders));
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
            var label = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text(line.Label);
            var value = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).AlignRight()
                .Text(line.IsCount ? ((int)line.Value).ToString() : $"{settings.CurrencySymbol} {Money(line.Value)}");
            if (highlight)
            {
                label.Bold().FontColor(Colors.Green.Darken2);
                value.Bold().FontColor(Colors.Green.Darken2);
            }
        }
    }

    private static void OrdersTable(IContainer container, List<ReportOrderRow> orders)
    {
        container.Table(table =>
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

            foreach (var o in orders)
            {
                var cancelled = o.Status != "Completed";
                BodyCell(table, o.Invoice, cancelled);
                BodyCell(table, o.OrderNo, cancelled);
                BodyCell(table, o.Date.ToString("dd-MMM-yy hh:mm tt"), cancelled);
                BodyCell(table, o.Type, cancelled);
                BodyCell(table, o.Table ?? "-", cancelled);
                BodyCell(table, o.Status, cancelled);
                BodyCell(table, o.Payment ?? "-", cancelled);
                BodyCell(table, o.Total.ToString("N0"), cancelled, alignRight: true);
                BodyCell(table, o.Cashier, cancelled);
            }

            table.Cell().ColumnSpan(7).PaddingVertical(4).AlignRight().Text("TOTAL").Bold();
            table.Cell().PaddingVertical(4).AlignRight().Text(orders.Sum(o => o.Total).ToString("N0")).Bold();
            table.Cell();
        });
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
                col.Item().Element(e => OrdersTable(e, report.Orders));

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
