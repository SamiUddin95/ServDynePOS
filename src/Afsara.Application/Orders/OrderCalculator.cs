using Afsara.Domain.Entities;

namespace Afsara.Application.Orders;

public static class OrderCalculator
{
    public static OrderTotals Calculate(OrderDraft draft, decimal taxPercent, decimal serviceChargePercent)
    {
        var order = new Order
        {
            OrderType = draft.OrderType,
            DiscountPercent = draft.DiscountPercent,
            DiscountAmount = draft.DiscountAmount,
            DeliveryCharge = draft.DeliveryCharge,
            TaxPercent = taxPercent,
            ServiceChargePercent = serviceChargePercent,
            Items = draft.Lines
                .Select(l => new OrderItem { UnitPrice = l.UnitPrice, Quantity = l.Quantity })
                .ToList()
        };

        order.Recalculate();
        return ToTotals(order);
    }

    public static OrderTotals ToTotals(Order order) => new()
    {
        SubTotal = order.SubTotal,
        DiscountPercent = order.DiscountPercent,
        DiscountAmount = order.DiscountAmount,
        ServiceChargePercent = order.ServiceChargePercent,
        ServiceCharge = order.ServiceCharge,
        TaxPercent = order.TaxPercent,
        TaxAmount = order.TaxAmount,
        DeliveryCharge = order.DeliveryCharge,
        GrandTotal = order.GrandTotal
    };
}
