using Atlas.Domain.Common;

namespace Atlas.Domain.Entities;

public class PairedDevice : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty; // "mobile", "desktop", "browser"
    public string ApiKey { get; set; } = string.Empty;
    public string? Platform { get; set; } // "ios", "android", "windows", "macos"
    public DateTime PairedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }
    public string? LastIpAddress { get; set; }
    public bool IsActive { get; set; } = true;
}
