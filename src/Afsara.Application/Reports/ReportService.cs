using Afsara.Application.Common.Interfaces;
using Afsara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Afsara.Application.Reports;

public class SalesReport
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public decimal GrossSales { get; set; }
    public decimal Discounts { get; set; }
    public decimal ServiceCharges { get; set; }
    public decimal Taxes { get; set; }
    public decimal DeliveryCharges { get; set; }
    public decimal NetSales { get; set; }
    public decimal CashSales { get; set; }
    public decimal CardSales { get; set; }
    public decimal OnlineSales { get; set; }
    public decimal DineInSales { get; set; }
    public decimal TakeAwaySales { get; set; }
    public decimal DeliverySales { get; set; }
    public List<ReportOrderRow> Orders { get; set; } = new();
    public List<ItemSalesRow> Items { get; set; } = new();
}

public class ReportOrderRow
{
    public int Id { get; set; }
    public string Invoice { get; set; } = string.Empty;
    public string OrderNo { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Table { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Payment { get; set; }
    public decimal Total { get; set; }
    public string Cashier { get; set; } = string.Empty;
}

public class ItemSalesRow
{
    public string Item { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
}

public interface IReportService
{
    Task<SalesReport> GetSalesReportAsync(DateTime from, DateTime to);
}

public interface IReportExporter
{
    void ExportExcel(SalesReport report, Domain.Entities.RestaurantSetting settings, string path);
    void ExportPdf(SalesReport report, Domain.Entities.RestaurantSetting settings, string path);
}

public class ReportService : IReportService
{
    private readonly IApplicationDbContextFactory _factory;

    public ReportService(IApplicationDbContextFactory factory) => _factory = factory;

    public async Task<SalesReport> GetSalesReportAsync(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date;

        await using var db = _factory.Create();
        var orders = await db.Orders.AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.CreatedBy)
            .Where(o => o.BusinessDate >= start && o.BusinessDate <= end && o.Status != OrderStatus.Open)
            .OrderByDescending(o => o.Id)
            .ToListAsync();

        var completed = orders.Where(o => o.Status == OrderStatus.Completed).ToList();

        var items = await db.OrderItems.AsNoTracking()
            .Where(i => i.Order!.Status == OrderStatus.Completed
                        && i.Order.BusinessDate >= start && i.Order.BusinessDate <= end
                        && i.Quantity > 0)
            .GroupBy(i => i.ItemName)
            .Select(g => new ItemSalesRow
            {
                Item = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                Amount = g.Sum(x => x.UnitPrice * x.Quantity)
            })
            .OrderByDescending(r => r.Amount)
            .ToListAsync();

        return new SalesReport
        {
            From = start,
            To = end,
            CompletedOrders = completed.Count,
            CancelledOrders = orders.Count(o => o.Status == OrderStatus.Cancelled),
            GrossSales = completed.Sum(o => o.SubTotal),
            Discounts = completed.Sum(o => o.DiscountAmount),
            ServiceCharges = completed.Sum(o => o.ServiceCharge),
            Taxes = completed.Sum(o => o.TaxAmount),
            DeliveryCharges = completed.Sum(o => o.DeliveryCharge),
            NetSales = completed.Sum(o => o.GrandTotal),
            CashSales = completed.Where(o => o.PaymentMethod == PaymentMethod.Cash).Sum(o => o.GrandTotal),
            CardSales = completed.Where(o => o.PaymentMethod == PaymentMethod.Card).Sum(o => o.GrandTotal),
            OnlineSales = completed.Where(o => o.PaymentMethod == PaymentMethod.Online).Sum(o => o.GrandTotal),
            DineInSales = completed.Where(o => o.OrderType == OrderType.DineIn).Sum(o => o.GrandTotal),
            TakeAwaySales = completed.Where(o => o.OrderType == OrderType.TakeAway).Sum(o => o.GrandTotal),
            DeliverySales = completed.Where(o => o.OrderType == OrderType.Delivery).Sum(o => o.GrandTotal),
            Orders = orders.Select(o => new ReportOrderRow
            {
                Id = o.Id,
                Invoice = o.InvoiceNo,
                OrderNo = o.OrderNo,
                Date = o.CompletedAt ?? o.CreatedAt,
                Type = Common.EnumText.ToDisplay(o.OrderType),
                Table = o.Table?.Name,
                Status = o.Status.ToString(),
                Payment = o.PaymentMethod.HasValue ? Common.EnumText.ToDisplay(o.PaymentMethod.Value) : null,
                Total = o.Status == OrderStatus.Completed ? o.GrandTotal : 0,
                Cashier = o.CreatedBy?.FullName ?? string.Empty
            }).ToList(),
            Items = items
        };
    }
}
