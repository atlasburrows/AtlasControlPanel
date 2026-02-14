using Atlas.Domain.Common;

namespace Atlas.Domain.Entities;

public class PairingCode : BaseEntity
{
    public string Code { get; set; } = string.Empty; // 6-digit human-readable code
    public string Token { get; set; } = string.Empty; // Full token for QR code
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsValid => !IsUsed && !IsExpired;
}
