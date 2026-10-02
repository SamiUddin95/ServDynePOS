using Afsara.Application.Common.Interfaces;
using Afsara.Domain.Entities;
using Afsara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Afsara.Infrastructure.Persistence;

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IPasswordHasher _hasher;

    public DatabaseInitializer(IDbContextFactory<AppDbContext> factory, IPasswordHasher hasher)
    {
        _factory = factory;
        _hasher = hasher;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        if (!await db.Users.AnyAsync(cancellationToken))
        {
            db.Users.AddRange(
                new User { Username = "admin", FullName = "Administrator", Role = UserRole.Admin, PasswordHash = _hasher.Hash("admin123") },
                new User { Username = "cashier", FullName = "Counter Cashier", Role = UserRole.Cashier, PasswordHash = _hasher.Hash("1234") });
        }

        if (!await db.RestaurantSettings.AnyAsync(cancellationToken))
        {
            db.RestaurantSettings.Add(new RestaurantSetting
            {
                RestaurantName = "SERVDYNE RESTAURANT",
                Address = "Main Boulevard, Karachi",
                Phone = "0300-1234567",
                TaxNumber = "1234567-8",
                CurrencySymbol = "Rs.",
                CashTaxPercent = 16,
                CardTaxPercent = 5,
                ServiceChargePercent = 0,
                DefaultDeliveryCharge = 150,
                InvoiceFooter = "Thank you for dining with us! Please visit again."
            });
        }

        if (!await db.Categories.AnyAsync(cancellationToken))
        {
            db.Categories.AddRange(SeedMenu());
        }

        if (!await db.DiningTables.AnyAsync(cancellationToken))
        {
            for (var i = 1; i <= 12; i++)
                db.DiningTables.Add(new DiningTable { Name = $"T-{i}", Area = "Main Hall", Capacity = 4 });
            for (var i = 1; i <= 6; i++)
                db.DiningTables.Add(new DiningTable { Name = $"F-{i}", Area = "Family Hall", Capacity = 6 });
        }

        if (!await db.Waiters.AnyAsync(cancellationToken))
        {
            db.Waiters.AddRange(
                new Waiter { Name = "Ali Raza" },
                new Waiter { Name = "Usman Khan" },
                new Waiter { Name = "Bilal Ahmed" });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static IEnumerable<Category> SeedMenu()
    {
        var order = 0;
        Category Cat(string name, string section, params (string code, string name, decimal price)[] items) => new()
        {
            Name = name,
            KitchenSection = section,
            DisplayOrder = ++order,
            Items = items.Select(i => new MenuItem { Code = i.code, Name = i.name, Price = i.price }).ToList()
        };

        yield return Cat("BBQ", "BBQ",
            ("B01", "Chicken Tikka", 650), ("B02", "Chicken Malai Boti", 850), ("B03", "Beef Seekh Kabab", 750),
            ("B04", "Reshmi Kabab", 800), ("B05", "Mutton Chops", 1800));
        yield return Cat("Karahi & Handi", "KITCHEN",
            ("K01", "Chicken Karahi (Half)", 1200), ("K02", "Chicken Karahi (Full)", 2200), ("K03", "Mutton Karahi (Half)", 2400),
            ("K04", "Chicken White Handi", 1500), ("K05", "Daal Mash", 550));
        yield return Cat("Fast Food", "FAST FOOD",
            ("F01", "Zinger Burger", 550), ("F02", "Beef Burger", 650), ("F03", "Club Sandwich", 600),
            ("F04", "French Fries", 300), ("F05", "Chicken Pizza (Large)", 1800));
        yield return Cat("Chinese", "KITCHEN",
            ("C01", "Chicken Chowmein", 850), ("C02", "Egg Fried Rice", 700), ("C03", "Chicken Manchurian", 950),
            ("C04", "Hot & Sour Soup", 450));
        yield return Cat("Bread & Rice", "TANDOOR",
            ("T01", "Roti", 30), ("T02", "Naan", 50), ("T03", "Garlic Naan", 120), ("T04", "Chicken Biryani", 450));
        yield return Cat("Beverages", "BAR",
            ("D01", "Soft Drink (Regular)", 120), ("D02", "Mineral Water (Large)", 150), ("D03", "Fresh Lime", 250),
            ("D04", "Mint Margarita", 350), ("D05", "Doodh Patti Chai", 150));
        yield return Cat("Desserts", "BAR",
            ("S01", "Kheer", 300), ("S02", "Gulab Jamun (2 pcs)", 200), ("S03", "Ice Cream Scoop", 250));
    }
}
