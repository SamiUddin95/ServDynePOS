using Afsara.Application.Common.Interfaces;
using Afsara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Afsara.Application.Settings;

public interface ISettingsService
{
    Task<RestaurantSetting> GetAsync();
    Task SaveAsync(RestaurantSetting setting);
}

public class SettingsService : ISettingsService
{
    private readonly IApplicationDbContextFactory _factory;

    public SettingsService(IApplicationDbContextFactory factory) => _factory = factory;

    public async Task<RestaurantSetting> GetAsync()
    {
        await using var db = _factory.Create();
        return await LoadAsync(db);
    }

    public async Task SaveAsync(RestaurantSetting setting)
    {
        await using var db = _factory.Create();
        var entity = await db.RestaurantSettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (entity is null)
        {
            entity = new RestaurantSetting();
            db.RestaurantSettings.Add(entity);
        }

        entity.RestaurantName = setting.RestaurantName.Trim();
        entity.Address = setting.Address.Trim();
        entity.Phone = setting.Phone.Trim();
        entity.TaxNumber = setting.TaxNumber?.Trim();
        entity.CurrencySymbol = setting.CurrencySymbol.Trim();
        entity.CashTaxPercent = setting.CashTaxPercent;
        entity.CardTaxPercent = setting.CardTaxPercent;
        entity.ServiceChargeAmount = setting.ServiceChargeAmount;
        entity.DefaultDeliveryCharge = setting.DefaultDeliveryCharge;
        entity.InvoiceFooter = setting.InvoiceFooter.Trim();
        entity.LogoPath = setting.LogoPath;
        entity.KotPrinterName = setting.KotPrinterName;
        entity.InvoicePrinterName = setting.InvoicePrinterName;
        entity.ShowPrintPreview = setting.ShowPrintPreview;
        entity.InvoiceCopies = Math.Clamp(setting.InvoiceCopies, 1, 5);
        entity.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync();
    }

    internal static async Task<RestaurantSetting> LoadAsync(IApplicationDbContext db)
    {
        var setting = await db.RestaurantSettings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync();
        return setting ?? new RestaurantSetting();
    }
}
