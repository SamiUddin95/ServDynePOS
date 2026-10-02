using Afsara.Application.Common;
using Afsara.Application.Common.Interfaces;
using Afsara.Domain.Entities;
using Afsara.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Afsara.Application.Users;

public interface IUserService
{
    Task<List<User>> GetUsersAsync();
    Task SaveUserAsync(User user, string? newPassword);
    Task DeleteUserAsync(int id);
}

public class UserService : IUserService
{
    private readonly IApplicationDbContextFactory _factory;
    private readonly IPasswordHasher _hasher;
    private readonly IUserSession _session;

    public UserService(IApplicationDbContextFactory factory, IPasswordHasher hasher, IUserSession session)
    {
        _factory = factory;
        _hasher = hasher;
        _session = session;
    }

    public async Task<List<User>> GetUsersAsync()
    {
        await using var db = _factory.Create();
        return await db.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync();
    }

    public async Task SaveUserAsync(User user, string? newPassword)
    {
        if (string.IsNullOrWhiteSpace(user.Username)) throw new AppException("Username is required.");
        if (string.IsNullOrWhiteSpace(user.FullName)) throw new AppException("Full name is required.");
        if (user.Id == 0 && string.IsNullOrWhiteSpace(newPassword)) throw new AppException("Password is required for a new user.");
        if (!string.IsNullOrEmpty(newPassword) && newPassword.Length < 4) throw new AppException("Password must be at least 4 characters.");

        await using var db = _factory.Create();
        var username = user.Username.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Username == username && u.Id != user.Id))
            throw new AppException("This username is already taken.");

        var entity = user.Id == 0 ? new User() : await db.Users.FindAsync(user.Id)
            ?? throw new AppException("User not found.");

        if (entity.Id == _session.CurrentUser?.Id && (!user.IsActive || user.Role != UserRole.Admin) && entity.Role == UserRole.Admin)
            throw new AppException("You cannot disable or demote your own admin account.");

        entity.Username = username;
        entity.FullName = user.FullName.Trim();
        entity.Role = user.Role;
        entity.IsActive = user.IsActive;
        if (!string.IsNullOrEmpty(newPassword)) entity.PasswordHash = _hasher.Hash(newPassword);

        if (entity.Id == 0) db.Users.Add(entity);
        else entity.UpdatedAt = DateTime.Now;

        await db.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(int id)
    {
        if (id == _session.CurrentUser?.Id) throw new AppException("You cannot delete your own account.");

        await using var db = _factory.Create();
        var entity = await db.Users.FindAsync(id) ?? throw new AppException("User not found.");

        if (await db.Orders.AnyAsync(o => o.CreatedByUserId == id))
            entity.IsActive = false;
        else
            db.Users.Remove(entity);

        await db.SaveChangesAsync();
    }
}
