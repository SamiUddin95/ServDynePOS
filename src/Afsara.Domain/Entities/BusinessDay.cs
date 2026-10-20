using Afsara.Domain.Common;
using Afsara.Domain.Enums;

namespace Afsara.Domain.Entities;

public class BusinessDay : BaseEntity
{
    public DateTime BusinessDate { get; set; } = DateTime.Today;
    public BusinessDayStatus Status { get; set; } = BusinessDayStatus.Open;

    public decimal OpeningBalance { get; set; }
    public string? OpeningNotes { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.Now;
    public int OpenedByUserId { get; set; }
    public User? OpenedBy { get; set; }

    public DateTime? ClosedAt { get; set; }
    public int? ClosedByUserId { get; set; }
    public User? ClosedBy { get; set; }
    public string? ClosingNotes { get; set; }

    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public decimal GrossSales { get; set; }
    public decimal Discounts { get; set; }
    public decimal ServiceCharges { get; set; }
    public decimal Taxes { get; set; }
    public decimal DeliveryCharges { get; set; }
    public decimal NetSales { get; set; }
    public decimal CashSales { get; set; }
    public decimal CardSales { get; set; }
    public decimal OnlineSales { get; set; }
    public decimal DineInSales { get; set; }
    public decimal TakeAwaySales { get; set; }
    public decimal DeliverySales { get; set; }

    public decimal ExpectedCash { get; set; }
    public decimal CountedCash { get; set; }
    public decimal CashVariance { get; set; }
    public string? CashCount { get; set; }

    public bool IsOpen => Status == BusinessDayStatus.Open;
}
