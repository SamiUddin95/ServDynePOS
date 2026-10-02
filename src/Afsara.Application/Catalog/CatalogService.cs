using Afsara.Application.Common;
using Afsara.Application.Common.Interfaces;
using Afsara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Afsara.Application.Catalog;

public interface ICatalogService
{
    Task<List<Category>> GetCategoriesAsync(bool activeOnly = false);
    Task SaveCategoryAsync(Category category);
    Task DeleteCategoryAsync(int id);

    Task<List<MenuItem>> GetMenuItemsAsync(int? categoryId = null, bool activeOnly = false, string? search = null);
    Task SaveMenuItemAsync(MenuItem item);
    Task DeleteMenuItemAsync(int id);

    Task<List<DiningTable>> GetTablesAsync(bool activeOnly = false);
    Task SaveTableAsync(DiningTable table);
    Task DeleteTableAsync(int id);

    Task<List<Waiter>> GetWaitersAsync(bool activeOnly = false);
    Task SaveWaiterAsync(Waiter waiter);
    Task DeleteWaiterAsync(int id);
}

public class CatalogService : ICatalogService
{
    private readonly IApplicationDbContextFactory _factory;

    public CatalogService(IApplicationDbContextFactory factory) => _factory = factory;

    public async Task<List<Category>> GetCategoriesAsync(bool activeOnly = false)
    {
        await using var db = _factory.Create();
        return await db.Categories.AsNoTracking()
            .Where(c => !activeOnly || c.IsActive)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task SaveCategoryAsync(Category category)
    {
        Require(category.Name, "Category name");
        await using var db = _factory.Create();

        if (await db.Categories.AnyAsync(c => c.Name == category.Name.Trim() && c.Id != category.Id))
            throw new AppException("A category with this name already exists.");

        var entity = category.Id == 0 ? new Category() : await db.Categories.FindAsync(category.Id)
            ?? throw new AppException("Category not found.");

        entity.Name = category.Name.Trim();
        entity.KitchenSection = string.IsNullOrWhiteSpace(category.KitchenSection) ? "KITCHEN" : category.KitchenSection.Trim().ToUpperInvariant();
        entity.DisplayOrder = category.DisplayOrder;
        entity.IsActive = category.IsActive;

        if (entity.Id == 0) db.Categories.Add(entity);
        else entity.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync();
    }

    public async Task DeleteCategoryAsync(int id)
    {
        await using var db = _factory.Create();
        var entity = await db.Categories.FindAsync(id) ?? throw new AppException("Category not found.");
        if (await db.MenuItems.AnyAsync(m => m.CategoryId == id))
            throw new AppException("This category has menu items. Move or delete the items first, or mark the category inactive.");

        db.Categories.Remove(entity);
        await db.SaveChangesAsync();
    }

    public async Task<List<MenuItem>> GetMenuItemsAsync(int? categoryId = null, bool activeOnly = false, string? search = null)
    {
        await using var db = _factory.Create();
        var query = db.MenuItems.AsNoTracking().Include(m => m.Category).AsQueryable();

        if (categoryId.HasValue) query = query.Where(m => m.CategoryId == categoryId.Value);
        if (activeOnly) query = query.Where(m => m.IsActive && m.Category!.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(m => m.Name.Contains(term) || m.Code.Contains(term));
        }

        return await query.OrderBy(m => m.Name).ToListAsync();
    }

    public async Task SaveMenuItemAsync(MenuItem item)
    {
        Require(item.Name, "Item name");
        if (item.Price < 0) throw new AppException("Price cannot be negative.");
        if (item.CategoryId == 0) throw new AppException("Please select a category.");

        await using var db = _factory.Create();
        var code = item.Code?.Trim() ?? string.Empty;
        if (code.Length > 0 && await db.MenuItems.AnyAsync(m => m.Code == code && m.Id != item.Id))
            throw new AppException("Another item already uses this code.");

        var entity = item.Id == 0 ? new MenuItem() : await db.MenuItems.FindAsync(item.Id)
            ?? throw new AppException("Menu item not found.");

        entity.Code = code;
        entity.Name = item.Name.Trim();
        entity.Price = item.Price;
        entity.CategoryId = item.CategoryId;
        entity.IsActive = item.IsActive;

        if (entity.Id == 0) db.MenuItems.Add(entity);
        else entity.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync();
    }

    public async Task DeleteMenuItemAsync(int id)
    {
        await using var db = _factory.Create();
        var entity = await db.MenuItems.FindAsync(id) ?? throw new AppException("Menu item not found.");

        if (await db.OrderItems.AnyAsync(o => o.MenuItemId == id))
        {
            entity.IsActive = false;
            entity.UpdatedAt = DateTime.Now;
        }
        else
        {
            db.MenuItems.Remove(entity);
        }

        await db.SaveChangesAsync();
    }

    public async Task<List<DiningTable>> GetTablesAsync(bool activeOnly = false)
    {
        await using var db = _factory.Create();
        return await db.DiningTables.AsNoTracking()
            .Where(t => !activeOnly || t.IsActive)
            .OrderBy(t => t.Area).ThenBy(t => t.Name.Length).ThenBy(t => t.Name)
            .ToListAsync();
    }

    public async Task SaveTableAsync(DiningTable table)
    {
        Require(table.Name, "Table name");
        await using var db = _factory.Create();

        if (await db.DiningTables.AnyAsync(t => t.Name == table.Name.Trim() && t.Id != table.Id))
            throw new AppException("A table with this name already exists.");

        var entity = table.Id == 0 ? new DiningTable() : await db.DiningTables.FindAsync(table.Id)
            ?? throw new AppException("Table not found.");

        entity.Name = table.Name.Trim();
        entity.Area = string.IsNullOrWhiteSpace(table.Area) ? "Main Hall" : table.Area.Trim();
        entity.Capacity = Math.Max(1, table.Capacity);
        entity.IsActive = table.IsActive;

        if (entity.Id == 0) db.DiningTables.Add(entity);
        else entity.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync();
    }

    public async Task DeleteTableAsync(int id)
    {
        await using var db = _factory.Create();
        var entity = await db.DiningTables.FindAsync(id) ?? throw new AppException("Table not found.");

        if (await db.Orders.AnyAsync(o => o.TableId == id))
            entity.IsActive = false;
        else
            db.DiningTables.Remove(entity);

        await db.SaveChangesAsync();
    }

    public async Task<List<Waiter>> GetWaitersAsync(bool activeOnly = false)
    {
        await using var db = _factory.Create();
        return await db.Waiters.AsNoTracking()
            .Where(w => !activeOnly || w.IsActive)
            .OrderBy(w => w.Name)
            .ToListAsync();
    }

    public async Task SaveWaiterAsync(Waiter waiter)
    {
        Require(waiter.Name, "Waiter name");
        await using var db = _factory.Create();

        var entity = waiter.Id == 0 ? new Waiter() : await db.Waiters.FindAsync(waiter.Id)
            ?? throw new AppException("Waiter not found.");

        entity.Name = waiter.Name.Trim();
        entity.Phone = waiter.Phone?.Trim();
        entity.IsActive = waiter.IsActive;

        if (entity.Id == 0) db.Waiters.Add(entity);
        else entity.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync();
    }

    public async Task DeleteWaiterAsync(int id)
    {
        await using var db = _factory.Create();
        var entity = await db.Waiters.FindAsync(id) ?? throw new AppException("Waiter not found.");

        if (await db.Orders.AnyAsync(o => o.WaiterId == id))
            entity.IsActive = false;
        else
            db.Waiters.Remove(entity);

        await db.SaveChangesAsync();
    }

    private static void Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new AppException($"{field} is required.");
    }
}
