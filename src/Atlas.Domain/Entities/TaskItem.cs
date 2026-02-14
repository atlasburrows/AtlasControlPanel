using Atlas.Domain.Common;
using Atlas.Domain.Enums;
using Atlas.Domain.ValueObjects;

namespace Atlas.Domain.Entities;

public class TaskItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskItemStatus Status { get; set; } = TaskItemStatus.ToDo;
    public Priority Priority { get; set; } = Priority.Medium;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? AssignedTo { get; set; }
    public TaskCost? Cost { get; set; }

    // Scheduling
    public DateTime? ScheduledAt { get; set; }
    public RecurrenceType RecurrenceType { get; set; } = RecurrenceType.None;
    public int RecurrenceInterval { get; set; }
    public string? RecurrenceDays { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastRunAt { get; set; }

    public bool IsScheduled => ScheduledAt.HasValue;
    public bool IsRecurring => RecurrenceType != RecurrenceType.None;
}
