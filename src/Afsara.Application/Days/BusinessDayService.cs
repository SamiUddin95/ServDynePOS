using System.Globalization;
using Afsara.Application.Common;
using Afsara.Application.Common.Interfaces;
using Afsara.Domain.Entities;
using Afsara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Afsara.Application.Days;

public sealed record CashCountLine(decimal Denomination, int Count)
{
    public decimal Amount => Denomination * Count;
}

public class DayReport
{
    public int DayId { get; set; }
    public DateTime BusinessDate { get; set; }
    public bool IsClosed { get; set; }
    public DateTime OpenedAt { get; set; }
    public string OpenedBy { get; set; } = string.Empty;
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public string? OpeningNotes { get; set; }
    public string? ClosingNotes { get; set; }

    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int RunningOrders { get; set; }
    public decimal RunningAmount { get; set; }

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

    public decimal OpeningBalance { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? CashVariance { get; set; }
    public List<CashCountLine> CashCount { get; set; } = new();
}

public interface IBusinessDayService
{
    Task<BusinessDay?> GetCurrentAsync();
    Task<BusinessDay?> GetLastClosedAsync();
    Task<BusinessDay> OpenAsync(decimal openingBalance, string? notes);
    Task<DayReport> GetCurrentReportAsync();
    Task<DayReport> CloseAsync(decimal countedCash, string? notes, IReadOnlyList<CashCountLine>? cashCount = null);
    Task<List<DayReport>> GetClosedDaysAsync(DateTime from, DateTime to);
    Task<List<DayReport>> GetDaysAsync(DateTime from, DateTime to);
    Task<DayReport> GetReportAsync(int dayId);
}

public class BusinessDayService : IBusinessDayService
{
    private readonly IApplicationDbContextFactory _factory;
    private readonly IUserSession _session;

    public BusinessDayService(IApplicationDbContextFactory factory, IUserSession session)
    {
        _factory = factory;
        _session = session;
    }

    public async Task<BusinessDay?> GetCurrentAsync()
    {
        await using var db = _factory.Create();
        return await db.BusinessDays.AsNoTracking()
            .Include(d => d.OpenedBy)
            .FirstOrDefaultAsync(d => d.Status == BusinessDayStatus.Open);
    }

