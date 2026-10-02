using System.Drawing;
using Afsara.Application.Reports;
using Afsara.Domain.Entities;
using Afsara.Printing.Rendering;

namespace Afsara.Printing.Layouts;

public static class SalesSummaryLayout
{
    public static Receipt Build(SalesReport report, RestaurantSetting settings, string printedBy)
    {
        string Money(decimal value) => value.ToString("N0");
        var period = report.From == report.To
            ? report.From.ToString("dd-MMM-yyyy")
            : $"{report.From:dd-MMM-yyyy} to {report.To:dd-MMM-yyyy}";

        var r = new Receipt()
            .Center(settings.RestaurantName, 13f, FontStyle.Bold)
            .Center(settings.Address, 8f)
            .Space(2)
            .Band("SALES SUMMARY REPORT", 10.5f)
            .Space(2)
            .Center(period, 9f, FontStyle.Bold)
            .Text($"Printed By: {printedBy}")
            .Rule(RuleStyle.Solid);

        void Line(string label, string value, FontStyle style = FontStyle.Regular) =>
            r.Row(9f, style, new Cell(label, 0.6f), new Cell(value, 0.4f, StringAlignment.Far));

        Line("Completed Orders", report.CompletedOrders.ToString());
        Line("Cancelled Orders", report.CancelledOrders.ToString());
        r.Rule();
        Line("Gross Sales", Money(report.GrossSales));
        Line("Discounts", $"- {Money(report.Discounts)}");
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

        if (report.Items.Count > 0)
        {
            r.Rule(RuleStyle.Solid)
                .Row(8.5f, FontStyle.Bold, new Cell("Item", 0.58f), new Cell("Qty", 0.14f, StringAlignment.Center), new Cell("Amount", 0.28f, StringAlignment.Far))
                .Rule(RuleStyle.Solid);
            foreach (var item in report.Items)
                r.Row(8.5f, FontStyle.Regular, new Cell(item.Item, 0.58f), new Cell(item.Quantity.ToString(), 0.14f, StringAlignment.Center), new Cell(Money(item.Amount), 0.28f, StringAlignment.Far));
        }

        return r.Rule(RuleStyle.Double)
            .Center($"Printed: {DateTime.Now:dd-MMM-yyyy hh:mm tt}", 7f)
            .Center("Software by ServDyne POS", 7f)
            .Space(10);
    }
}
