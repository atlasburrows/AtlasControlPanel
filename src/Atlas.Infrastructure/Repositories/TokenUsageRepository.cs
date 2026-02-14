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

    public async Task<IEnumerable<ProjectCostSummary>> GetUsageSummaryByProjectAsync(DateTime from, DateTime to)
    {
        using var connection = connectionFactory.CreateConnection();
        var result = await connection.QueryAsync<ProjectCostSummary>(
            @"SELECT 
                Project,
                SUM(CAST(CostUsd AS FLOAT)) as TotalCost,
                SUM(InputTokens) as TotalInputTokens,
                SUM(OutputTokens) as TotalOutputTokens,
                COUNT(*) as RequestCount
              FROM TokenUsage
              WHERE Timestamp >= @From AND Timestamp < @To AND Project IS NOT NULL
              GROUP BY Project
              ORDER BY TotalCost DESC",
            new { From = from, To = to });
        return result.ToList();
    }
}
