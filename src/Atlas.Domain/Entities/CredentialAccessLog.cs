using Atlas.Domain.Common;

namespace Atlas.Domain.Entities;

public class CredentialAccessLog : BaseEntity
{
    public Guid CredentialId { get; set; }
    public string CredentialName { get; set; } = "";
    public string? Requester { get; set; }
    public DateTime AccessedAt { get; set; }
    public string VaultMode { get; set; } = "locked"; // "locked" or "unlocked"
    public bool AutoApproved { get; set; } // true if unlocked mode, false if locked mode requiring approval
    public string? Details { get; set; }
}
