using Afsara.Domain.Entities;

namespace Afsara.Application.Common;

public interface IUserSession
{
    User? CurrentUser { get; }
    bool IsAuthenticated { get; }
    User RequireUser();
    void SignIn(User user);
    void SignOut();
}

public class UserSession : IUserSession
{
    public User? CurrentUser { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;

    public User RequireUser() => CurrentUser ?? throw new AppException("No user is signed in.");

    public void SignIn(User user) => CurrentUser = user;

    public void SignOut() => CurrentUser = null;
}
