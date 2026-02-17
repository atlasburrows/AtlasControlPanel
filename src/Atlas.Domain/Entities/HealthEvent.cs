namespace Atlas.Domain.Entities;

public class HealthEvent
{
    public Guid Id { get; set; }
    public string ServiceName { get; set; } = "";
    public string EventType { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string? Details { get; set; }
    public bool NotificationSent { get; set; }
}
