using Vigil.Domain.Common;
using Vigil.Domain.Enums;
using Vigil.Domain.ValueObjects;

namespace Vigil.Domain.Entities;

public class ActivityLog : BaseEntity
{
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ActivityCategory Category { get; set; }
    public TaskCost? CostInfo { get; set; }
    public Guid? RelatedTaskId { get; set; }
    public Guid? ParentId { get; set; }
    public string? Details { get; set; }
    
    // Not mapped from DB — populated in code
    public List<ActivityLog> SubEntries { get; set; } = new();
}
