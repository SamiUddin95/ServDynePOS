using Afsara.Domain.Common;

namespace Afsara.Domain.Entities;

public class Waiter : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}
