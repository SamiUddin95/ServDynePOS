using System.Drawing;
using Afsara.Application.Days;
using Afsara.Domain.Entities;
using Afsara.Printing.Rendering;

namespace Afsara.Printing.Layouts;

public static class DayCloseLayout
{
    public static Receipt Build(DayReport report, RestaurantSetting settings, string printedBy, bool duplicate)
    {
        string Money(decimal value) => value.ToString("N0");

        var r = new Receipt()
            .Center(settings.RestaurantName, 13f, FontStyle.Bold)
            .Center(settings.Address, 8f)
            .Space(2)
            .Band(report.IsClosed ? "DAY CLOSE REPORT (Z)" : "DAY STATUS REPORT (X)", 10.5f);
        if (duplicate) r.Center("*** DUPLICATE ***", 9f, FontStyle.Bold);

        r.Space(2)
            .Center($"Business Date: {report.BusinessDate:dd-MMM-yyyy}", 10f, FontStyle.Bold)
            .Rule(RuleStyle.Solid);

        void Line(string label, string value, FontStyle style = FontStyle.Regular, float size = 9f) =>
            r.Row(size, style, new Cell(label, 0.6f), new Cell(value, 0.4f, StringAlignment.Far));

        Line("Opened By", report.OpenedBy);
        Line("Opened At", report.OpenedAt.ToString("dd-MMM hh:mm tt"));
        if (report.IsClosed)
        {
            Line("Closed By", report.ClosedBy ?? string.Empty);
            Line("Closed At", report.ClosedAt?.ToString("dd-MMM hh:mm tt") ?? string.Empty);
        }

        r.Rule().Text("SALES", 9f, FontStyle.Bold);
        Line("Completed Orders", report.CompletedOrders.ToString());
        Line("Cancelled Orders", report.CancelledOrders.ToString());
        if (!report.IsClosed && report.RunningOrders > 0)
            Line("Running Orders", $"{report.RunningOrders} ({Money(report.RunningAmount)})");
        Line("Gross Sales", Money(report.GrossSales));
        Line("Discounts", report.Discounts > 0 ? $"- {Money(report.Discounts)}" : "0");
        Line("Service Charges", Money(report.ServiceCharges));
        Line("Sales Tax", Money(report.Taxes));
        Line("Delivery Charges", Money(report.DeliveryCharges));
        r.Rule(RuleStyle.Double)
            .Row(11f, FontStyle.Bold, new Cell("NET SALES", 0.5f), new Cell($"{settings.CurrencySymbol} {Money(report.NetSales)}", 0.5f, StringAlignment.Far))
            .Rule(RuleStyle.Double)
            .Text("PAYMENT BREAKDOWN", 9f, FontStyle.Bold);
        Line("Cash", Money(report.CashSales));
        Line("Card", Money(report.CardSales));
        Line("Online", Money(report.OnlineSales));
        r.Rule().Text("ORDER TYPE BREAKDOWN", 9f, FontStyle.Bold);
        Line("Dine In", Money(report.DineInSales));
        Line("Take Away", Money(report.TakeAwaySales));
        Line("Delivery", Money(report.DeliverySales));

        r.Rule(RuleStyle.Solid).Text("CASH DRAWER", 9f, FontStyle.Bold);
        Line("Opening Balance", Money(report.OpeningBalance));
        Line("+ Cash Sales", Money(report.CashSales));
        Line("Expected Cash", Money(report.ExpectedCash), FontStyle.Bold);

        if (report.CountedCash is decimal counted)
        {
            if (report.CashCount.Count > 0)
            {
                r.Rule()
                    .Row(8.5f, FontStyle.Bold, new Cell("Denomination", 0.4f), new Cell("Count", 0.25f, StringAlignment.Center), new Cell("Amount", 0.35f, StringAlignment.Far));
                foreach (var line in report.CashCount)
                    r.Row(8.5f, FontStyle.Regular, new Cell(Money(line.Denomination), 0.4f), new Cell(line.Count.ToString(), 0.25f, StringAlignment.Center), new Cell(Money(line.Amount), 0.35f, StringAlignment.Far));
                r.Rule();
            }
            Line("Counted Cash", Money(counted), FontStyle.Bold);

            var variance = report.CashVariance ?? 0;
            var label = variance == 0 ? "BALANCED" : variance < 0 ? "SHORT" : "OVER";
            r.Rule(RuleStyle.Double)
                .Row(10.5f, FontStyle.Bold, new Cell($"CASH {label}:", 0.55f), new Cell($"{settings.CurrencySymbol} {Money(Math.Abs(variance))}", 0.45f, StringAlignment.Far))
                .Rule(RuleStyle.Double);
        }

        if (!string.IsNullOrWhiteSpace(report.OpeningNotes)) r.Text($"Opening Note: {report.OpeningNotes}", 8f);
        if (!string.IsNullOrWhiteSpace(report.ClosingNotes)) r.Text($"Closing Note: {report.ClosingNotes}", 8f);

        if (report.IsClosed)
        {
            r.Space(24)
                .Row(8.5f, FontStyle.Regular, new Cell("____________", 0.5f, StringAlignment.Center), new Cell("____________", 0.5f, StringAlignment.Center))
                .Row(8f, FontStyle.Regular, new Cell("Cashier", 0.5f, StringAlignment.Center), new Cell("Manager", 0.5f, StringAlignment.Center))
                .Space(4);
        }

        return r.Rule(RuleStyle.Double)
            .Center($"Printed By: {printedBy}  |  {DateTime.Now:dd-MMM-yyyy hh:mm tt}", 7f)
            .Center("Software by ServDyne POS", 7f)
            .Space(10);
    }
}
