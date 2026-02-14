using System.Data;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.ValueObjects;
using Dapper;

namespace Atlas.Infrastructure.Repositories.Sqlite;

public class SqliteTokenUsageRepository(IDbConnectionFactory connectionFactory) : ITokenUsageRepository
{
    public async Task LogUsageAsync(TokenUsage usage)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO TokenUsage (Id, Timestamp, Provider, Model, InputTokens, OutputTokens, TotalTokens, CostUsd, DurationMs, SessionKey, TaskCategory, ContextPercent)
              VALUES (@Id, @Timestamp, @Provider, @Model, @InputTokens, @OutputTokens, @TotalTokens, @CostUsd, @DurationMs, @SessionKey, @TaskCategory, @ContextPercent)",
            new
            {
                Id = usage.Id.ToString(),
                Timestamp = usage.Timestamp.ToString("O"),
                usage.Provider,
                usage.Model,
                usage.InputTokens,
                usage.OutputTokens,
                usage.TotalTokens,
                usage.CostUsd,
                usage.DurationMs,
                usage.SessionKey,
                usage.TaskCategory,
                usage.ContextPercent
            });
    }

    public async Task<List<TokenUsage>> GetUsageByDateRangeAsync(DateTime from, DateTime to)
    {
        using var connection = connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<TokenUsageRow>(
            @"SELECT * FROM TokenUsage 
              WHERE Timestamp >= @From AND Timestamp < @To
              ORDER BY Timestamp DESC",
            new { From = from.ToString("O"), To = to.ToString("O") });
        
        return result.Select(r => new TokenUsage
        {
            Id = Guid.Parse(r.Id),
            Timestamp = DateTime.Parse(r.Timestamp),
            Provider = r.Provider,
            Model = r.Model,
            InputTokens = r.InputTokens,
            OutputTokens = r.OutputTokens,
            TotalTokens = r.TotalTokens,
            CostUsd = r.CostUsd,
            DurationMs = r.DurationMs,
            SessionKey = r.SessionKey,
            TaskCategory = r.TaskCategory,
            ContextPercent = r.ContextPercent
        }).ToList();
    }

    public async Task<List<ModelCostBreakdown>> GetUsageSummaryByModelAsync(DateTime from, DateTime to)
    {
        using var connection = connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<ModelCostBreakdown>(
            @"SELECT 
                Model,
                SUM(CAST(CostUsd AS REAL)) as TotalCost,
                COUNT(*) as RequestCount,
                SUM(TotalTokens) as TotalTokens
              FROM TokenUsage
              WHERE Timestamp >= @From AND Timestamp < @To
              GROUP BY Model
              ORDER BY TotalCost DESC",
            new { From = from.ToString("O"), To = to.ToString("O") });
        return result.ToList();
    }

    public async Task<List<DailyCostPoint>> GetUsageSummaryByDayAsync(DateTime from, DateTime to)
    {
        using var connection = connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<DailyCostPoint>(
            @"SELECT 
                DATE(Timestamp) as Date,
                SUM(CAST(CostUsd AS REAL)) as Cost,
                COUNT(*) as RequestCount
              FROM TokenUsage
              WHERE Timestamp >= @From AND Timestamp < @To
              GROUP BY DATE(Timestamp)
              ORDER BY Date ASC",
            new { From = from.ToString("O"), To = to.ToString("O") });
        return result.ToList();
    }

    public async Task<List<SessionCostBreakdown>> GetUsageSummaryBySessionAsync(DateTime from, DateTime to)
    {
        using var connection = connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<SessionCostBreakdown>(
            @"SELECT 
                SessionKey,
                TaskCategory,
                SUM(CAST(CostUsd AS REAL)) as TotalCost,
                COUNT(*) as RequestCount
              FROM TokenUsage
              WHERE Timestamp >= @From AND Timestamp < @To
              GROUP BY SessionKey, TaskCategory
              ORDER BY TotalCost DESC",
            new { From = from.ToString("O"), To = to.ToString("O") });
        return result.ToList();
    }

    public async Task<decimal> GetTotalCostAsync(DateTime from, DateTime to)
    {
        using var connection = connectionFactory.CreateConnection();
        var result = await connection.QuerySingleAsync<decimal?>(
            @"SELECT SUM(CAST(CostUsd AS REAL)) FROM TokenUsage
              WHERE Timestamp >= @From AND Timestamp < @To",
            new { From = from.ToString("O"), To = to.ToString("O") });
        return result ?? 0;
    }

    private class TokenUsageRow
    {
        public string Id { get; set; } = "";
        public string Timestamp { get; set; } = "";
        public string Provider { get; set; } = "";
        public string Model { get; set; } = "";
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int TotalTokens { get; set; }
        public decimal CostUsd { get; set; }
        public int? DurationMs { get; set; }
        public string? SessionKey { get; set; }
        public string? TaskCategory { get; set; }
        public int? ContextPercent { get; set; }
    }
}
