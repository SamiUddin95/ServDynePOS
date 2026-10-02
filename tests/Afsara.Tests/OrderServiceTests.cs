using Afsara.Application.Common;
using Afsara.Application.Common.Interfaces;
using Afsara.Application.Orders;
using Afsara.Domain.Entities;
using Afsara.Domain.Enums;
using Afsara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Afsara.Tests;

public class OrderServiceTests
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
    private readonly OrderService _service;
    private readonly MenuItem _tikka;
    private readonly MenuItem _naan;
    private readonly DiningTable _table;

    public OrderServiceTests()
    {
        using var db = (AppDbContext)_factory.Create();
        var user = new User { Username = "admin", FullName = "Admin", PasswordHash = "x", Role = UserRole.Admin };
        var category = new Category { Name = "BBQ", KitchenSection = "BBQ" };
        _tikka = new MenuItem { Name = "Chicken Tikka", Price = 650, Category = category };
        _naan = new MenuItem { Name = "Naan", Price = 50, Category = category };
        _table = new DiningTable { Name = "T-1" };
        db.AddRange(user, category, _tikka, _naan, _table,
            new RestaurantSetting { CashTaxPercent = 16, CardTaxPercent = 5 });
        db.SaveChanges();

        _session.SignIn(user);
        _service = new OrderService(_factory, _session);
    }

    private OrderDraft NewDineIn(params (MenuItem Item, int Qty)[] lines) => new()
    {
        OrderType = OrderType.DineIn,
        TableId = _table.Id,
        Lines = lines.Select(l => new CartLine { MenuItemId = l.Item.Id, Name = l.Item.Name, UnitPrice = l.Item.Price, Quantity = l.Qty }).ToList()
    };

    [Fact]
    public async Task Second_kot_contains_only_newly_added_quantities()
    {
        var first = await _service.SendToKitchenAsync(NewDineIn((_tikka, 2)));
        Assert.NotNull(first.Ticket);
        Assert.Equal(1, first.Ticket!.KotNumber);
        Assert.False(first.Ticket.IsAdditional);
        Assert.Equal(2, first.Ticket.Lines.Single().Quantity);

        var draft = await _service.GetDraftAsync(first.OrderId);
        draft.Lines[0].Quantity = 3;
        draft.Lines.Add(new CartLine { MenuItemId = _naan.Id, Name = _naan.Name, UnitPrice = _naan.Price, Quantity = 4 });

        var second = await _service.SendToKitchenAsync(draft);

        Assert.NotNull(second.Ticket);
        Assert.True(second.Ticket!.IsAdditional);
        Assert.Equal(2, second.Ticket.KotNumber);
        Assert.Contains(second.Ticket.Lines, l => l.Name == "Chicken Tikka" && l.Quantity == 1 && !l.IsCancelled);
        Assert.Contains(second.Ticket.Lines, l => l.Name == "Naan" && l.Quantity == 4);

        var third = await _service.SendToKitchenAsync(await _service.GetDraftAsync(first.OrderId));
        Assert.Null(third.Ticket);
    }

    [Fact]
    public async Task Removing_a_sent_item_prints_cancellation_on_next_kot()
    {
        var first = await _service.SendToKitchenAsync(NewDineIn((_tikka, 2), (_naan, 2)));
        var draft = await _service.GetDraftAsync(first.OrderId);
        draft.Lines.RemoveAll(l => l.MenuItemId == _naan.Id);

        var result = await _service.SendToKitchenAsync(draft);

        var line = Assert.Single(result.Ticket!.Lines);
        Assert.Equal("Naan", line.Name);
        Assert.True(line.IsCancelled);
        Assert.Equal(2, line.Quantity);

        var reloaded = await _service.GetDraftAsync(first.OrderId);
        Assert.Single(reloaded.Lines);
    }

    [Fact]
    public async Task Complete_with_card_uses_card_tax_and_frees_table()
    {
        var kitchen = await _service.SendToKitchenAsync(NewDineIn((_tikka, 2)));

        var invoice = await _service.CompleteAsync(kitchen.OrderId, PaymentMethod.Card, 0);

        Assert.True(invoice.IsPaid);
        Assert.Equal(5, invoice.TaxPercent);
        Assert.Equal(1300 + 65, invoice.GrandTotal);
        Assert.Equal(invoice.GrandTotal, invoice.Paid);
        Assert.Empty(await _service.GetOpenOrdersAsync());
        Assert.False((await _service.GetTableStatusesAsync()).Single().IsOccupied);
    }

    [Fact]
    public async Task Cash_payment_less_than_total_is_rejected()
    {
        var kitchen = await _service.SendToKitchenAsync(NewDineIn((_tikka, 1)));

        await Assert.ThrowsAsync<AppException>(() => _service.CompleteAsync(kitchen.OrderId, PaymentMethod.Cash, 100));
    }

    [Fact]
    public async Task Cannot_open_second_order_on_occupied_table()
    {
        await _service.SaveAsync(NewDineIn((_tikka, 1)));

        await Assert.ThrowsAsync<AppException>(() => _service.SaveAsync(NewDineIn((_naan, 1))));
    }

    [Fact]
    public async Task Cancel_after_kot_returns_cancellation_ticket()
    {
        var kitchen = await _service.SendToKitchenAsync(NewDineIn((_tikka, 2)));

        var ticket = await _service.CancelAsync(kitchen.OrderId, "Customer left");

        Assert.NotNull(ticket);
        Assert.True(ticket!.IsCancellation);
        Assert.All(ticket.Lines, l => Assert.True(l.IsCancelled));
        Assert.Empty(await _service.GetOpenOrdersAsync());
    }
}
