using Afsara.Domain.Common;
using Afsara.Domain.Enums;

namespace Afsara.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Cashier;
    public bool IsActive { get; set; } = true;

    public bool CanManage => Role is UserRole.Admin or UserRole.Manager;
}
