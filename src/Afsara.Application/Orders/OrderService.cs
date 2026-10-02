using Afsara.Application.Common;
using Afsara.Application.Common.Interfaces;
using Afsara.Application.Printing;
using Afsara.Application.Settings;
using Afsara.Domain.Entities;
using Afsara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Afsara.Application.Orders;

public interface IOrderService
{
    Task<List<TableStatus>> GetTableStatusesAsync();
    Task<List<OpenOrderSummary>> GetOpenOrdersAsync();
    Task<OrderDraft> GetDraftAsync(int orderId);
    Task<int> SaveAsync(OrderDraft draft);
    Task<KitchenResult> SendToKitchenAsync(OrderDraft draft);
    Task<OrderTotals> GetTotalsAsync(int orderId, PaymentMethod method);
    Task<InvoiceDocument> BuildBillAsync(int orderId, PaymentMethod method);
    Task<InvoiceDocument> CompleteAsync(int orderId, PaymentMethod method, decimal paidAmount);
    Task<KotTicket?> CancelAsync(int orderId, string reason);
    Task<InvoiceDocument> GetInvoiceAsync(int orderId, bool duplicate);
}

public class OrderService : IOrderService
{
    private readonly IApplicationDbContextFactory _factory;
    private readonly IUserSession _session;

    public OrderService(IApplicationDbContextFactory factory, IUserSession session)
    {
        _factory = factory;
        _session = session;
    }

    public async Task<List<TableStatus>> GetTableStatusesAsync()
    {
        await using var db = _factory.Create();

        var openOrders = await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Open && o.TableId != null)
            .Select(o => new { o.Id, o.TableId, o.GrandTotal, o.CreatedAt })
            .ToListAsync();

