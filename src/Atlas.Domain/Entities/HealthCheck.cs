namespace Atlas.Domain.Entities;

public class HealthCheck
{
    public Guid Id { get; set; }
    public string ServiceName { get; set; } = "";
    public string Status { get; set; } = "Healthy";
    public DateTime CheckedAt { get; set; }
    public double? ResponseTimeMs { get; set; }
    public string? Details { get; set; }
    public bool AutoRestarted { get; set; }
}
