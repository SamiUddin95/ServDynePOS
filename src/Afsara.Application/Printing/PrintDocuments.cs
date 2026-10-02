namespace Afsara.Application.Printing;

public class KotTicket
{
    public string RestaurantName { get; set; } = string.Empty;
    public int KotNumber { get; set; }
    public bool IsAdditional { get; set; }
    public bool IsCancellation { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public string? WaiterName { get; set; }
    public string? CustomerName { get; set; }
    public int Guests { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime PrintedAt { get; set; } = DateTime.Now;
    public string? Notes { get; set; }
    public List<KotLine> Lines { get; set; } = new();
}

public class KotLine
{
    public string Name { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool IsCancelled { get; set; }
    public string? Notes { get; set; }
}

public class InvoiceDocument
{
    public string RestaurantName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public string? LogoPath { get; set; }
    public string Footer { get; set; } = string.Empty;
    public string Currency { get; set; } = "Rs.";

    public bool IsPaid { get; set; }
    public bool IsDuplicate { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public string OrderNo { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public string? WaiterName { get; set; }
    public string Cashier { get; set; } = string.Empty;
    public int Guests { get; set; }
    public DateTime Date { get; set; }

    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }

    public List<InvoiceLine> Lines { get; set; } = new();

    public decimal SubTotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal Discount { get; set; }
    public decimal ServiceChargePercent { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal Tax { get; set; }
    public decimal DeliveryCharge { get; set; }
    public decimal GrandTotal { get; set; }

    public string? PaymentMethod { get; set; }
    public decimal Paid { get; set; }
    public decimal Change { get; set; }
}

public class InvoiceLine
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount => Rate * Quantity;
}
