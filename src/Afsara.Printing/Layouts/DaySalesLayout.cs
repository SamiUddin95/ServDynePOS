using System.Drawing;
using Afsara.Application.Reports;
using Afsara.Domain.Entities;
using Afsara.Printing.Rendering;

namespace Afsara.Printing.Layouts;

public static class DaySalesLayout
{
    public static Receipt Build(DaySalesReport report, RestaurantSetting settings, string printedBy)
    {
        string Money(decimal value) => value.ToString("N0");

        var r = new Receipt()
            .Center(settings.RestaurantName, 13f, FontStyle.Bold)
            .Center(settings.Address, 8f)
            .Space(2)
            .Band("DAY SALES REPORT", 10.5f)
            .Space(2)
            .Center(report.SpansMultipleDates ? $"Period: {report.Period}" : $"Business Date: {report.Period}", 10f, FontStyle.Bold);
        if (report.Sessions.Any(s => !s.IsClosed)) r.Center("*** DAY OPEN - LIVE FIGURES ***", 8.5f, FontStyle.Bold);
        r.Rule(RuleStyle.Solid);

        void Line(string label, string value, FontStyle style = FontStyle.Regular) =>
            r.Row(9f, style, new Cell(label, 0.5f), new Cell(value, 0.5f, StringAlignment.Far));

        if (report.Sessions.Count == 0) r.Center("No business day was opened in this period.", 8.5f);
        foreach (var s in report.Sessions)
        {
            if (report.Sessions.Count > 1) r.Text($"Business Date {s.BusinessDate:dd-MMM-yyyy}", 8.5f, FontStyle.Bold);
            Line("Day Open", $"{s.OpenedAt:dd-MMM hh:mm tt}  {s.OpenedBy}");
            Line("Day Close", s.IsClosed ? $"{s.ClosedAt:dd-MMM hh:mm tt}  {s.ClosedBy}" : "OPEN");
            if (report.Sessions.Count > 1) Line($"Orders {s.CompletedOrders}", Money(s.NetSales));
        }
        r.Rule();
        Line("Completed Orders", report.CompletedOrders.ToString());
        Line("Cancelled Orders", report.CancelledOrders.ToString());
        Line("Items Sold", report.ItemsSold.ToString("N0"));
        Line("Unique Items", report.Items.Count.ToString());
        Line("Average Order", Money(report.AverageOrder));
        if (report.TopItem is { } top) Line("Top Seller", $"{top.Item} ({top.Quantity})", FontStyle.Bold);
        if (report.PeakHour is { } peak) Line("Peak Hour", peak.Label);
        r.Rule(RuleStyle.Double)
            .Row(11f, FontStyle.Bold, new Cell("NET SALES", 0.5f), new Cell($"{settings.CurrencySymbol} {Money(report.NetSales)}", 0.5f, StringAlignment.Far))
            .Rule(RuleStyle.Double);

        if (report.Items.Count > 0)
        {
            r.Text("ITEM-WISE SALES (MOST SOLD FIRST)", 9f, FontStyle.Bold)
                .Rule(RuleStyle.Solid)
                .Row(8.5f, FontStyle.Bold, new Cell("#", 0.08f), new Cell("Item", 0.5f), new Cell("Qty", 0.14f, StringAlignment.Center), new Cell("Amount", 0.28f, StringAlignment.Far))
                .Rule(RuleStyle.Solid);
            foreach (var item in report.Items)
                r.Row(8.5f, item.Rank <= 3 ? FontStyle.Bold : FontStyle.Regular,
                    new Cell(item.Rank.ToString(), 0.08f), new Cell(item.Item, 0.5f),
                    new Cell(item.Quantity.ToString(), 0.14f, StringAlignment.Center), new Cell(Money(item.Amount), 0.28f, StringAlignment.Far));
            r.Rule()
                .Row(9f, FontStyle.Bold, new Cell("TOTAL", 0.58f), new Cell(report.ItemsSold.ToString("N0"), 0.14f, StringAlignment.Center), new Cell(Money(report.Items.Sum(i => i.Amount)), 0.28f, StringAlignment.Far));
        }

        if (report.Categories.Count > 0)
        {
            r.Rule(RuleStyle.Solid).Text("CATEGORY SUMMARY", 9f, FontStyle.Bold)
                .Row(8.5f, FontStyle.Bold, new Cell("Category", 0.44f), new Cell("Qty", 0.14f, StringAlignment.Center), new Cell("Amount", 0.26f, StringAlignment.Far), new Cell("%", 0.16f, StringAlignment.Far));
            foreach (var c in report.Categories)
                r.Row(8.5f, FontStyle.Regular, new Cell(c.Category, 0.44f), new Cell(c.Quantity.ToString(), 0.14f, StringAlignment.Center),
                    new Cell(Money(c.Amount), 0.26f, StringAlignment.Far), new Cell($"{c.AmountShare:0.#}%", 0.16f, StringAlignment.Far));
        }

        if (report.Hours.Count > 0)
        {
            r.Rule(RuleStyle.Solid).Text("HOURLY SALES", 9f, FontStyle.Bold)
                .Row(8.5f, FontStyle.Bold, new Cell("Hour", 0.44f), new Cell("Orders", 0.18f, StringAlignment.Center), new Cell("Sales", 0.38f, StringAlignment.Far));
            foreach (var h in report.Hours)
                r.Row(8.5f, FontStyle.Regular, new Cell(h.Label, 0.44f), new Cell(h.Orders.ToString(), 0.18f, StringAlignment.Center), new Cell(Money(h.Amount), 0.38f, StringAlignment.Far));
        }

        return r.Rule(RuleStyle.Double)
            .Center($"Printed By: {printedBy}  |  {DateTime.Now:dd-MMM-yyyy hh:mm tt}", 7f)
            .Center("Software by ServDyne POS", 7f)
            .Space(10);
    }
}
