using Afsara.Application.Reports;
using Afsara.Domain.Entities;
using Afsara.Infrastructure.Reports;
using ClosedXML.Excel;
using Xunit;

namespace Afsara.Tests;

public class ReportExporterTests
{
    private static SalesReport SampleReport() => new()
    {
        From = new DateTime(2026, 9, 1),
        To = new DateTime(2026, 9, 28),
        CompletedOrders = 2,
        CancelledOrders = 1,
        GrossSales = 2000,
        Taxes = 320,
        NetSales = 2320,
        CashSales = 2320,
        DineInSales = 2320,
        Orders =
        {
            new ReportOrderRow { Id = 1, Invoice = "INV-1", OrderNo = "0001", Date = DateTime.Now, Type = "Dine In", Status = "Completed", Payment = "Cash", Total = 1160, Cashier = "Admin" },
            new ReportOrderRow { Id = 2, Invoice = "INV-2", OrderNo = "0002", Date = DateTime.Now, Type = "Take Away", Status = "Completed", Payment = "Cash", Total = 1160, Cashier = "Admin" },
            new ReportOrderRow { Id = 3, OrderNo = "0003", Date = DateTime.Now, Type = "Dine In", Status = "Cancelled", Cashier = "Admin" }
        },
        Items = { new ItemSalesRow { Item = "Chicken Tikka", Quantity = 3, Amount = 1950 }, new ItemSalesRow { Item = "Naan", Quantity = 1, Amount = 50 } }
    };

    [Fact]
    public void Excel_export_writes_summary_orders_and_items_sheets()
    {
        var path = Path.Combine(Path.GetTempPath(), $"afsara_{Guid.NewGuid():N}.xlsx");
        try
        {
            new ReportExporter().ExportExcel(SampleReport(), new RestaurantSetting(), path);

            using var workbook = new XLWorkbook(path);
            Assert.Equal(new[] { "Summary", "Orders", "Item Sales" }, workbook.Worksheets.Select(w => w.Name));
            Assert.Equal(2320, workbook.Worksheet("Orders").Cell(5, 8).GetValue<decimal>());
            Assert.Equal("Chicken Tikka", workbook.Worksheet("Item Sales").Cell(2, 2).GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Pdf_export_writes_valid_pdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"afsara_{Guid.NewGuid():N}.pdf");
        try
        {
            new ReportExporter().ExportPdf(SampleReport(), new RestaurantSetting(), path);

            var header = new byte[5];
            using (var stream = File.OpenRead(path)) stream.ReadExactly(header);
            Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(header));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
