using Afsara.Application.Common;
using Afsara.Application.Common.Interfaces;
using Afsara.Domain.Entities;
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

public class DaySessionRow
{
    public int DayId { get; set; }
    public DateTime BusinessDate { get; set; }
    public bool IsClosed { get; set; }
    public DateTime OpenedAt { get; set; }
    public string OpenedBy { get; set; } = string.Empty;
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public int CompletedOrders { get; set; }
    public decimal NetSales { get; set; }
}

public class DaySalesReport
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<DaySessionRow> Sessions { get; set; } = new();
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int ItemsSold { get; set; }
    public decimal GrossSales { get; set; }
    public decimal NetSales { get; set; }
    public decimal AverageOrder { get; set; }
    public List<DayItemSalesRow> Items { get; set; } = new();
    public List<CategorySalesRow> Categories { get; set; } = new();
    public List<HourlySalesRow> Hours { get; set; } = new();

    public bool IsClosed => Sessions.Count > 0 && Sessions.All(s => s.IsClosed);
    public bool SpansMultipleDates => From.Date != To.Date;
    public string Period => SpansMultipleDates ? $"{From:dd-MMM-yyyy} to {To:dd-MMM-yyyy}" : From.ToString("dd-MMM-yyyy");
    public DayItemSalesRow? TopItem => Items.FirstOrDefault();
    public HourlySalesRow? PeakHour => Hours.OrderByDescending(h => h.Amount).ThenByDescending(h => h.Orders).FirstOrDefault();
}

