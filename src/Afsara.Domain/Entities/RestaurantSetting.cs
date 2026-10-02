using Afsara.Domain.Common;

namespace Afsara.Domain.Entities;

public class RestaurantSetting : BaseEntity
{
    public string RestaurantName { get; set; } = "Afsara Restaurant";
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? TaxNumber { get; set; }
    public string CurrencySymbol { get; set; } = "Rs.";

    public decimal CashTaxPercent { get; set; } = 16m;
    public decimal CardTaxPercent { get; set; } = 5m;
    public decimal ServiceChargePercent { get; set; }
    public decimal DefaultDeliveryCharge { get; set; }

    public string InvoiceFooter { get; set; } = "Thank you for dining with us!";
    public string? LogoPath { get; set; }

    public string? KotPrinterName { get; set; }
    public string? InvoicePrinterName { get; set; }
    public bool ShowPrintPreview { get; set; } = true;
    public int InvoiceCopies { get; set; } = 1;
}
