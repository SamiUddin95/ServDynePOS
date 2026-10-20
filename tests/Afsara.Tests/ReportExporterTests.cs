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

    private static DaySalesReport SampleDaySales() => new()
    {
        From = new DateTime(2026, 9, 1),
        To = new DateTime(2026, 9, 1),
        Sessions = { new DaySessionRow { DayId = 1, BusinessDate = new DateTime(2026, 9, 1), IsClosed = true, OpenedAt = new DateTime(2026, 9, 1, 10, 0, 0), OpenedBy = "Admin", ClosedAt = new DateTime(2026, 9, 1, 23, 0, 0), ClosedBy = "Admin", CompletedOrders = 2, NetSales = 2320 } },
        CompletedOrders = 2,
        ItemsSold = 4,
        NetSales = 2320,
        AverageOrder = 1160,
        Items =
        {
            new DayItemSalesRow { Rank = 1, Item = "Chicken Tikka", Category = "BBQ", Quantity = 3, Orders = 2, AveragePrice = 650, Amount = 1950, QuantityShare = 75, AmountShare = 97.5m, FirstSold = DateTime.Now, LastSold = DateTime.Now },
            new DayItemSalesRow { Rank = 2, Item = "Naan", Category = "Bread", Quantity = 1, Orders = 1, AveragePrice = 50, Amount = 50, QuantityShare = 25, AmountShare = 2.5m, FirstSold = DateTime.Now, LastSold = DateTime.Now }
        },
        Categories = { new CategorySalesRow { Category = "BBQ", Items = 1, Quantity = 3, Amount = 1950, AmountShare = 97.5m } },
        Hours = { new HourlySalesRow { Hour = 13, Orders = 2, Quantity = 4, Amount = 2320 } }
    };

    [Fact]
    public void Day_sales_excel_export_writes_all_sheets()
    {
        var path = Path.Combine(Path.GetTempPath(), $"afsara_{Guid.NewGuid():N}.xlsx");
        try
        {
            new ReportExporter().ExportDaySalesExcel(SampleDaySales(), SampleReport(), new RestaurantSetting(), path);

            using var workbook = new XLWorkbook(path);
            Assert.Equal(new[] { "Summary", "Item Sales", "Categories", "Hourly", "Orders" }, workbook.Worksheets.Select(w => w.Name));
            Assert.Equal("Chicken Tikka", workbook.Worksheet("Item Sales").Cell(2, 2).GetString());
            Assert.Equal("Admin", workbook.Worksheet("Summary").Cell(6, 3).GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Day_sales_pdf_export_writes_valid_pdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"afsara_{Guid.NewGuid():N}.pdf");
        try
        {
            new ReportExporter().ExportDaySalesPdf(SampleDaySales(), SampleReport(), new RestaurantSetting(), path);

            var header = new byte[5];
            using (var stream = File.OpenRead(path)) stream.ReadExactly(header);
            Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(header));
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
