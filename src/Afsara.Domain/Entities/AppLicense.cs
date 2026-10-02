using Afsara.Domain.Common;

namespace Afsara.Domain.Entities;

public class AppLicense : BaseEntity
{
    public string LicenseKey { get; set; } = string.Empty;
    public string Licensee { get; set; } = string.Empty;
    public DateOnly ExpiresOn { get; set; }
    public string? MachineId { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public string Checksum { get; set; } = string.Empty;
}
