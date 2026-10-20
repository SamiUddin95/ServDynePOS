using Afsara.Application.Printing;

namespace Afsara.Application.Common.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public interface IReceiptPrinter
{
    Task PrintKotAsync(KotTicket ticket);
    Task PrintInvoiceAsync(InvoiceDocument invoice, bool skipPreview = false);
    Task PrintSalesSummaryAsync(Reports.SalesReport report);
    Task PrintDaySalesAsync(Reports.DaySalesReport report);
    Task PrintDayReportAsync(Days.DayReport report, bool duplicate = false);
}