        var tables = await db.DiningTables.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Area).ThenBy(t => t.Name.Length).ThenBy(t => t.Name)
            .ToListAsync();

        return tables.Select(t =>
        {
            var open = openOrders.FirstOrDefault(o => o.TableId == t.Id);
            return new TableStatus
            {
                TableId = t.Id,
                Name = t.Name,
                Area = t.Area,
                Capacity = t.Capacity,
                OpenOrderId = open?.Id,
                OpenAmount = open?.GrandTotal ?? 0,
                OccupiedSince = open?.CreatedAt
            };
        }).ToList();
    }

    public async Task<List<OpenOrderSummary>> GetOpenOrdersAsync()
    {
        await using var db = _factory.Create();
        return await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Open)
            .OrderBy(o => o.CreatedAt)
            .Select(o => new OpenOrderSummary
            {
                Id = o.Id,
                OrderNo = o.DailyNumber.ToString("D4"),
                OrderType = o.OrderType,
                TableName = o.Table != null ? o.Table.Name : null,
                WaiterName = o.Waiter != null ? o.Waiter.Name : null,
                CustomerName = o.CustomerName,
                Items = o.Items.Where(i => i.Quantity > 0).Sum(i => i.Quantity),
                GrandTotal = o.GrandTotal,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<OrderDraft> GetDraftAsync(int orderId)
    {
        await using var db = _factory.Create();
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new AppException("Order not found.");

        if (order.Status != OrderStatus.Open)
            throw new AppException("This order is already closed.");

        return new OrderDraft
        {
            OrderId = order.Id,
            OrderNo = order.OrderNo,
            OrderType = order.OrderType,
            TableId = order.TableId,
            TableName = order.Table?.Name,
            WaiterId = order.WaiterId,
            Guests = order.Guests,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            DeliveryAddress = order.DeliveryAddress,
            DeliveryCharge = order.DeliveryCharge,
            DiscountPercent = order.DiscountPercent,
            DiscountAmount = order.DiscountPercent > 0 ? 0 : order.DiscountAmount,
            Notes = order.Notes,
            Lines = order.Items
                .Where(i => i.Quantity > 0)
                .OrderBy(i => i.Id)
                .Select(i => new CartLine
                {
                    OrderItemId = i.Id,
                    MenuItemId = i.MenuItemId,
                    Name = i.ItemName,
                    KitchenSection = i.KitchenSection,
                    UnitPrice = i.UnitPrice,
                    Quantity = i.Quantity,
                    PrintedQuantity = i.PrintedQuantity,
                    Notes = i.Notes
                })
                .ToList()
        };
    }

    public async Task<int> SaveAsync(OrderDraft draft)
    {
        var user = _session.RequireUser();
        await using var db = _factory.Create();
        var settings = await SettingsService.LoadAsync(db);

        Validate(draft);

        Order order;
        if (draft.OrderId is int id)
        {
            order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id)
                ?? throw new AppException("Order not found.");
            if (order.Status != OrderStatus.Open)
                throw new AppException("This order is already closed and cannot be modified.");
        }
        else
        {
            if (!draft.Lines.Any(l => l.Quantity > 0))
                throw new AppException("Add at least one item to the order.");

            var today = DateTime.Today;
            var lastNumber = await db.Orders
                .Where(o => o.BusinessDate == today)
                .MaxAsync(o => (int?)o.DailyNumber) ?? 0;

            order = new Order
            {
                BusinessDate = today,
                DailyNumber = lastNumber + 1,
                CreatedByUserId = user.Id,
                TaxPercent = settings.CashTaxPercent,
                ServiceChargePercent = settings.ServiceChargePercent
            };
            db.Orders.Add(order);
        }

        if (draft.OrderType == OrderType.DineIn && draft.TableId.HasValue)
        {
            var occupied = await db.Orders.AnyAsync(o =>
                o.Status == OrderStatus.Open && o.TableId == draft.TableId && o.Id != order.Id);
            if (occupied)
                throw new AppException("The selected table already has a running order.");
        }

        order.OrderType = draft.OrderType;
        order.TableId = draft.OrderType == OrderType.DineIn ? draft.TableId : null;
        order.WaiterId = draft.WaiterId;
        order.Guests = Math.Max(1, draft.Guests);
        order.Notes = draft.Notes?.Trim();
        order.DiscountPercent = Math.Clamp(draft.DiscountPercent, 0, 100);
        order.DiscountAmount = draft.DiscountPercent > 0 ? 0 : draft.DiscountAmount;
        order.DeliveryCharge = draft.OrderType == OrderType.Delivery ? draft.DeliveryCharge : 0;
        order.CustomerName = draft.CustomerName?.Trim();
        order.CustomerPhone = draft.CustomerPhone?.Trim();
        order.DeliveryAddress = draft.OrderType == OrderType.Delivery ? draft.DeliveryAddress?.Trim() : null;
        var customer = await UpsertCustomerAsync(db, draft);
        if (customer is null) order.CustomerId = null;
        else order.Customer = customer;

        SyncItems(db, order, draft);
        order.Recalculate();
        if (order.Id != 0) order.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync();
        return order.Id;
    }

    public async Task<KitchenResult> SendToKitchenAsync(OrderDraft draft)
    {
        var orderId = await SaveAsync(draft);

        await using var db = _factory.Create();
        var settings = await SettingsService.LoadAsync(db);
        var order = await LoadFullAsync(db, orderId, tracking: true);

        var pending = order.Items.Where(i => i.Quantity != i.PrintedQuantity).OrderBy(i => i.Id).ToList();
        if (pending.Count == 0)
            return new KitchenResult(orderId, null);

        order.KotCount++;
        var ticket = CreateKot(order, settings);
        ticket.Lines = pending.Select(i => new KotLine
        {
            Name = i.ItemName,
            Section = i.KitchenSection,
            Quantity = Math.Abs(i.PendingKitchenQuantity),
            IsCancelled = i.PendingKitchenQuantity < 0,
            Notes = i.Notes
        }).ToList();

        foreach (var item in pending)
        {
            item.PrintedQuantity = item.Quantity;
            if (item.Quantity == 0) db.OrderItems.Remove(item);
        }

        await db.SaveChangesAsync();
        return new KitchenResult(orderId, ticket);
    }

    public async Task<OrderTotals> GetTotalsAsync(int orderId, PaymentMethod method)
    {
        await using var db = _factory.Create();
        var settings = await SettingsService.LoadAsync(db);
        var order = await LoadFullAsync(db, orderId, tracking: false);
        order.TaxPercent = TaxFor(method, settings);
        order.Recalculate();
        return OrderCalculator.ToTotals(order);
    }

    public async Task<InvoiceDocument> BuildBillAsync(int orderId, PaymentMethod method)
    {
        await using var db = _factory.Create();
        var settings = await SettingsService.LoadAsync(db);
        var order = await LoadFullAsync(db, orderId, tracking: true);
        EnsureOpen(order);

        order.TaxPercent = TaxFor(method, settings);
        order.Recalculate();
        await db.SaveChangesAsync();

        return CreateInvoice(order, settings, duplicate: false);
    }

    public async Task<InvoiceDocument> CompleteAsync(int orderId, PaymentMethod method, decimal paidAmount)
    {
        await using var db = _factory.Create();
        var settings = await SettingsService.LoadAsync(db);
        var order = await LoadFullAsync(db, orderId, tracking: true);
        EnsureOpen(order);

        if (!order.Items.Any(i => i.Quantity > 0))
            throw new AppException("Cannot settle an order without items.");

        order.TaxPercent = TaxFor(method, settings);
        order.Recalculate();

        if (method == PaymentMethod.Cash && paidAmount < order.GrandTotal)
            throw new AppException($"Received amount is less than the bill total ({order.GrandTotal:N0}).");
        if (method != PaymentMethod.Cash)
            paidAmount = order.GrandTotal;

        foreach (var voided in order.Items.Where(i => i.Quantity == 0).ToList())
        {
            order.Items.Remove(voided);
            db.OrderItems.Remove(voided);
        }

        order.PaymentMethod = method;
        order.PaidAmount = paidAmount;
        order.ChangeAmount = paidAmount - order.GrandTotal;
        order.Status = OrderStatus.Completed;
        order.CompletedAt = DateTime.Now;
        order.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync();
        return CreateInvoice(order, settings, duplicate: false);
    }

    public async Task<KotTicket?> CancelAsync(int orderId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new AppException("Please provide a reason for cancellation.");

        await using var db = _factory.Create();
        var settings = await SettingsService.LoadAsync(db);
        var order = await LoadFullAsync(db, orderId, tracking: true);
        EnsureOpen(order);

        order.Status = OrderStatus.Cancelled;
        order.CancelReason = reason.Trim();
        order.CompletedAt = DateTime.Now;
        order.UpdatedAt = DateTime.Now;

        var sent = order.Items.Where(i => i.PrintedQuantity > 0).ToList();
        KotTicket? ticket = null;
        if (sent.Count > 0)
        {
            order.KotCount++;
            ticket = CreateKot(order, settings);
            ticket.IsCancellation = true;
            ticket.Notes = $"ORDER CANCELLED: {order.CancelReason}";
            ticket.Lines = sent.Select(i => new KotLine
            {
                Name = i.ItemName,
                Section = i.KitchenSection,
                Quantity = i.PrintedQuantity,
                IsCancelled = true
            }).ToList();
        }

        await db.SaveChangesAsync();
        return ticket;
    }

    public async Task<InvoiceDocument> GetInvoiceAsync(int orderId, bool duplicate)
    {
        await using var db = _factory.Create();
        var settings = await SettingsService.LoadAsync(db);
        var order = await LoadFullAsync(db, orderId, tracking: false);
        return CreateInvoice(order, settings, duplicate);
    }

    private static void Validate(OrderDraft draft)
    {
        if (draft.OrderType == OrderType.DineIn && draft.TableId is null)
            throw new AppException("Please select a table for Dine In orders.");

        if (draft.OrderType == OrderType.Delivery)
        {
            if (string.IsNullOrWhiteSpace(draft.CustomerPhone))
                throw new AppException("Customer phone is required for Delivery orders.");
            if (string.IsNullOrWhiteSpace(draft.DeliveryAddress))
                throw new AppException("Delivery address is required for Delivery orders.");
        }

        if (draft.Lines.Any(l => l.Quantity < 0))
            throw new AppException("Item quantity cannot be negative.");
    }

    private static async Task<Customer?> UpsertCustomerAsync(IApplicationDbContext db, OrderDraft draft)
    {
        var phone = draft.CustomerPhone?.Trim();
        if (string.IsNullOrEmpty(phone)) return null;

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone);
        if (customer is null)
        {
            customer = new Customer { Phone = phone };
            db.Customers.Add(customer);
        }

        if (!string.IsNullOrWhiteSpace(draft.CustomerName)) customer.Name = draft.CustomerName.Trim();
        if (!string.IsNullOrWhiteSpace(draft.DeliveryAddress)) customer.Address = draft.DeliveryAddress.Trim();

        return customer;
    }

    private static void SyncItems(IApplicationDbContext db, Order order, OrderDraft draft)
    {
        foreach (var existing in order.Items.ToList())
        {
            var line = draft.Lines.FirstOrDefault(l => l.OrderItemId == existing.Id);
            if (line is null || line.Quantity == 0)
            {
                if (existing.PrintedQuantity > 0)
                {
                    existing.Quantity = 0;
                }
                else
                {
                    order.Items.Remove(existing);
                    db.OrderItems.Remove(existing);
                }
                continue;
            }

            existing.Quantity = line.Quantity;
            existing.Notes = line.Notes?.Trim();
        }

        foreach (var line in draft.Lines.Where(l => l.OrderItemId is null && l.Quantity > 0))
        {
            order.Items.Add(new OrderItem
            {
                MenuItemId = line.MenuItemId,
                ItemName = line.Name,
                KitchenSection = line.KitchenSection,
                UnitPrice = line.UnitPrice,
                Quantity = line.Quantity,
                Notes = line.Notes?.Trim()
            });
        }
    }

    private static async Task<Order> LoadFullAsync(IApplicationDbContext db, int orderId, bool tracking)
    {
        var query = db.Orders
            .Include(o => o.Items)
            .Include(o => o.Table)
            .Include(o => o.Waiter)
            .Include(o => o.CreatedBy)
            .AsQueryable();

        if (!tracking) query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new AppException("Order not found.");
    }

    private static void EnsureOpen(Order order)
    {
        if (order.Status != OrderStatus.Open)
            throw new AppException("This order is already closed.");
    }

    private static decimal TaxFor(PaymentMethod method, RestaurantSetting settings) =>
        method == PaymentMethod.Cash ? settings.CashTaxPercent : settings.CardTaxPercent;

    private KotTicket CreateKot(Order order, RestaurantSetting settings) => new()
    {
        RestaurantName = settings.RestaurantName,
        KotNumber = order.KotCount,
        IsAdditional = order.KotCount > 1,
        OrderNo = order.OrderNo,
        OrderType = order.OrderType.ToDisplay(),
        TableName = order.Table?.Name,
        WaiterName = order.Waiter?.Name,
        CustomerName = order.CustomerName,
        Guests = order.Guests,
        CreatedBy = _session.CurrentUser?.FullName ?? order.CreatedBy?.FullName ?? string.Empty,
        PrintedAt = DateTime.Now,
        Notes = order.Notes
    };

    private InvoiceDocument CreateInvoice(Order order, RestaurantSetting settings, bool duplicate) => new()
    {
        RestaurantName = settings.RestaurantName,
        Address = settings.Address,
        Phone = settings.Phone,
        TaxNumber = settings.TaxNumber,
        LogoPath = settings.LogoPath,
        Footer = settings.InvoiceFooter,
        Currency = settings.CurrencySymbol,
        IsPaid = order.Status == OrderStatus.Completed,
        IsDuplicate = duplicate,
        InvoiceNo = order.InvoiceNo,
        OrderNo = order.OrderNo,
        OrderType = order.OrderType.ToDisplay(),
        TableName = order.Table?.Name,
        WaiterName = order.Waiter?.Name,
        Cashier = (duplicate ? order.CreatedBy?.FullName : _session.CurrentUser?.FullName) ?? order.CreatedBy?.FullName ?? string.Empty,
        Guests = order.Guests,
        Date = order.CompletedAt ?? DateTime.Now,
        CustomerName = order.CustomerName,
        CustomerPhone = order.CustomerPhone,
        DeliveryAddress = order.DeliveryAddress,
        Lines = order.Items
            .Where(i => i.Quantity > 0)
            .OrderBy(i => i.Id)
            .GroupBy(i => new { i.ItemName, i.UnitPrice })
            .Select(g => new InvoiceLine { Name = g.Key.ItemName, Rate = g.Key.UnitPrice, Quantity = g.Sum(x => x.Quantity) })
            .ToList(),
        SubTotal = order.SubTotal,
        DiscountPercent = order.DiscountPercent,
        Discount = order.DiscountAmount,
        ServiceChargePercent = order.ServiceChargePercent,
        ServiceCharge = order.ServiceCharge,
        TaxPercent = order.TaxPercent,
        Tax = order.TaxAmount,
        DeliveryCharge = order.DeliveryCharge,
        GrandTotal = order.GrandTotal,
        PaymentMethod = order.PaymentMethod?.ToDisplay(),
        Paid = order.PaidAmount,
        Change = order.ChangeAmount
    };
}
