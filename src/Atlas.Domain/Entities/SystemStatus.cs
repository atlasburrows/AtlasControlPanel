using Atlas.Domain.Common;

namespace Atlas.Domain.Entities;

public class SystemStatus : BaseEntity
{
    public string GatewayHealth { get; set; } = "Unknown";
    public int ActiveSessions { get; set; }
    public double MemoryUsage { get; set; }
    public TimeSpan Uptime { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public string? AnthropicBalance { get; set; }
    public string? TokensRemaining { get; set; }
}
