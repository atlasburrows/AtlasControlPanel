using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Dapper;

namespace Atlas.Infrastructure.Repositories.Sqlite;

public class SqliteHealthRepository(IDbConnectionFactory connectionFactory) : IHealthRepository
{
    public async Task<HealthCheck> RecordCheckAsync(HealthCheck check)
    {
        check.Id = Guid.NewGuid();
        if (check.CheckedAt == default) check.CheckedAt = DateTime.UtcNow;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO HealthChecks (Id, ServiceName, Status, CheckedAt, ResponseTimeMs, Details, AutoRestarted)
              VALUES (@Id, @ServiceName, @Status, @CheckedAt, @ResponseTimeMs, @Details, @AutoRestarted)",
            new
            {
                Id = check.Id.ToString(),
                check.ServiceName,
                check.Status,
                CheckedAt = check.CheckedAt.ToString("O"),
                check.ResponseTimeMs,
                check.Details,
                AutoRestarted = check.AutoRestarted ? 1 : 0
            });
        return check;
    }

    public async Task<HealthEvent> RecordEventAsync(HealthEvent evt)
    {
        evt.Id = Guid.NewGuid();
        if (evt.OccurredAt == default) evt.OccurredAt = DateTime.UtcNow;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO HealthEvents (Id, ServiceName, EventType, OccurredAt, Details, NotificationSent)
              VALUES (@Id, @ServiceName, @EventType, @OccurredAt, @Details, @NotificationSent)",
            new
            {
                Id = evt.Id.ToString(),
                evt.ServiceName,
                evt.EventType,
                OccurredAt = evt.OccurredAt.ToString("O"),
                evt.Details,
                NotificationSent = evt.NotificationSent ? 1 : 0
            });
        return evt;
    }

    public async Task<IEnumerable<HealthCheck>> GetLatestChecksAsync(int take = 50)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<HealthCheckRow>(
            "SELECT * FROM HealthChecks ORDER BY CheckedAt DESC LIMIT @Take", new { Take = take });
        return rows.Select(MapCheck);
    }

    public async Task<IEnumerable<HealthEvent>> GetEventsAsync(int take = 100)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<HealthEventRow>(
            "SELECT * FROM HealthEvents ORDER BY OccurredAt DESC LIMIT @Take", new { Take = take });
        return rows.Select(MapEvent);
    }

    public async Task<HealthCheck?> GetLatestCheckForServiceAsync(string serviceName)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<HealthCheckRow>(
            "SELECT * FROM HealthChecks WHERE ServiceName = @ServiceName ORDER BY CheckedAt DESC LIMIT 1",
            new { ServiceName = serviceName });
        return row is null ? null : MapCheck(row);
    }

    public async Task<HealthUptimeStats> GetUptimeStatsAsync(string serviceName, int days = 7)
    {
        using var connection = connectionFactory.CreateConnection();
        var since = DateTime.UtcNow.AddDays(-days).ToString("O");
        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            @"SELECT 
                COUNT(*) as TotalChecks,
                SUM(CASE WHEN Status = 'Healthy' THEN 1 ELSE 0 END) as HealthyChecks,
                AVG(ResponseTimeMs) as AvgResponseTimeMs
              FROM HealthChecks 
              WHERE ServiceName = @ServiceName AND CheckedAt >= @Since",
            new { ServiceName = serviceName, Since = since });

        var total = (int)(row?.TotalChecks ?? 0);
        var healthy = (int)(row?.HealthyChecks ?? 0);
        return new HealthUptimeStats
        {
            ServiceName = serviceName,
            TotalChecks = total,
            HealthyChecks = healthy,
            UptimePercent = total > 0 ? Math.Round(healthy * 100.0 / total, 2) : 0,
            AvgResponseTimeMs = row?.AvgResponseTimeMs ?? 0,
            Days = days
        };
    }

    private static HealthCheck MapCheck(HealthCheckRow r) => new()
    {
        Id = Guid.Parse(r.Id),
        ServiceName = r.ServiceName ?? "",
        Status = r.Status ?? "Unknown",
        CheckedAt = DateTime.Parse(r.CheckedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
        ResponseTimeMs = r.ResponseTimeMs,
        Details = r.Details,
        AutoRestarted = r.AutoRestarted == 1
    };

    private static HealthEvent MapEvent(HealthEventRow r) => new()
    {
        Id = Guid.Parse(r.Id),
        ServiceName = r.ServiceName ?? "",
        EventType = r.EventType ?? "",
        OccurredAt = DateTime.Parse(r.OccurredAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
        Details = r.Details,
        NotificationSent = r.NotificationSent == 1
    };

    private class HealthCheckRow
    {
        public string Id { get; set; } = "";
        public string? ServiceName { get; set; }
        public string? Status { get; set; }
        public string CheckedAt { get; set; } = "";
        public double? ResponseTimeMs { get; set; }
        public string? Details { get; set; }
        public int AutoRestarted { get; set; }
    }

    private class HealthEventRow
    {
        public string Id { get; set; } = "";
        public string? ServiceName { get; set; }
        public string? EventType { get; set; }
        public string OccurredAt { get; set; } = "";
        public string? Details { get; set; }
        public int NotificationSent { get; set; }
    }
}
