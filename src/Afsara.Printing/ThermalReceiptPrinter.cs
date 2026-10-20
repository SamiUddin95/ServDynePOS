using System.Drawing.Printing;
using System.Windows.Forms;
using Afsara.Application.Common;
using Afsara.Application.Common.Interfaces;
using Afsara.Application.Days;
using Afsara.Application.Printing;
using Afsara.Application.Reports;
using Afsara.Application.Settings;
using Afsara.Printing.Layouts;
using Afsara.Printing.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Afsara.Printing;

public class ThermalReceiptPrinter : IReceiptPrinter
{
    private readonly ISettingsService _settings;
    private readonly IUserSession _session;

    public ThermalReceiptPrinter(ISettingsService settings, IUserSession session)
    {
        _settings = settings;
        _session = session;
    }

    public async Task PrintKotAsync(KotTicket ticket)
    {
        var settings = await _settings.GetAsync();
        Print(KotLayout.Build(ticket), settings.KotPrinterName, settings.ShowPrintPreview, 1,
            $"KOT {ticket.OrderNo}-{ticket.KotNumber}", "Kitchen Order Ticket");
    }

    public async Task PrintInvoiceAsync(InvoiceDocument invoice, bool skipPreview = false)
    {
        var settings = await _settings.GetAsync();
        var copies = invoice.IsPaid && !invoice.IsDuplicate ? settings.InvoiceCopies : 1;
        Print(InvoiceLayout.Build(invoice), settings.InvoicePrinterName, settings.ShowPrintPreview && !skipPreview, copies,
            $"Invoice {invoice.InvoiceNo}", invoice.IsPaid ? "Sales Invoice" : "Guest Bill");
    }

    public async Task PrintSalesSummaryAsync(SalesReport report)
    {
        var settings = await _settings.GetAsync();
        var receipt = SalesSummaryLayout.Build(report, settings, _session.CurrentUser?.FullName ?? string.Empty);
        Print(receipt, settings.InvoicePrinterName, settings.ShowPrintPreview, 1,
            $"Sales Summary {report.From:yyyyMMdd}", "Sales Summary");
    }

    public async Task PrintDaySalesAsync(DaySalesReport report)
    {
        var settings = await _settings.GetAsync();
        var receipt = DaySalesLayout.Build(report, settings, _session.CurrentUser?.FullName ?? string.Empty);
        Print(receipt, settings.InvoicePrinterName, settings.ShowPrintPreview, 1,
            $"Day Sales {report.From:yyyyMMdd}", "Day Sales Report");
    }

    public async Task PrintDayReportAsync(DayReport report, bool duplicate = false)
    {
        var settings = await _settings.GetAsync();
        var receipt = DayCloseLayout.Build(report, settings, _session.CurrentUser?.FullName ?? string.Empty, duplicate);
        Print(receipt, settings.InvoicePrinterName, settings.ShowPrintPreview, 1,
            $"Day Report {report.BusinessDate:yyyyMMdd}", report.IsClosed ? "Day Close Report" : "Day Status Report");
    }

    private static void Print(Receipt receipt, string? printerName, bool preview, int copies, string documentName, string title)
    {
        using var document = new ReceiptPrintDocument(receipt, documentName);

        if (!string.IsNullOrWhiteSpace(printerName))
        {
            document.PrinterSettings.PrinterName = printerName;
            if (!document.PrinterSettings.IsValid)
                throw new InvalidOperationException($"Printer '{printerName}' was not found. Update the printer in Settings.");
        }

        document.PrinterSettings.Copies = (short)Math.Max(1, copies);

        if (preview)
        {
            using var dialog = new PrintPreviewDialog
            {
                Document = document,
                Text = $"Print Preview - {title}",
                Width = 460,
                Height = 820,
                StartPosition = FormStartPosition.CenterScreen,
                ShowIcon = false,
                UseAntiAlias = true
            };
            dialog.PrintPreviewControl.Zoom = 1.0;
            dialog.ShowDialog();
        }
        else
        {
            document.Print();
        }
    }

    public static IReadOnlyList<string> InstalledPrinters() =>
        PrinterSettings.InstalledPrinters.Cast<string>().ToList();
}

public static class DependencyInjection
{
    public static IServiceCollection AddPrinting(this IServiceCollection services)
    {
        services.AddTransient<IReceiptPrinter, ThermalReceiptPrinter>();
        return services;
    }
}
