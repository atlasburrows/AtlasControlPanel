using Atlas.Domain.Common;
using Atlas.Domain.Enums;

namespace Atlas.Domain.Entities;

public class SecurityAudit : BaseEntity
{
    public string Action { get; set; } = string.Empty;
    public Severity Severity { get; set; } = Severity.Info;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
