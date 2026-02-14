using System.Data;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.ValueObjects;
using Atlas.Infrastructure.Data;
using Dapper;

namespace Atlas.Infrastructure.Repositories;

public class TokenUsageRepository(IDbConnectionFactory connectionFactory) : ITokenUsageRepository
{
    public async Task LogUsageAsync(TokenUsage usage)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO TokenUsage (Id, Timestamp, Provider, Model, InputTokens, OutputTokens, TotalTokens, CostUsd, DurationMs, SessionKey, TaskCategory, Project, ContextPercent)
              VALUES (@Id, @Timestamp, @Provider, @Model, @InputTokens, @OutputTokens, @TotalTokens, @CostUsd, @DurationMs, @SessionKey, @TaskCategory, @Project, @ContextPercent)",
            usage);
    }

    public async Task<List<TokenUsage>> GetUsageByDateRangeAsync(DateTime from, DateTime to)
    {
        using var connection = connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<TokenUsage>(
            @"SELECT * FROM TokenUsage 
              WHERE Timestamp >= @From AND Timestamp < @To
              ORDER BY Timestamp DESC",
            new { From = from, To = to });
        return result.ToList();
    }

    public async Task<List<ModelCostBreakdown>> GetUsageSummaryByModelAsync(DateTime from, DateTime to)
    {
        using var connection = connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<ModelCostBreakdown>(
            @"SELECT 
                Model,
                SUM(CAST(CostUsd AS FLOAT)) as TotalCost,
                COUNT(*) as RequestCount,
                SUM(TotalTokens) as TotalTokens
              FROM TokenUsage
              WHERE Timestamp >= @From AND Timestamp < @To
              GROUP BY Model
              ORDER BY TotalCost DESC",
            new { From = from, To = to });
        return result.ToList();
    }

    public async Task<List<DailyCostPoint>> GetUsageSummaryByDayAsync(DateTime from, DateTime to)
    {
        using var connection = connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<DailyCostPoint>(
            @"SELECT 
                CAST(Timestamp AS DATE) as Date,
                SUM(CAST(CostUsd AS FLOAT)) as Cost,
                COUNT(*) as RequestCount
              FROM TokenUsage
              WHERE Timestamp >= @From AND Timestamp < @To
              GROUP BY CAST(Timestamp AS DATE)
              ORDER BY Date ASC",
            new { From = from, To = to });
        return result.ToList();
    }

    public async Task<List<SessionCostBreakdown>> GetUsageSummaryBySessionAsync(DateTime from, DateTime to)
    {
        using var connection = connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<SessionCostBreakdown>(
            @"SELECT 
                SessionKey,
                TaskCategory,
                SUM(CAST(CostUsd AS FLOAT)) as TotalCost,
                COUNT(*) as RequestCount
              FROM TokenUsage
              WHERE Timestamp >= @From AND Timestamp < @To
              GROUP BY SessionKey, TaskCategory
              ORDER BY TotalCost DESC",
            new { From = from, To = to });
        return result.ToList();
    }

    public async Task<decimal> GetTotalCostAsync(DateTime from, DateTime to)
    {
        using var connection = connectionFactory.CreateConnection();
        var result = await connection.QuerySingleAsync<decimal?>(
            @"SELECT SUM(CAST(CostUsd AS FLOAT)) FROM TokenUsage
              WHERE Timestamp >= @From AND Timestamp < @To",
            new { From = from, To = to });
        return result ?? 0;
    }

    public async Task<IEnumerable<ProjectCostSummary>> GetUsageSummaryByProjectAsync(DateTime from, DateTime to, int inactiveDaysThreshold = 7)
    {
        using var connection = connectionFactory.CreateConnection();
        var now = DateTime.UtcNow;
        var result = await connection.QueryAsync<ProjectCostSummary>(
            @";WITH ProjectTotals AS (
                SELECT 
                    Project,
                    SUM(CAST(CostUsd AS FLOAT)) as TotalCost,
                    SUM(InputTokens) as TotalInputTokens,
                    SUM(OutputTokens) as TotalOutputTokens,
                    COUNT(*) as RequestCount,
                    COUNT(DISTINCT CAST(Timestamp AS DATE)) as DaysActive,
                    MAX(Timestamp) as LastActivity
                FROM TokenUsage
                WHERE Timestamp >= @From AND Timestamp < @To AND Project IS NOT NULL
                GROUP BY Project
              ),
              Rolling AS (
                SELECT 
                    p.Project,
                    ISNULL((SELECT SUM(CAST(CostUsd AS FLOAT)) / 7.0 FROM TokenUsage 
                        WHERE Project = p.Project AND Timestamp >= DATEADD(DAY, -7, @Now) AND Timestamp < @Now), 0) as RollingAvg7Day,
                    ISNULL((SELECT SUM(CAST(CostUsd AS FLOAT)) / 14.0 FROM TokenUsage 
                        WHERE Project = p.Project AND Timestamp >= DATEADD(DAY, -14, @Now) AND Timestamp < @Now), 0) as RollingAvg14Day,
                    ISNULL((SELECT SUM(CAST(CostUsd AS FLOAT)) / 30.0 FROM TokenUsage 
                        WHERE Project = p.Project AND Timestamp >= DATEADD(DAY, -30, @Now) AND Timestamp < @Now), 0) as RollingAvg30Day
                FROM ProjectTotals p
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
                DATEDIFF(DAY, pt.LastActivity, @Now) as DaysSinceLastActivity,
                CASE WHEN DATEDIFF(DAY, pt.LastActivity, @Now) <= @InactiveDaysThreshold THEN 1 ELSE 0 END as IsActive,
                r.RollingAvg7Day,
                r.RollingAvg14Day,
                r.RollingAvg30Day
              FROM ProjectTotals pt
              JOIN Rolling r ON r.Project = pt.Project
              ORDER BY pt.TotalCost DESC",
            new { From = from, To = to, Now = now, InactiveDaysThreshold = inactiveDaysThreshold });
        return result.ToList();
    }
}
