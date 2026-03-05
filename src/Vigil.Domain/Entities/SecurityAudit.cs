using Vigil.Domain.Common;
using Vigil.Domain.Enums;

namespace Vigil.Domain.Entities;

public class SecurityAudit : BaseEntity
{
    public string Action { get; set; } = string.Empty;
    public Severity Severity { get; set; } = Severity.Info;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
