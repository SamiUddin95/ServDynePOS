using Afsara.Domain.Common;

namespace Afsara.Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string KitchenSection { get; set; } = "KITCHEN";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();
}