public class DayItemSalesRow
{
    public int Rank { get; set; }
    public string Item { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int Orders { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal Amount { get; set; }
    public decimal QuantityShare { get; set; }
    public decimal AmountShare { get; set; }
    public DateTime FirstSold { get; set; }
    public DateTime LastSold { get; set; }
}

public class CategorySalesRow
{
    public string Category { get; set; } = string.Empty;
    public int Items { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountShare { get; set; }
}

public class HourlySalesRow
{
    public int Hour { get; set; }
    public int Orders { get; set; }
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public string Label => $"{new DateTime(1, 1, 1, Hour, 0, 0):hh tt} - {new DateTime(1, 1, 1, (Hour + 1) % 24, 0, 0):hh tt}";
}

public interface IReportService
{
    Task<SalesReport> GetSalesReportAsync(DateTime from, DateTime to);
    Task<SalesReport> GetSalesReportForDayAsync(int dayId);
    Task<DaySalesReport> GetDaySalesAsync(int dayId);
    Task<DaySalesReport> GetDaySalesAsync(DateTime from, DateTime to);
}

public interface IReportExporter
{
    void ExportExcel(SalesReport report, Domain.Entities.RestaurantSetting settings, string path);
    void ExportPdf(SalesReport report, Domain.Entities.RestaurantSetting settings, string path);
    void ExportDaySalesExcel(DaySalesReport report, SalesReport sales, Domain.Entities.RestaurantSetting settings, string path);
    void ExportDaySalesPdf(DaySalesReport report, SalesReport sales, Domain.Entities.RestaurantSetting settings, string path);
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

        return BuildReport(start, end, orders, items);
    }

    public async Task<SalesReport> GetSalesReportForDayAsync(int dayId)
    {
        await using var db = _factory.Create();
        var day = await db.BusinessDays.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dayId)
            ?? throw new AppException("Business day not found.");

        var orders = await db.Orders.AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.CreatedBy)
            .Where(o => o.BusinessDayId == dayId && o.Status != OrderStatus.Open)
            .OrderByDescending(o => o.Id)
            .ToListAsync();

        var items = await db.OrderItems.AsNoTracking()
            .Where(i => i.Order!.Status == OrderStatus.Completed
                        && i.Order.BusinessDayId == dayId
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

        return BuildReport(day.BusinessDate, day.BusinessDate, orders, items);
    }

    public async Task<DaySalesReport> GetDaySalesAsync(int dayId)
    {
        await using var db = _factory.Create();
        var day = await db.BusinessDays.AsNoTracking()
            .Include(d => d.OpenedBy)
            .Include(d => d.ClosedBy)
            .FirstOrDefaultAsync(d => d.Id == dayId)
            ?? throw new AppException("Business day not found.");
        return await BuildDaySalesAsync(db, day.BusinessDate, day.BusinessDate, new List<BusinessDay> { day });
    }

    public async Task<DaySalesReport> GetDaySalesAsync(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date;
        if (start > end) throw new AppException("'From' date cannot be after 'To' date.");

        await using var db = _factory.Create();
        var days = await db.BusinessDays.AsNoTracking()
            .Include(d => d.OpenedBy)
            .Include(d => d.ClosedBy)
            .Where(d => d.BusinessDate >= start && d.BusinessDate <= end)
            .OrderBy(d => d.OpenedAt)
            .ToListAsync();
        return await BuildDaySalesAsync(db, start, end, days);
    }

    private static async Task<DaySalesReport> BuildDaySalesAsync(IApplicationDbContext db, DateTime from, DateTime to, List<BusinessDay> days)
    {
        var dayIds = days.Select(d => d.Id).ToList();

        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.BusinessDayId != null && dayIds.Contains(o.BusinessDayId.Value) && o.Status != OrderStatus.Open)
            .Select(o => new { o.Id, o.BusinessDayId, o.Status, o.SubTotal, o.GrandTotal, SoldAt = o.CompletedAt ?? o.CreatedAt })
            .ToListAsync();
        var completed = orders.Where(o => o.Status == OrderStatus.Completed).ToList();
        var soldAt = completed.ToDictionary(o => o.Id, o => o.SoldAt);

        var lines = await db.OrderItems.AsNoTracking()
            .Where(i => i.Order!.BusinessDayId != null && dayIds.Contains(i.Order.BusinessDayId.Value)
                        && i.Order.Status == OrderStatus.Completed && i.Quantity > 0)
            .Select(i => new { i.OrderId, i.ItemName, Category = i.MenuItem!.Category!.Name, i.UnitPrice, i.Quantity })
            .ToListAsync();

        var totalQty = lines.Sum(l => l.Quantity);
        var totalAmount = lines.Sum(l => l.UnitPrice * l.Quantity);
        static decimal Share(decimal part, decimal total) => total == 0 ? 0 : Math.Round(part * 100m / total, 1);

        var items = lines
            .GroupBy(l => l.ItemName)
            .Select(g =>
            {
                var qty = g.Sum(x => x.Quantity);
                var amount = g.Sum(x => x.UnitPrice * x.Quantity);
                var times = g.Select(x => soldAt[x.OrderId]).ToList();
                return new DayItemSalesRow
                {
                    Item = g.Key,
                    Category = g.Select(x => x.Category).FirstOrDefault(c => !string.IsNullOrEmpty(c)) ?? "-",
                    Quantity = qty,
                    Orders = g.Select(x => x.OrderId).Distinct().Count(),
                    AveragePrice = qty == 0 ? 0 : Math.Round(amount / qty, 0),
                    Amount = amount,
                    QuantityShare = Share(qty, totalQty),
                    AmountShare = Share(amount, totalAmount),
                    FirstSold = times.Min(),
                    LastSold = times.Max()
                };
            })
            .OrderByDescending(r => r.Quantity)
            .ThenByDescending(r => r.Amount)
            .ThenBy(r => r.Item)
            .ToList();
        for (var i = 0; i < items.Count; i++) items[i].Rank = i + 1;

        var categories = items
            .GroupBy(i => i.Category)
            .Select(g => new CategorySalesRow
            {
                Category = g.Key,
                Items = g.Count(),
                Quantity = g.Sum(x => x.Quantity),
                Amount = g.Sum(x => x.Amount),
                AmountShare = Share(g.Sum(x => x.Amount), totalAmount)
            })
            .OrderByDescending(c => c.Amount)
            .ToList();

        var qtyByOrder = lines.GroupBy(l => l.OrderId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var hours = completed
            .GroupBy(o => o.SoldAt.Hour)
            .Select(g => new HourlySalesRow
            {
                Hour = g.Key,
                Orders = g.Count(),
                Quantity = g.Sum(o => qtyByOrder.GetValueOrDefault(o.Id)),
                Amount = g.Sum(o => o.GrandTotal)
            })
            .OrderBy(h => h.Hour)
            .ToList();

        var netSales = completed.Sum(o => o.GrandTotal);
        return new DaySalesReport
        {
            From = from,
            To = to,
            Sessions = days.Select(d => new DaySessionRow
            {
                DayId = d.Id,
                BusinessDate = d.BusinessDate,
                IsClosed = !d.IsOpen,
                OpenedAt = d.OpenedAt,
                OpenedBy = d.OpenedBy?.FullName ?? string.Empty,
                ClosedAt = d.ClosedAt,
                ClosedBy = d.ClosedBy?.FullName,
                CompletedOrders = completed.Count(o => o.BusinessDayId == d.Id),
                NetSales = completed.Where(o => o.BusinessDayId == d.Id).Sum(o => o.GrandTotal)
            }).ToList(),
            CompletedOrders = completed.Count,
            CancelledOrders = orders.Count(o => o.Status == OrderStatus.Cancelled),
            ItemsSold = totalQty,
            GrossSales = completed.Sum(o => o.SubTotal),
            NetSales = netSales,
            AverageOrder = completed.Count == 0 ? 0 : Math.Round(netSales / completed.Count, 0),
            Items = items,
            Categories = categories,
            Hours = hours
        };
    }

    private static SalesReport BuildReport(DateTime from, DateTime to, List<Order> orders, List<ItemSalesRow> items)
    {
        var completed = orders.Where(o => o.Status == OrderStatus.Completed).ToList();

        return new SalesReport
        {
            From = from,
            To = to,
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