    public async Task<BusinessDay?> GetLastClosedAsync()
    {
        await using var db = _factory.Create();
        return await db.BusinessDays.AsNoTracking()
            .Where(d => d.Status == BusinessDayStatus.Closed)
            .OrderByDescending(d => d.ClosedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<BusinessDay> OpenAsync(decimal openingBalance, string? notes)
    {
        var user = _session.RequireUser();
        if (openingBalance < 0)
            throw new AppException("Opening balance cannot be negative.");

        await using var db = _factory.Create();
        var open = await db.BusinessDays.AsNoTracking().FirstOrDefaultAsync(d => d.Status == BusinessDayStatus.Open);
        if (open is not null)
            throw new AppException($"Business day {open.BusinessDate:dd-MMM-yyyy} is already open.");

        var day = new BusinessDay
        {
            BusinessDate = DateTime.Today,
            OpeningBalance = openingBalance,
            OpeningNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            OpenedAt = DateTime.Now,
            OpenedByUserId = user.Id,
            ExpectedCash = openingBalance
        };
        db.BusinessDays.Add(day);
        await db.SaveChangesAsync();
        day.OpenedBy = user;
        return day;
    }

    public async Task<DayReport> GetCurrentReportAsync()
    {
        await using var db = _factory.Create();
        var day = await db.BusinessDays.AsNoTracking()
            .Include(d => d.OpenedBy)
            .FirstOrDefaultAsync(d => d.Status == BusinessDayStatus.Open)
            ?? throw new AppException("There is no open business day.");

        await ApplyTotalsAsync(db, day);
        var report = ToReport(day);
        await FillRunningAsync(db, report);
        return report;
    }

    public async Task<DayReport> CloseAsync(decimal countedCash, string? notes, IReadOnlyList<CashCountLine>? cashCount = null)
    {
        var user = _session.RequireUser();
        if (!user.CanManage)
            throw new AppException("Only a Manager or Admin can close the business day.");
        if (countedCash < 0)
            throw new AppException("Counted cash cannot be negative.");

        await using var db = _factory.Create();
        var day = await db.BusinessDays
            .Include(d => d.OpenedBy)
            .FirstOrDefaultAsync(d => d.Status == BusinessDayStatus.Open)
            ?? throw new AppException("There is no open business day to close.");

        var running = await db.Orders.Where(o => o.Status == OrderStatus.Open).Select(o => o.GrandTotal).ToListAsync();
        if (running.Count > 0)
            throw new AppException($"There are {running.Count} running order(s) totalling {running.Sum():N0}.\nSettle or cancel all running orders before closing the day.");

        await ApplyTotalsAsync(db, day);

        var variance = countedCash - day.ExpectedCash;
        notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (variance != 0 && notes is null)
            throw new AppException($"{(variance < 0 ? "Cash Short" : "Cash Over")}: {Math.Abs(variance):N0}\nPlease enter a reason in the closing notes.");

        day.CountedCash = countedCash;
        day.CashVariance = variance;
        day.CashCount = SerializeCount(cashCount);
        day.ClosingNotes = notes;
        day.Status = BusinessDayStatus.Closed;
        day.ClosedAt = DateTime.Now;
        day.ClosedByUserId = user.Id;
        day.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();

        day.ClosedBy = user;
        return ToReport(day);
    }

    public async Task<List<DayReport>> GetClosedDaysAsync(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date;
        await using var db = _factory.Create();
        var days = await db.BusinessDays.AsNoTracking()
            .Include(d => d.OpenedBy)
            .Include(d => d.ClosedBy)
            .Where(d => d.Status == BusinessDayStatus.Closed && d.BusinessDate >= start && d.BusinessDate <= end)
            .OrderByDescending(d => d.ClosedAt)
            .ToListAsync();
        return days.Select(ToReport).ToList();
    }

    public async Task<List<DayReport>> GetDaysAsync(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date;
        await using var db = _factory.Create();
        var days = await db.BusinessDays.AsNoTracking()
            .Include(d => d.OpenedBy)
            .Include(d => d.ClosedBy)
            .Where(d => d.BusinessDate >= start && d.BusinessDate <= end)
            .OrderByDescending(d => d.BusinessDate)
            .ToListAsync();
        foreach (var day in days.Where(d => d.IsOpen))
            await ApplyTotalsAsync(db, day);
        return days.Select(ToReport).ToList();
    }

    public async Task<DayReport> GetReportAsync(int dayId)
    {
        await using var db = _factory.Create();
        var day = await db.BusinessDays.AsNoTracking()
            .Include(d => d.OpenedBy)
            .Include(d => d.ClosedBy)
            .FirstOrDefaultAsync(d => d.Id == dayId)
            ?? throw new AppException("Business day not found.");

        if (day.IsOpen) await ApplyTotalsAsync(db, day);
        var report = ToReport(day);
        if (day.IsOpen) await FillRunningAsync(db, report);
        return report;
    }

    internal static async Task<BusinessDay> RequireOpenAsync(IApplicationDbContext db) =>
        await db.BusinessDays.AsNoTracking().FirstOrDefaultAsync(d => d.Status == BusinessDayStatus.Open)
        ?? throw new AppException("The business day is not open.\nPlease open the day with the opening cash balance before taking orders.");

    private static async Task ApplyTotalsAsync(IApplicationDbContext db, BusinessDay day)
    {
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.BusinessDayId == day.Id && o.Status != OrderStatus.Open)
            .ToListAsync();
        var completed = orders.Where(o => o.Status == OrderStatus.Completed).ToList();

        day.CompletedOrders = completed.Count;
        day.CancelledOrders = orders.Count - completed.Count;
        day.GrossSales = completed.Sum(o => o.SubTotal);
        day.Discounts = completed.Sum(o => o.DiscountAmount);
        day.ServiceCharges = completed.Sum(o => o.ServiceCharge);
        day.Taxes = completed.Sum(o => o.TaxAmount);
        day.DeliveryCharges = completed.Sum(o => o.DeliveryCharge);
        day.NetSales = completed.Sum(o => o.GrandTotal);
        day.CashSales = completed.Where(o => o.PaymentMethod == PaymentMethod.Cash).Sum(o => o.GrandTotal);
        day.CardSales = completed.Where(o => o.PaymentMethod == PaymentMethod.Card).Sum(o => o.GrandTotal);
        day.OnlineSales = completed.Where(o => o.PaymentMethod == PaymentMethod.Online).Sum(o => o.GrandTotal);
        day.DineInSales = completed.Where(o => o.OrderType == OrderType.DineIn).Sum(o => o.GrandTotal);
        day.TakeAwaySales = completed.Where(o => o.OrderType == OrderType.TakeAway).Sum(o => o.GrandTotal);
        day.DeliverySales = completed.Where(o => o.OrderType == OrderType.Delivery).Sum(o => o.GrandTotal);
        day.ExpectedCash = day.OpeningBalance + day.CashSales;
    }

    private static async Task FillRunningAsync(IApplicationDbContext db, DayReport report)
    {
        var running = await db.Orders.AsNoTracking().Where(o => o.Status == OrderStatus.Open).Select(o => o.GrandTotal).ToListAsync();
        report.RunningOrders = running.Count;
        report.RunningAmount = running.Sum();
    }

    private static DayReport ToReport(BusinessDay day) => new()
    {
        DayId = day.Id,
        BusinessDate = day.BusinessDate,
        IsClosed = !day.IsOpen,
        OpenedAt = day.OpenedAt,
        OpenedBy = day.OpenedBy?.FullName ?? string.Empty,
        ClosedAt = day.ClosedAt,
        ClosedBy = day.ClosedBy?.FullName,
        OpeningNotes = day.OpeningNotes,
        ClosingNotes = day.ClosingNotes,
        CompletedOrders = day.CompletedOrders,
        CancelledOrders = day.CancelledOrders,
        GrossSales = day.GrossSales,
        Discounts = day.Discounts,
        ServiceCharges = day.ServiceCharges,
        Taxes = day.Taxes,
        DeliveryCharges = day.DeliveryCharges,
        NetSales = day.NetSales,
        CashSales = day.CashSales,
        CardSales = day.CardSales,
        OnlineSales = day.OnlineSales,
        DineInSales = day.DineInSales,
        TakeAwaySales = day.TakeAwaySales,
        DeliverySales = day.DeliverySales,
        OpeningBalance = day.OpeningBalance,
        ExpectedCash = day.ExpectedCash,
        CountedCash = day.IsOpen ? null : day.CountedCash,
        CashVariance = day.IsOpen ? null : day.CashVariance,
        CashCount = ParseCount(day.CashCount)
    };

    private static string? SerializeCount(IReadOnlyList<CashCountLine>? lines)
    {
        var used = lines?.Where(l => l.Count > 0).ToList();
        return used is { Count: > 0 }
            ? string.Join(';', used.Select(l => $"{l.Denomination.ToString(CultureInfo.InvariantCulture)}x{l.Count}"))
            : null;
    }

    private static List<CashCountLine> ParseCount(string? value)
    {
        var lines = new List<CashCountLine>();
        if (string.IsNullOrWhiteSpace(value)) return lines;
        foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('x');
            if (pieces.Length == 2
                && decimal.TryParse(pieces[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var denomination)
                && int.TryParse(pieces[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
                lines.Add(new CashCountLine(denomination, count));
        }
        return lines;
    }
}
