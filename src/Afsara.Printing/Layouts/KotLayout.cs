using System.Drawing;
using Afsara.Application.Printing;
using Afsara.Printing.Rendering;

namespace Afsara.Printing.Layouts;

public static class KotLayout
{
    public static Receipt Build(KotTicket kot)
    {
        var title = kot.IsCancellation ? "ORDER CANCELLED"
            : kot.IsAdditional ? "ADDITIONAL KOT"
            : "KITCHEN ORDER TICKET";

        var headline = kot.OrderType.ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(kot.TableName)) headline += $"  |  TABLE {kot.TableName}";

        var r = new Receipt()
            .Center(kot.RestaurantName, 10f, FontStyle.Bold)
            .Band(title, 11f)
            .Space(2)
            .Center(headline, 13f, FontStyle.Bold)
            .Space(2)
            .Row(9f, FontStyle.Bold, new Cell($"KOT #: {kot.KotNumber}", 0.5f), new Cell($"Order #: {kot.OrderNo}", 0.5f, StringAlignment.Far))
            .Pair($"Date: {kot.PrintedAt:dd-MMM-yyyy}", $"Time: {kot.PrintedAt:hh:mm tt}");

        if (!string.IsNullOrWhiteSpace(kot.WaiterName) || kot.Guests > 0)
            r.Pair($"Waiter: {kot.WaiterName ?? "-"}", $"Guests: {kot.Guests}");

        if (!string.IsNullOrWhiteSpace(kot.CustomerName))
            r.Text($"Customer: {kot.CustomerName}");

        r.Text($"Order By: {kot.CreatedBy}")
            .Rule(RuleStyle.Solid)
            .Row(9f, FontStyle.Bold, new Cell("QTY", 0.16f), new Cell("ITEM", 0.84f))
            .Rule(RuleStyle.Solid);

        var groups = kot.Lines.GroupBy(l => l.Section).ToList();
        foreach (var group in groups)
        {
            if (groups.Count > 1)
                r.Space(2).Center($"--- {group.Key} ---", 8.5f, FontStyle.Bold);

            foreach (var line in group)
            {
                var style = line.IsCancelled ? FontStyle.Bold | FontStyle.Strikeout : FontStyle.Bold;
                var name = line.IsCancelled ? $"[CANCEL] {line.Name}" : line.Name;
                r.Row(11f, style, new Cell(line.Quantity.ToString(), 0.16f), new Cell(name, 0.84f));
                if (!string.IsNullOrWhiteSpace(line.Notes))
                    r.Row(8.5f, FontStyle.Italic, new Cell(string.Empty, 0.16f), new Cell($">> {line.Notes}", 0.84f));
            }
        }

        r.Rule(RuleStyle.Solid)
            .Row(9f, FontStyle.Bold,
                new Cell($"Total Items: {kot.Lines.Count}", 0.5f),
                new Cell($"Total Qty: {kot.Lines.Sum(l => l.Quantity)}", 0.5f, StringAlignment.Far));

        if (!string.IsNullOrWhiteSpace(kot.Notes))
            r.Rule().Text($"NOTE: {kot.Notes}", 9f, FontStyle.Bold);

        return r.Rule(RuleStyle.Double)
            .Center($"Printed: {DateTime.Now:dd-MMM-yyyy hh:mm:ss tt}", 7f)
            .Space(10);
    }
}
