using System.Drawing;
using Afsara.Application.Printing;
using Afsara.Printing.Rendering;

namespace Afsara.Printing.Layouts;

public static class InvoiceLayout
{
    public static Receipt Build(InvoiceDocument inv)
    {
        string Money(decimal value) => value.ToString("N0");

        var title = inv.IsPaid ? "SALES INVOICE" : "GUEST BILL";
        if (inv.IsDuplicate) title += " (DUPLICATE)";

        var r = new Receipt()
            .Image(inv.LogoPath)
            .Center(inv.RestaurantName, 14f, FontStyle.Bold)
            .Center(inv.Address, 8f)
            .Center(string.IsNullOrWhiteSpace(inv.Phone) ? string.Empty : $"Ph: {inv.Phone}", 8f)
            .Center(string.IsNullOrWhiteSpace(inv.TaxNumber) ? string.Empty : $"NTN / STRN: {inv.TaxNumber}", 8f)
            .Space(2)
            .Band(title, 10.5f)
            .Space(2)
            .Row(8.5f, FontStyle.Bold, new Cell($"Invoice #: {inv.InvoiceNo}", 0.6f), new Cell($"Order #: {inv.OrderNo}", 0.4f, StringAlignment.Far))
            .Pair($"Date: {inv.Date:dd-MMM-yyyy}", $"Time: {inv.Date:hh:mm tt}")
            .Pair($"Type: {inv.OrderType}", string.IsNullOrWhiteSpace(inv.TableName) ? string.Empty : $"Table: {inv.TableName}");

        if (!string.IsNullOrWhiteSpace(inv.WaiterName))
            r.Pair($"Waiter: {inv.WaiterName}", $"Guests: {inv.Guests}");

        r.Text($"Cashier: {inv.Cashier}");

        if (!string.IsNullOrWhiteSpace(inv.CustomerName) || !string.IsNullOrWhiteSpace(inv.CustomerPhone))
        {
            r.Rule()
                .Text($"Customer: {inv.CustomerName}", 8.5f, FontStyle.Bold)
                .Text(string.IsNullOrWhiteSpace(inv.CustomerPhone) ? string.Empty : $"Phone: {inv.CustomerPhone}")
                .Text(string.IsNullOrWhiteSpace(inv.DeliveryAddress) ? string.Empty : $"Address: {inv.DeliveryAddress}");
        }

        r.Rule(RuleStyle.Solid)
            .Row(8.5f, FontStyle.Bold,
                new Cell("Item", 0.46f),
                new Cell("Qty", 0.12f, StringAlignment.Center),
                new Cell("Rate", 0.20f, StringAlignment.Far),
                new Cell("Amount", 0.22f, StringAlignment.Far))
            .Rule(RuleStyle.Solid);

        foreach (var line in inv.Lines)
        {
            r.Row(8.5f, FontStyle.Regular,
                new Cell(line.Name, 0.46f),
                new Cell(line.Quantity.ToString(), 0.12f, StringAlignment.Center),
                new Cell(Money(line.Rate), 0.20f, StringAlignment.Far),
                new Cell(Money(line.Amount), 0.22f, StringAlignment.Far));
        }

        r.Rule(RuleStyle.Solid)
            .Row(8.5f, FontStyle.Regular,
                new Cell($"Items: {inv.Lines.Count}", 0.5f),
                new Cell($"Total Qty: {inv.Lines.Sum(l => l.Quantity)}", 0.5f, StringAlignment.Far))
            .Rule();

        void Total(string label, string value, float size = 9f, FontStyle style = FontStyle.Regular) =>
            r.Row(size, style, new Cell(label, 0.62f, StringAlignment.Far), new Cell(value, 0.38f, StringAlignment.Far));

        Total("Sub Total:", Money(inv.SubTotal));
        if (inv.Discount > 0)
            Total(inv.DiscountPercent > 0 ? $"Discount ({inv.DiscountPercent:0.##}%):" : "Discount:", $"- {Money(inv.Discount)}");
        if (inv.ServiceCharge > 0)
            Total($"Service Charges ({inv.ServiceChargePercent:0.##}%):", Money(inv.ServiceCharge));
        if (inv.TaxPercent > 0)
            Total($"Sales Tax ({inv.TaxPercent:0.##}%):", Money(inv.Tax));
        if (inv.DeliveryCharge > 0)
            Total("Delivery Charges:", Money(inv.DeliveryCharge));

        r.Rule(RuleStyle.Double)
            .Row(12f, FontStyle.Bold, new Cell("GRAND TOTAL", 0.5f), new Cell($"{inv.Currency} {Money(inv.GrandTotal)}", 0.5f, StringAlignment.Far))
            .Rule(RuleStyle.Double);

        if (inv.IsPaid)
        {
            Total("Payment Mode:", inv.PaymentMethod ?? "-", 9f, FontStyle.Bold);
            Total("Amount Received:", Money(inv.Paid));
            Total("Change Returned:", Money(inv.Change));
            r.Rule();
        }
        else
        {
            r.Center("Tax shown is for cash payment.", 7.5f, FontStyle.Italic)
                .Center("This is not a final invoice. Please pay at the counter.", 7.5f, FontStyle.Italic)
                .Rule();
        }

        return r.Space(2)
            .Center(inv.Footer, 9f, FontStyle.Bold)
            .Space(4)
            .Center("Software by ServDyne POS", 7f)
            .Center($"Printed: {DateTime.Now:dd-MMM-yyyy hh:mm tt}", 7f)
            .Space(10);
    }
}
