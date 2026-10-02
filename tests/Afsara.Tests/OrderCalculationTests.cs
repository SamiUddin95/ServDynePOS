using Afsara.Domain.Entities;
using Afsara.Domain.Enums;
using Xunit;

namespace Afsara.Tests;

public class OrderCalculationTests
{
    [Fact]
    public void Recalculate_applies_discount_service_and_tax_on_discounted_amount()
    {
        var order = new Order
        {
            OrderType = OrderType.DineIn,
            DiscountPercent = 10,
            ServiceChargePercent = 5,
            TaxPercent = 16,
            Items = { new OrderItem { UnitPrice = 500, Quantity = 2 } }
        };

        order.Recalculate();

        Assert.Equal(1000, order.SubTotal);
        Assert.Equal(100, order.DiscountAmount);
        Assert.Equal(45, order.ServiceCharge);
        Assert.Equal(144, order.TaxAmount);
        Assert.Equal(1089, order.GrandTotal);
    }

    [Fact]
    public void Recalculate_ignores_voided_items_and_non_delivery_charges()
    {
        var order = new Order
        {
            OrderType = OrderType.TakeAway,
            ServiceChargePercent = 10,
            DeliveryCharge = 200,
            Items =
            {
                new OrderItem { UnitPrice = 300, Quantity = 1 },
                new OrderItem { UnitPrice = 999, Quantity = 0, PrintedQuantity = 1 }
            }
        };

        order.Recalculate();

        Assert.Equal(300, order.SubTotal);
        Assert.Equal(0, order.ServiceCharge);
        Assert.Equal(0, order.DeliveryCharge);
        Assert.Equal(300, order.GrandTotal);
    }

    [Fact]
    public void Recalculate_clamps_flat_discount_to_subtotal()
    {
        var order = new Order
        {
            OrderType = OrderType.Delivery,
            DiscountAmount = 5000,
            DeliveryCharge = 150,
            Items = { new OrderItem { UnitPrice = 400, Quantity = 1 } }
        };

        order.Recalculate();

        Assert.Equal(400, order.DiscountAmount);
        Assert.Equal(150, order.GrandTotal);
    }
}
