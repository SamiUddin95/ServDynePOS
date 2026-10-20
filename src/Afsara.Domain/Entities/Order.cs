using Afsara.Domain.Common;
using Afsara.Domain.Enums;

namespace Afsara.Domain.Entities;

public class Order : BaseEntity
{
    public int DailyNumber { get; set; }
    public DateTime BusinessDate { get; set; } = DateTime.Today;
    public OrderType OrderType { get; set; } = OrderType.DineIn;
    public OrderStatus Status { get; set; } = OrderStatus.Open;
    public int Guests { get; set; } = 1;
    public string? Notes { get; set; }
    public int KotCount { get; set; }

    public int? BusinessDayId { get; set; }
    public BusinessDay? BusinessDay { get; set; }

    public int? TableId { get; set; }
    public DiningTable? Table { get; set; }

    public int? WaiterId { get; set; }
    public Waiter? Waiter { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }

    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }

    public decimal SubTotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public int ServiceChargeAmount { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DeliveryCharge { get; set; }
    public decimal GrandTotal { get; set; }

    public PaymentMethod? PaymentMethod { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    public string InvoiceNo => $"INV-{Id:D6}";
    public string OrderNo => $"{DailyNumber:D4}";

    public void Recalculate()
    {
        SubTotal = Items.Where(i => i.Quantity > 0).Sum(i => i.LineTotal);

        DiscountAmount = DiscountPercent > 0
            ? Round(SubTotal * DiscountPercent / 100m)
            : Math.Min(Math.Max(DiscountAmount, 0), SubTotal);

        var taxable = SubTotal - DiscountAmount;

        ServiceCharge = OrderType == OrderType.DineIn && SubTotal > 0
            ? ServiceChargeAmount
            : 0;

        TaxAmount = Round(taxable * TaxPercent / 100m);

        if (OrderType != OrderType.Delivery)
            DeliveryCharge = 0;

        GrandTotal = Round(taxable + ServiceCharge + TaxAmount + DeliveryCharge);
    }

    private static decimal Round(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);
}
