using Afsara.Application.Common;
using Afsara.Application.Common.Interfaces;
using Afsara.Application.Days;
using Afsara.Application.Orders;
using Afsara.Domain.Entities;
using Afsara.Domain.Enums;
using Afsara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Afsara.Tests;

public class BusinessDayServiceTests
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
    private readonly User _admin;
    private readonly User _cashier;
    private readonly MenuItem _tikka;

    public BusinessDayServiceTests()
    {
        using var db = (AppDbContext)_factory.Create();
        _admin = new User { Username = "admin", FullName = "Admin", PasswordHash = "x", Role = UserRole.Admin };
        _cashier = new User { Username = "cashier", FullName = "Cashier", PasswordHash = "x", Role = UserRole.Cashier };
        var category = new Category { Name = "BBQ", KitchenSection = "BBQ" };
        _tikka = new MenuItem { Name = "Chicken Tikka", Price = 1000, Category = category };
        db.AddRange(_admin, _cashier, category, _tikka, new RestaurantSetting { CashTaxPercent = 0, CardTaxPercent = 0 });
        db.SaveChanges();

        _session.SignIn(_admin);
        _days = new BusinessDayService(_factory, _session);
        _orders = new OrderService(_factory, _session);
    }

    private OrderDraft TakeAway(int qty) => new()
    {
        OrderType = OrderType.TakeAway,
        Lines = { new CartLine { MenuItemId = _tikka.Id, Name = _tikka.Name, UnitPrice = _tikka.Price, Quantity = qty } }
    };

    private async Task SellAsync(int qty, PaymentMethod method)
    {
        var id = await _orders.SaveAsync(TakeAway(qty));
        await _orders.CompleteAsync(id, method, qty * _tikka.Price);
    }

    [Fact]
    public async Task Orders_cannot_be_taken_before_the_day_is_opened()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() => _orders.SaveAsync(TakeAway(1)));
        Assert.Contains("not open", ex.Message);
    }

    [Fact]
    public async Task Only_one_day_can_be_open_at_a_time()
    {
        await _days.OpenAsync(5000, null);

        await Assert.ThrowsAsync<AppException>(() => _days.OpenAsync(1000, null));
    }

    [Fact]
    public async Task Orders_are_linked_to_the_open_day_and_use_its_business_date()
    {
        var day = await _days.OpenAsync(0, null);
        var id = await _orders.SaveAsync(TakeAway(1));

        await using var db = _factory.Create();
        var order = await db.Orders.SingleAsync(o => o.Id == id);
        Assert.Equal(day.Id, order.BusinessDayId);
        Assert.Equal(day.BusinessDate, order.BusinessDate);
    }

    [Fact]
    public async Task Close_computes_expected_cash_and_variance()
    {
        await _days.OpenAsync(5000, "Float");
        await SellAsync(2, PaymentMethod.Cash);
        await SellAsync(1, PaymentMethod.Card);

        var preview = await _days.GetCurrentReportAsync();
        Assert.Equal(3000, preview.NetSales);
        Assert.Equal(2000, preview.CashSales);
        Assert.Equal(7000, preview.ExpectedCash);

        var count = new List<CashCountLine> { new(5000, 1), new(1000, 1), new(500, 1) };
        var closed = await _days.CloseAsync(6500, "Paid supplier from drawer", count);

        Assert.True(closed.IsClosed);
        Assert.Equal(-500, closed.CashVariance);
        Assert.Equal(3, closed.CashCount.Count);
        Assert.Null(await _days.GetCurrentAsync());

        var history = Assert.Single(await _days.GetClosedDaysAsync(DateTime.Today, DateTime.Today));
        Assert.Equal(6500, history.CountedCash);
        Assert.Equal(2, history.CompletedOrders);
        Assert.Equal(1000, history.CashCount.Single(l => l.Denomination == 1000).Amount);
    }

    [Fact]
    public async Task Close_with_cash_difference_requires_a_note()
    {
        await _days.OpenAsync(1000, null);

        await Assert.ThrowsAsync<AppException>(() => _days.CloseAsync(900, " "));
        var closed = await _days.CloseAsync(1000, null);
        Assert.Equal(0, closed.CashVariance);
    }

    [Fact]
    public async Task Close_is_blocked_while_orders_are_running()
    {
        await _days.OpenAsync(0, null);
        await _orders.SaveAsync(TakeAway(1));

        var ex = await Assert.ThrowsAsync<AppException>(() => _days.CloseAsync(0, null));
        Assert.Contains("running order", ex.Message);
    }

    [Fact]
    public async Task Cashier_cannot_close_the_day()
    {
        await _days.OpenAsync(0, null);
        _session.SignIn(_cashier);

        await Assert.ThrowsAsync<AppException>(() => _days.CloseAsync(0, null));
    }

    [Fact]
    public async Task New_day_can_be_opened_after_close_and_starts_fresh()
    {
        await _days.OpenAsync(1000, null);
        await SellAsync(1, PaymentMethod.Cash);
        await _days.CloseAsync(2000, null);

        await _days.OpenAsync(2000, null);
        var report = await _days.GetCurrentReportAsync();

        Assert.Equal(0, report.CompletedOrders);
        Assert.Equal(2000, report.ExpectedCash);
        Assert.Equal(2000, (await _days.GetLastClosedAsync())!.CountedCash);
    }
}
