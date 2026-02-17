using System.Data;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Dapper;

namespace Atlas.Infrastructure.Repositories;

public class HealthRepository(IDbConnectionFactory connectionFactory) : IHealthRepository
{
    public async Task<HealthCheck> RecordCheckAsync(HealthCheck check)
    {
        using var connection = connectionFactory.CreateConnection();
        var id = await connection.QuerySingleAsync<Guid>("sp_HealthChecks_Record", new
        {
            check.ServiceName,
            check.Status,
            check.CheckedAt,
            check.ResponseTimeMs,
            check.Details,
            check.AutoRestarted
        }, commandType: CommandType.StoredProcedure);
        check.Id = id;
        return check;
    }

    public async Task<HealthEvent> RecordEventAsync(HealthEvent evt)
    {
        using var connection = connectionFactory.CreateConnection();
        var id = await connection.QuerySingleAsync<Guid>("sp_HealthEvents_Record", new
        {
            evt.ServiceName,
            evt.EventType,
            evt.OccurredAt,
            evt.Details,
            evt.NotificationSent
        }, commandType: CommandType.StoredProcedure);
        evt.Id = id;
        return evt;
    }

    public async Task<IEnumerable<HealthCheck>> GetLatestChecksAsync(int take = 50)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryAsync<HealthCheck>("sp_HealthChecks_GetLatest", new { Take = take },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<HealthEvent>> GetEventsAsync(int take = 100)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryAsync<HealthEvent>("sp_HealthEvents_GetAll", new { Take = take },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<HealthCheck?> GetLatestCheckForServiceAsync(string serviceName)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<HealthCheck>("sp_HealthChecks_GetLatestForService",
            new { ServiceName = serviceName }, commandType: CommandType.StoredProcedure);
    }

    public async Task<HealthUptimeStats> GetUptimeStatsAsync(string serviceName, int days = 7)
    {
        using var connection = connectionFactory.CreateConnection();
        var result = await connection.QuerySingleOrDefaultAsync<HealthUptimeStats>("sp_Health_GetUptimeStats",
            new { ServiceName = serviceName, Days = days }, commandType: CommandType.StoredProcedure);
        return result ?? new HealthUptimeStats { ServiceName = serviceName, Days = days };
    }
}
