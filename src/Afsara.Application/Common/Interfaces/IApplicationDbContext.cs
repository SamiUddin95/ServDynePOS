using Afsara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Afsara.Application.Common.Interfaces;

public interface IApplicationDbContext : IDisposable, IAsyncDisposable
{
    DbSet<User> Users { get; }
    DbSet<Category> Categories { get; }
    DbSet<MenuItem> MenuItems { get; }
    DbSet<DiningTable> DiningTables { get; }
    DbSet<Waiter> Waiters { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<RestaurantSetting> RestaurantSettings { get; }
    DbSet<AppLicense> Licenses { get; }
    DbSet<BusinessDay> BusinessDays { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IApplicationDbContextFactory
{
    IApplicationDbContext Create();
}
