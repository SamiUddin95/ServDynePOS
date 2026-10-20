using Afsara.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Afsara.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Username).HasMaxLength(50).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.Property(c => c.KitchenSection).HasMaxLength(50).IsRequired();
        builder.HasIndex(c => c.Name).IsUnique();
    }
}

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.Property(m => m.Code).HasMaxLength(20);
        builder.Property(m => m.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(m => m.Code).IsUnique().HasFilter("[Code] <> ''");
        builder.HasOne(m => m.Category)
            .WithMany(c => c.Items)
            .HasForeignKey(m => m.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DiningTableConfiguration : IEntityTypeConfiguration<DiningTable>
{
    public void Configure(EntityTypeBuilder<DiningTable> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(50).IsRequired();
        builder.Property(t => t.Area).HasMaxLength(50).IsRequired();
        builder.HasIndex(t => t.Name).IsUnique();
    }
}

public class WaiterConfiguration : IEntityTypeConfiguration<Waiter>
{
    public void Configure(EntityTypeBuilder<Waiter> builder)
    {
        builder.Property(w => w.Name).HasMaxLength(100).IsRequired();
        builder.Property(w => w.Phone).HasMaxLength(30);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(100);
        builder.Property(c => c.Phone).HasMaxLength(30).IsRequired();
        builder.Property(c => c.Address).HasMaxLength(300);
        builder.HasIndex(c => c.Phone).IsUnique();
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.BusinessDate).HasColumnType("date");
        builder.Property(o => o.Notes).HasMaxLength(500);
        builder.Property(o => o.CustomerName).HasMaxLength(100);
        builder.Property(o => o.CustomerPhone).HasMaxLength(30);
        builder.Property(o => o.DeliveryAddress).HasMaxLength(300);
        builder.Property(o => o.CancelReason).HasMaxLength(300);

        builder.HasIndex(o => new { o.BusinessDate, o.DailyNumber });
        builder.HasIndex(o => o.Status);

        builder.HasOne(o => o.Table).WithMany().HasForeignKey(o => o.TableId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Waiter).WithMany().HasForeignKey(o => o.WaiterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Customer).WithMany().HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(o => o.CreatedBy).WithMany().HasForeignKey(o => o.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.BusinessDay).WithMany().HasForeignKey(o => o.BusinessDayId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.Property(i => i.ItemName).HasMaxLength(150).IsRequired();
        builder.Property(i => i.KitchenSection).HasMaxLength(50).IsRequired();
        builder.Property(i => i.Notes).HasMaxLength(200);
        builder.HasOne(i => i.MenuItem).WithMany().HasForeignKey(i => i.MenuItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RestaurantSettingConfiguration : IEntityTypeConfiguration<RestaurantSetting>
{
    public void Configure(EntityTypeBuilder<RestaurantSetting> builder)
    {
        builder.Property(s => s.RestaurantName).HasMaxLength(150).IsRequired();
        builder.Property(s => s.Address).HasMaxLength(300);
        builder.Property(s => s.Phone).HasMaxLength(50);
        builder.Property(s => s.TaxNumber).HasMaxLength(50);
        builder.Property(s => s.CurrencySymbol).HasMaxLength(10);
        builder.Property(s => s.InvoiceFooter).HasMaxLength(300);
        builder.Property(s => s.LogoPath).HasMaxLength(400);
        builder.Property(s => s.KotPrinterName).HasMaxLength(200);
        builder.Property(s => s.InvoicePrinterName).HasMaxLength(200);
    }
}

public class BusinessDayConfiguration : IEntityTypeConfiguration<BusinessDay>
{
    public void Configure(EntityTypeBuilder<BusinessDay> builder)
    {
        builder.Property(d => d.BusinessDate).HasColumnType("date");
        builder.Property(d => d.OpeningNotes).HasMaxLength(300);
        builder.Property(d => d.ClosingNotes).HasMaxLength(500);
        builder.Property(d => d.CashCount).HasMaxLength(500);

        builder.HasIndex(d => d.BusinessDate);
        builder.HasIndex(d => d.Status).IsUnique().HasFilter("[Status] = 1");

        builder.HasOne(d => d.OpenedBy).WithMany().HasForeignKey(d => d.OpenedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.ClosedBy).WithMany().HasForeignKey(d => d.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AppLicenseConfiguration : IEntityTypeConfiguration<AppLicense>
{
    public void Configure(EntityTypeBuilder<AppLicense> builder)
    {
        builder.ToTable("Licenses");
        builder.Property(l => l.LicenseKey).HasMaxLength(2000).IsRequired();
        builder.Property(l => l.Licensee).HasMaxLength(200);
        builder.Property(l => l.MachineId).HasMaxLength(50);
        builder.Property(l => l.Checksum).HasMaxLength(100).IsRequired();
    }
}
