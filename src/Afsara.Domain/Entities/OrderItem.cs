using Afsara.Domain.Common;

namespace Afsara.Domain.Entities;

public class OrderItem : BaseEntity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }

    public string ItemName { get; set; } = string.Empty;
    public string KitchenSection { get; set; } = "KITCHEN";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public int PrintedQuantity { get; set; }
    public string? Notes { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
    public int PendingKitchenQuantity => Quantity - PrintedQuantity;
}
