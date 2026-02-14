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
            @"INSERT INTO TokenUsage (Id, Timestamp, Provider, Model, InputTokens, OutputTokens, TotalTokens, CostUsd, DurationMs, SessionKey, TaskCategory, Project, ContextPercent)
              VALUES (@Id, @Timestamp, @Provider, @Model, @InputTokens, @OutputTokens, @TotalTokens, @CostUsd, @DurationMs, @SessionKey, @TaskCategory, @Project, @ContextPercent)",
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
                usage.Project,
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
            Project = r.Project,
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

    public async Task<IEnumerable<ProjectCostSummary>> GetUsageSummaryByProjectAsync(DateTime from, DateTime to, int inactiveDaysThreshold = 7)
    {
        using var connection = connectionFactory.CreateConnection();
        var now = DateTime.UtcNow;
        var result = await connection.QueryAsync<ProjectCostSummary>(
            @"WITH ProjectTotals AS (
                SELECT 
                    Project,
                    SUM(CAST(CostUsd AS REAL)) as TotalCost,
                    SUM(InputTokens) as TotalInputTokens,
                    SUM(OutputTokens) as TotalOutputTokens,
                    COUNT(*) as RequestCount,
                    COUNT(DISTINCT DATE(Timestamp)) as DaysActive,
                    MAX(Timestamp) as LastActivity
                FROM TokenUsage
                WHERE Timestamp >= @From AND Timestamp < @To AND Project IS NOT NULL
                GROUP BY Project
              )
              SELECT 
                pt.Project,
                pt.TotalCost,
                pt.TotalInputTokens,
                pt.TotalOutputTokens,
                pt.RequestCount,
                pt.DaysActive,
                CASE WHEN pt.DaysActive > 0 THEN pt.TotalCost / pt.DaysActive ELSE 0 END as AverageDailyCost,
                pt.LastActivity,
                CAST(julianday(@Now) - julianday(pt.LastActivity) AS INTEGER) as DaysSinceLastActivity,
                CASE WHEN CAST(julianday(@Now) - julianday(pt.LastActivity) AS INTEGER) <= @InactiveDaysThreshold THEN 1 ELSE 0 END as IsActive,
                COALESCE((SELECT SUM(CAST(CostUsd AS REAL)) / 7.0 FROM TokenUsage 
                    WHERE Project = pt.Project AND Timestamp >= @R7 AND Timestamp < @Now), 0) as RollingAvg7Day,
                COALESCE((SELECT SUM(CAST(CostUsd AS REAL)) / 14.0 FROM TokenUsage 
                    WHERE Project = pt.Project AND Timestamp >= @R14 AND Timestamp < @Now), 0) as RollingAvg14Day,
                COALESCE((SELECT SUM(CAST(CostUsd AS REAL)) / 30.0 FROM TokenUsage 
                    WHERE Project = pt.Project AND Timestamp >= @R30 AND Timestamp < @Now), 0) as RollingAvg30Day
              FROM ProjectTotals pt
              ORDER BY pt.TotalCost DESC",
            new { 
                From = from.ToString("O"), 
                To = to.ToString("O"), 
                Now = now.ToString("O"),
                InactiveDaysThreshold = inactiveDaysThreshold,
                R7 = now.AddDays(-7).ToString("O"),
                R14 = now.AddDays(-14).ToString("O"),
                R30 = now.AddDays(-30).ToString("O")
            });
        return result.ToList();
    }

    private const decimal OpusInputPerToken = 15m / 1_000_000m;
    private const decimal OpusOutputPerToken = 75m / 1_000_000m;

    public async Task<RoiSummary> GetRoiSummaryAsync(DateTime from, DateTime to)
    {
        using var connection = connectionFactory.CreateConnection();
        var roi = await connection.QuerySingleAsync<RoiSummary>(
            @"SELECT
                SUM(CAST(CostUsd AS REAL)) as TotalCost,
                SUM(CASE WHEN TaskCategory = 'System' THEN CAST(CostUsd AS REAL) ELSE 0 END) as OperationalCost,
                SUM(CASE WHEN TaskCategory != 'System' OR TaskCategory IS NULL THEN CAST(CostUsd AS REAL) ELSE 0 END) as DevelopmentCost,
                SUM(CAST(CostUsd AS REAL)) as ActualSpend,
                SUM(InputTokens * @OpusIn + OutputTokens * @OpusOut) as IfAllOpusSpend,
                SUM(InputTokens * @OpusIn + OutputTokens * @OpusOut) - SUM(CAST(CostUsd AS REAL)) as ModelTierSavings,
                COUNT(*) as TotalRequests,
                SUM(CASE WHEN Model NOT LIKE '%opus%' THEN 1 ELSE 0 END) as DelegatedRequests,
                SUM(CASE WHEN Model LIKE '%opus%' THEN 1 ELSE 0 END) as OpusRequests,
                CASE WHEN COUNT(*) > 0 THEN SUM(CAST(CostUsd AS REAL)) / COUNT(*) ELSE 0 END as CostPerRequest,
                CASE WHEN SUM(CAST(CostUsd AS REAL)) > 0 
                     THEN SUM(CASE WHEN TaskCategory = 'System' THEN CAST(CostUsd AS REAL) ELSE 0 END) / SUM(CAST(CostUsd AS REAL)) * 100 
                     ELSE 0 END as OperationalPercent,
                COUNT(DISTINCT DATE(Timestamp)) as DaysTracked
              FROM TokenUsage
              WHERE Timestamp >= @From AND Timestamp < @To",
            new { From = from.ToString("O"), To = to.ToString("O"), OpusIn = (double)OpusInputPerToken, OpusOut = (double)OpusOutputPerToken });

        if (roi.DaysTracked > 0 && roi.ModelTierSavings > 0)
        {
            roi.ProjectedMonthlySavings = roi.ModelTierSavings / roi.DaysTracked * 30;
        }

        return roi;
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
        public string? Project { get; set; }
        public int? ContextPercent { get; set; }
    }
}
