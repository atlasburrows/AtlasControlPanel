using System.Data;
using System.Text.Json;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.ValueObjects;
using Dapper;

namespace Atlas.Infrastructure.Repositories.Sqlite;

public class SqliteMonitoringRepository(IDbConnectionFactory connectionFactory) : IMonitoringRepository
{
    public async Task<SystemStatus?> GetSystemStatusAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SystemStatusRow>(
            "SELECT * FROM SystemStatus LIMIT 1");
        
        if (row is null) return null;
        
        return new SystemStatus
        {
            Id = Guid.Parse(row.Id),
            GatewayHealth = row.GatewayHealth ?? "Unknown",
            ActiveSessions = row.ActiveSessions,
            MemoryUsage = row.MemoryUsage,
            Uptime = TimeSpan.FromSeconds(row.Uptime),
            LastUpdated = DateTime.Parse(row.LastChecked, null, System.Globalization.DateTimeStyles.RoundtripKind),
            AnthropicBalance = row.AnthropicBalance,
            TokensRemaining = row.TokensRemaining
        };
    }

    public async Task<SystemStatus> UpsertSystemStatusAsync(SystemStatus status)
    {
        using var connection = connectionFactory.CreateConnection();
        
        // Check if record exists
        var existing = await connection.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT Id FROM SystemStatus WHERE Id = @Id",
            new { Id = status.Id.ToString() });

        if (existing is null)
        {
            // Insert
            await connection.ExecuteAsync(
                @"INSERT INTO SystemStatus (Id, GatewayHealth, ActiveSessions, MemoryUsage, Uptime, LastChecked, 
                    AnthropicBalance, TokensRemaining)
                  VALUES (@Id, @GatewayHealth, @ActiveSessions, @MemoryUsage, @Uptime, @LastChecked, 
                    @AnthropicBalance, @TokensRemaining)",
                new
                {
                    Id = status.Id.ToString(),
                    status.GatewayHealth,
                    status.ActiveSessions,
                    status.MemoryUsage,
                    Uptime = (long)status.Uptime.TotalSeconds,
                    LastChecked = status.LastUpdated.ToString("O"),
                    status.AnthropicBalance,
                    status.TokensRemaining
                });
        }
        else
        {
            // Update
            await connection.ExecuteAsync(
                @"UPDATE SystemStatus 
                  SET GatewayHealth = @GatewayHealth, ActiveSessions = @ActiveSessions, MemoryUsage = @MemoryUsage, 
                      Uptime = @Uptime, LastChecked = @LastChecked, AnthropicBalance = @AnthropicBalance, 
                      TokensRemaining = @TokensRemaining
                  WHERE Id = @Id",
                new
                {
                    status.GatewayHealth,
                    status.ActiveSessions,
                    status.MemoryUsage,
                    Uptime = (long)status.Uptime.TotalSeconds,
                    LastChecked = status.LastUpdated.ToString("O"),
                    status.AnthropicBalance,
                    status.TokensRemaining,
                    Id = status.Id.ToString()
                });
        }

        return status;
    }

    public async Task<CostSummary?> GetDailyCostAsync(DateTime date)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT DailyCost, MonthlyCost, TaskBreakdown FROM CostSummary WHERE Date = @Date",
            new { Date = date.Date.ToString("yyyy-MM-dd") });
        
        if (row is null) return null;
        
        var taskBreakdown = string.IsNullOrEmpty((string?)row.TaskBreakdown)
            ? new Dictionary<string, decimal>()
            : JsonSerializer.Deserialize<Dictionary<string, decimal>>((string)row.TaskBreakdown) ?? new();
        
        return new CostSummary
        {
            DailyCost = (decimal)row.DailyCost,
            MonthlyCost = (decimal)row.MonthlyCost,
            TaskBreakdown = taskBreakdown
        };
    }

    public async Task<CostSummary?> GetMonthlyCostAsync(int year, int month)
    {
        using var connection = connectionFactory.CreateConnection();
        
        // In SQLite, we need to get all days in the month and aggregate them
        var startDate = new DateTime(year, month, 1).ToString("yyyy-MM-dd");
        var endDate = new DateTime(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd");
        
        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(
            @"SELECT 
                SUM(DailyCost) as DailyCost, 
                SUM(MonthlyCost) as MonthlyCost, 
                TaskBreakdown
              FROM CostSummary 
              WHERE Date BETWEEN @StartDate AND @EndDate",
            new { StartDate = startDate, EndDate = endDate });
        
        if (row is null || row.DailyCost is null) return null;
        
        var taskBreakdown = string.IsNullOrEmpty((string?)row.TaskBreakdown)
            ? new Dictionary<string, decimal>()
            : JsonSerializer.Deserialize<Dictionary<string, decimal>>((string)row.TaskBreakdown) ?? new();
        
        return new CostSummary
        {
            DailyCost = (decimal)(row.DailyCost ?? 0),
            MonthlyCost = (decimal)(row.MonthlyCost ?? 0),
            TaskBreakdown = taskBreakdown
        };
    }

    public async Task UpsertCostSummaryAsync(DateTime date, CostSummary summary)
    {
        using var connection = connectionFactory.CreateConnection();
        
        var dateStr = date.Date.ToString("yyyy-MM-dd");
        var taskBreakdownJson = JsonSerializer.Serialize(summary.TaskBreakdown);
        
        // Check if record exists
        var existing = await connection.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT Date FROM CostSummary WHERE Date = @Date",
            new { Date = dateStr });

        if (existing is null)
        {
            // Insert
            await connection.ExecuteAsync(
                @"INSERT INTO CostSummary (Date, DailyCost, MonthlyCost, TaskBreakdown)
                  VALUES (@Date, @DailyCost, @MonthlyCost, @TaskBreakdown)",
                new
                {
                    Date = dateStr,
                    summary.DailyCost,
                    summary.MonthlyCost,
                    TaskBreakdown = taskBreakdownJson
                });
        }
        else
        {
            // Update
            await connection.ExecuteAsync(
                @"UPDATE CostSummary 
                  SET DailyCost = @DailyCost, MonthlyCost = @MonthlyCost, TaskBreakdown = @TaskBreakdown
                  WHERE Date = @Date",
                new
                {
                    summary.DailyCost,
                    summary.MonthlyCost,
                    TaskBreakdown = taskBreakdownJson,
                    Date = dateStr
                });
        }
    }

    public async Task IncrementDailyCostAsync(decimal costUsd)
    {
        using var connection = connectionFactory.CreateConnection();
        
        var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        
        // Check if record exists
        var existing = await connection.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT DailyCost FROM CostSummary WHERE Date = @Date",
            new { Date = today });

        if (existing is null)
        {
            // Insert
            await connection.ExecuteAsync(
                @"INSERT INTO CostSummary (Date, DailyCost, MonthlyCost, TaskBreakdown)
                  VALUES (@Date, @DailyCost, @MonthlyCost, @TaskBreakdown)",
                new
                {
                    Date = today,
                    DailyCost = costUsd,
                    MonthlyCost = costUsd,
                    TaskBreakdown = "{}"
                });
        }
        else
        {
            // Update - increment
            await connection.ExecuteAsync(
                @"UPDATE CostSummary 
                  SET DailyCost = DailyCost + @Cost, MonthlyCost = MonthlyCost + @Cost
                  WHERE Date = @Date",
                new { Cost = costUsd, Date = today });
        }
    }

    private class SystemStatusRow
    {
        public string Id { get; set; } = "";
        public string? GatewayHealth { get; set; }
        public int ActiveSessions { get; set; }
        public double MemoryUsage { get; set; }
        public long Uptime { get; set; }
        public string LastChecked { get; set; } = "";
        public string? AnthropicBalance { get; set; }
        public string? TokensRemaining { get; set; }
    }
}
