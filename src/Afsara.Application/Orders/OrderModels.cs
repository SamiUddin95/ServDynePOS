using Afsara.Domain.Enums;

namespace Afsara.Application.Orders;

public class CartLine
{
    public int? OrderItemId { get; set; }
    public int MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KitchenSection { get; set; } = "KITCHEN";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public int PrintedQuantity { get; set; }
    public string? Notes { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
    public bool IsSentToKitchen => PrintedQuantity > 0 && PrintedQuantity == Quantity;
}

public class OrderDraft
{
    public int? OrderId { get; set; }
    public string? OrderNo { get; set; }
    public OrderType OrderType { get; set; } = OrderType.DineIn;
    public int? TableId { get; set; }
    public string? TableName { get; set; }
    public int? WaiterId { get; set; }
    public int Guests { get; set; } = 1;
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public decimal DeliveryCharge { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? Notes { get; set; }
    public List<CartLine> Lines { get; set; } = new();
}

public class OrderTotals
{
    public decimal SubTotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DeliveryCharge { get; set; }
    public decimal GrandTotal { get; set; }
}

public class OpenOrderSummary
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public OrderType OrderType { get; set; }
    public string? TableName { get; set; }
    public string? WaiterName { get; set; }
    public string? CustomerName { get; set; }
    public int Items { get; set; }
    public decimal GrandTotal { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TableStatus
{
    public int TableId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int? OpenOrderId { get; set; }
    public decimal OpenAmount { get; set; }
    public DateTime? OccupiedSince { get; set; }

    public bool IsOccupied => OpenOrderId.HasValue;
}

public record KitchenResult(int OrderId, Printing.KotTicket? Ticket);
