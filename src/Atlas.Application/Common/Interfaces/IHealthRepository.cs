using Atlas.Domain.Entities;

namespace Atlas.Application.Common.Interfaces;

public interface IHealthRepository
{
    Task<HealthCheck> RecordCheckAsync(HealthCheck check);
    Task<HealthEvent> RecordEventAsync(HealthEvent evt);
    Task<IEnumerable<HealthCheck>> GetLatestChecksAsync(int take = 50);
    Task<IEnumerable<HealthEvent>> GetEventsAsync(int take = 100);
    Task<HealthCheck?> GetLatestCheckForServiceAsync(string serviceName);
    Task<HealthUptimeStats> GetUptimeStatsAsync(string serviceName, int days = 7);
}

public class HealthUptimeStats
{
    public string ServiceName { get; set; } = "";
    public int TotalChecks { get; set; }
    public int HealthyChecks { get; set; }
    public double UptimePercent { get; set; }
    public double AvgResponseTimeMs { get; set; }
    public int Days { get; set; }
}
