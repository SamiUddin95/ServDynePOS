using Afsara.Application.Common;
using Afsara.Application.Common.Interfaces;
using Afsara.Application.Days;
using Afsara.Application.Orders;
using Afsara.Application.Reports;
using Afsara.Domain.Entities;
using Afsara.Domain.Enums;
using Afsara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Afsara.Tests;

public class DaySalesReportTests
{
    private sealed class InMemoryFactory : IApplicationDbContextFactory
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public InMemoryFactory(string name) =>
            _options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options;

        public IApplicationDbContext Create() => new AppDbContext(_options);
    }

    private readonly InMemoryFactory _factory = new(Guid.NewGuid().ToString());
    private readonly UserSession _session = new();
    private readonly BusinessDayService _days;
    private readonly OrderService _orders;
    private readonly ReportService _reports;
    private readonly MenuItem _tikka;
    private readonly MenuItem _naan;
    private readonly MenuItem _cola;

    public DaySalesReportTests()
    {
        using var db = (AppDbContext)_factory.Create();
        var admin = new User { Username = "admin", FullName = "Admin", PasswordHash = "x", Role = UserRole.Admin };
        var bbq = new Category { Name = "BBQ", KitchenSection = "BBQ" };
        var drinks = new Category { Name = "Drinks" };
        _tikka = new MenuItem { Name = "Chicken Tikka", Price = 1000, Category = bbq };
        _naan = new MenuItem { Name = "Naan", Price = 50, Category = bbq };
        _cola = new MenuItem { Name = "Cola", Price = 150, Category = drinks };
        db.AddRange(admin, bbq, drinks, _tikka, _naan, _cola, new RestaurantSetting { CashTaxPercent = 0, CardTaxPercent = 0 });
        db.SaveChanges();

        _session.SignIn(admin);
        _days = new BusinessDayService(_factory, _session);
        _orders = new OrderService(_factory, _session);
        _reports = new ReportService(_factory);
    }

    private static CartLine Line(MenuItem item, int qty) =>
        new() { MenuItemId = item.Id, Name = item.Name, UnitPrice = item.Price, Quantity = qty };

    private async Task<int> SellAsync(params CartLine[] lines)
    {
        var draft = new OrderDraft { OrderType = OrderType.TakeAway };
        draft.Lines.AddRange(lines);
        var id = await _orders.SaveAsync(draft);
        await _orders.CompleteAsync(id, PaymentMethod.Cash, lines.Sum(l => l.UnitPrice * l.Quantity));
        return id;
    }

    [Fact]
    public async Task Ranks_items_by_quantity_with_category_and_share()
    {
        var day = await _days.OpenAsync(0, null);
        await SellAsync(Line(_tikka, 1), Line(_naan, 4), Line(_cola, 2));
        await SellAsync(Line(_naan, 2), Line(_cola, 1));

        var report = await _reports.GetDaySalesAsync(day.Id);

        Assert.Equal(2, report.CompletedOrders);
        Assert.Equal(10, report.ItemsSold);
        Assert.Equal(1750, report.NetSales);
        Assert.Equal(875, report.AverageOrder);
        Assert.Equal(new[] { "Naan", "Cola", "Chicken Tikka" }, report.Items.Select(i => i.Item));
        Assert.Equal(new[] { 1, 2, 3 }, report.Items.Select(i => i.Rank));

        var naan = report.TopItem!;
        Assert.Equal("Naan", naan.Item);
        Assert.Equal("BBQ", naan.Category);
        Assert.Equal(6, naan.Quantity);
        Assert.Equal(2, naan.Orders);
        Assert.Equal(60, naan.QuantityShare);

        var bbq = report.Categories.First();
        Assert.Equal("BBQ", bbq.Category);
        Assert.Equal(2, bbq.Items);
        Assert.Equal(1300, bbq.Amount);

        var hour = Assert.Single(report.Hours);
        Assert.Equal(2, hour.Orders);
        Assert.Equal(10, hour.Quantity);
        Assert.Equal(1750, hour.Amount);
    }

    [Fact]
    public async Task Only_includes_completed_orders_of_the_selected_day()
    {
        var first = await _days.OpenAsync(0, null);
        await SellAsync(Line(_tikka, 3));
        var cancelled = await _orders.SaveAsync(new OrderDraft { OrderType = OrderType.TakeAway, Lines = { Line(_cola, 5) } });
        await _orders.CancelAsync(cancelled, "Customer left");
        await _days.CloseAsync(3000, null);

        var second = await _days.OpenAsync(0, null);
        await SellAsync(Line(_naan, 1));

        var report = await _reports.GetDaySalesAsync(first.Id);
        Assert.True(report.IsClosed);
        Assert.Equal(1, report.CancelledOrders);
        var item = Assert.Single(report.Items);
        Assert.Equal("Chicken Tikka", item.Item);
        Assert.Equal(3, item.Quantity);

        var live = await _reports.GetDaySalesAsync(second.Id);
        Assert.False(live.IsClosed);
        Assert.Equal("Naan", Assert.Single(live.Items).Item);
    }

    [Fact]
    public async Task Date_range_combines_every_open_close_cycle_in_the_period()
    {
        var first = await _days.OpenAsync(0, null);
        await SellAsync(Line(_tikka, 1), Line(_naan, 2));
        await _days.CloseAsync(1100, null);
        var second = await _days.OpenAsync(0, null);
        await SellAsync(Line(_naan, 3));

        var report = await _reports.GetDaySalesAsync(DateTime.Today, DateTime.Today);

        Assert.Equal(new[] { first.Id, second.Id }, report.Sessions.Select(s => s.DayId));
        Assert.True(report.Sessions[0].IsClosed);
        Assert.False(report.IsClosed);
        Assert.Equal(1100, report.Sessions[0].NetSales);
        Assert.Equal(150, report.Sessions[1].NetSales);
        Assert.Equal(2, report.CompletedOrders);
        Assert.Equal(5, report.TopItem!.Quantity);
        Assert.Equal("Naan", report.TopItem.Item);
    }

    [Fact]
    public async Task Date_range_without_business_days_is_empty()
    {
        var report = await _reports.GetDaySalesAsync(DateTime.Today.AddDays(-3), DateTime.Today.AddDays(-2));

        Assert.Empty(report.Sessions);
        Assert.Empty(report.Items);
        Assert.Equal(0, report.NetSales);
    }

    [Fact]
    public async Task Date_range_rejects_from_after_to()
    {
        await Assert.ThrowsAsync<AppException>(() => _reports.GetDaySalesAsync(DateTime.Today, DateTime.Today.AddDays(-1)));
    }

    [Fact]
    public async Task Unknown_day_throws()
    {
        await Assert.ThrowsAsync<AppException>(() => _reports.GetDaySalesAsync(999));
    }
}
