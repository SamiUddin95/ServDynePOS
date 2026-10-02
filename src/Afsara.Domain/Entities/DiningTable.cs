using Afsara.Domain.Common;

namespace Afsara.Domain.Entities;

public class DiningTable : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Area { get; set; } = "Main Hall";
    public int Capacity { get; set; } = 4;
    public bool IsActive { get; set; } = true;
}
