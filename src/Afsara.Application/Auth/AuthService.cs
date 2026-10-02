using Afsara.Application.Common;
using Afsara.Application.Common.Interfaces;
using Afsara.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Afsara.Application.Auth;

public interface IAuthService
{
    Task<User> LoginAsync(string username, string password);
    Task ChangePasswordAsync(string currentPassword, string newPassword);
    void Logout();
}

public class AuthService : IAuthService
{
    private readonly IApplicationDbContextFactory _factory;
    private readonly IPasswordHasher _hasher;
    private readonly IUserSession _session;

    public AuthService(IApplicationDbContextFactory factory, IPasswordHasher hasher, IUserSession session)
    {
        _factory = factory;
        _hasher = hasher;
        _session = session;
    }

    public async Task<User> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new AppException("Please enter username and password.");

        await using var db = _factory.Create();
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username.Trim());

        if (user is null || !_hasher.Verify(password, user.PasswordHash))
            throw new AppException("Invalid username or password.");

        if (!user.IsActive)
            throw new AppException("This account has been disabled. Contact your manager.");

        _session.SignIn(user);
        return user;
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var current = _session.RequireUser();
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
            throw new AppException("New password must be at least 4 characters.");

        await using var db = _factory.Create();
        var user = await db.Users.FirstAsync(u => u.Id == current.Id);
        if (!_hasher.Verify(currentPassword, user.PasswordHash))
            throw new AppException("Current password is incorrect.");

        user.PasswordHash = _hasher.Hash(newPassword);
        user.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    public void Logout() => _session.SignOut();
}
