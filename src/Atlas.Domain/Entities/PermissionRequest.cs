using Atlas.Domain.Common;
using Atlas.Domain.Enums;

namespace Atlas.Domain.Entities;

public class PermissionRequest : BaseEntity
{
    public PermissionRequestType RequestType { get; set; }
    public string Description { get; set; } = string.Empty;
    public PermissionStatus Status { get; set; } = PermissionStatus.Pending;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? Urgency { get; set; }
    public Guid? CredentialId { get; set; }
    public string? Category { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
