using System.Data;
using System.Text.Json;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.ValueObjects;
using Atlas.Infrastructure.Data;
using Dapper;

namespace Atlas.Infrastructure.Repositories;

public class MonitoringRepository(IDbConnectionFactory connectionFactory) : IMonitoringRepository
{
    public async Task<SystemStatus?> GetSystemStatusAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SystemStatusRow>("sp_SystemStatus_Get", commandType: CommandType.StoredProcedure);
        if (row is null) return null;
        return new SystemStatus
        {
            Id = row.Id,
            GatewayHealth = row.GatewayHealth ?? "Unknown",
            ActiveSessions = row.ActiveSessions,
            MemoryUsage = row.MemoryUsage,
            Uptime = TimeSpan.FromSeconds(row.Uptime),
            LastUpdated = row.LastChecked,
            AnthropicBalance = row.AnthropicBalance,
            TokensRemaining = row.TokensRemaining
        };
    }

    public async Task<SystemStatus> UpsertSystemStatusAsync(SystemStatus status)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("sp_SystemStatus_Upsert", new
        {
            status.Id,
            status.GatewayHealth,
            status.ActiveSessions,
            status.MemoryUsage,
            Uptime = (long)status.Uptime.TotalSeconds,
            LastChecked = status.LastUpdated
        }, commandType: CommandType.StoredProcedure);
        return status;
    }

    public async Task<CostSummary?> GetDailyCostAsync(DateTime date)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<dynamic>("sp_CostSummary_GetDaily", new { Date = date.Date }, commandType: CommandType.StoredProcedure);
        if (row is null) return null;
        return new CostSummary
        {
            DailyCost = (decimal)row.DailyCost,
            MonthlyCost = (decimal)row.MonthlyCost,
            TaskBreakdown = string.IsNullOrEmpty((string?)row.TaskBreakdown)
                ? new Dictionary<string, decimal>()
                : JsonSerializer.Deserialize<Dictionary<string, decimal>>((string)row.TaskBreakdown) ?? new()
        };
    }

    public async Task<CostSummary?> GetMonthlyCostAsync(int year, int month)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<dynamic>("sp_CostSummary_GetMonthly", new { Year = year, Month = month }, commandType: CommandType.StoredProcedure);
        if (row is null) return null;
        return new CostSummary
        {
            DailyCost = (decimal)row.DailyCost,
            MonthlyCost = (decimal)row.MonthlyCost,
            TaskBreakdown = string.IsNullOrEmpty((string?)row.TaskBreakdown)
                ? new Dictionary<string, decimal>()
                : JsonSerializer.Deserialize<Dictionary<string, decimal>>((string)row.TaskBreakdown) ?? new()
        };
    }

    public async Task UpsertCostSummaryAsync(DateTime date, CostSummary summary)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("sp_CostSummary_Upsert", new
        {
            Date = date.Date,
            summary.DailyCost,
            summary.MonthlyCost,
            TaskBreakdown = JsonSerializer.Serialize(summary.TaskBreakdown)
        }, commandType: CommandType.StoredProcedure);
    }

    public async Task IncrementDailyCostAsync(decimal costUsd)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("sp_DailyCosts_Increment", new { Cost = costUsd }, commandType: CommandType.StoredProcedure);
    }

    private class SystemStatusRow
    {
        public Guid Id { get; set; }
        public string? GatewayHealth { get; set; }
        public int ActiveSessions { get; set; }
        public double MemoryUsage { get; set; }
        public long Uptime { get; set; }
        public DateTime LastChecked { get; set; }
        public string? AnthropicBalance { get; set; }
        public string? TokensRemaining { get; set; }
    }
}
