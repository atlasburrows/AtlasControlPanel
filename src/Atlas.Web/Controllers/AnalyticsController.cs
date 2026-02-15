using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.ValueObjects;
using Atlas.Infrastructure.Services;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Controllers;

[ApiController]
[Route("api/analytics")]
public class AnalyticsController(
    ITokenUsageRepository tokenUsageRepository,
    ICostEfficiencyAnalyzer costEfficiencyAnalyzer,
    IDbConnectionFactory connectionFactory) : ControllerBase
{
    /// <summary>
    /// Log a single token usage record
    /// </summary>
    [HttpPost("usage")]
    public async Task<IActionResult> LogUsage([FromBody] TokenUsageDto dto)
    {
        if (dto.CostUsd <= 0)
            return BadRequest("Cost must be positive");

        var usage = new TokenUsage
        {
            Id = Guid.NewGuid(),
            Timestamp = dto.Timestamp ?? DateTime.UtcNow,
            Provider = dto.Provider,
            Model = dto.Model,
            InputTokens = dto.InputTokens,
            OutputTokens = dto.OutputTokens,
            TotalTokens = dto.InputTokens + dto.OutputTokens,
            CostUsd = dto.CostUsd,
            DurationMs = dto.DurationMs,
            SessionKey = dto.SessionKey,
            TaskCategory = dto.TaskCategory,
            Project = dto.Project,
            ContextPercent = dto.ContextPercent
        };

        await tokenUsageRepository.LogUsageAsync(usage);
        return Ok(new { id = usage.Id });
    }

    /// <summary>
    /// Get flexible summary by grouping (model, day, or session)
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string groupBy = "day")
    {
        if (from >= to)
            return BadRequest("'from' must be before 'to'");

        return groupBy.ToLower() switch
        {
            "model" => Ok(await tokenUsageRepository.GetUsageSummaryByModelAsync(from, to)),
            "session" => Ok(await tokenUsageRepository.GetUsageSummaryBySessionAsync(from, to)),
            "day" => Ok(await tokenUsageRepository.GetUsageSummaryByDayAsync(from, to)),
            _ => BadRequest("groupBy must be 'model', 'session', or 'day'")
        };
    }

    /// <summary>
    /// Get daily cost for charts
    /// </summary>
    [HttpGet("cost/daily")]
    public async Task<IActionResult> GetDailyCosts([FromQuery] int days = 30)
    {
        if (days <= 0) days = 30;

        var to = DateTime.UtcNow.Date.AddDays(1);
        var from = to.AddDays(-days);

        var dailyCosts = await tokenUsageRepository.GetUsageSummaryByDayAsync(from, to);
        return Ok(dailyCosts);
    }

    /// <summary>
    /// Get cost breakdown by model
    /// </summary>
    [HttpGet("cost/by-model")]
    public async Task<IActionResult> GetCostByModel([FromQuery] int days = 30)
    {
        if (days <= 0) days = 30;

        var to = DateTime.UtcNow.Date.AddDays(1);
        var from = to.AddDays(-days);

        var costs = await tokenUsageRepository.GetUsageSummaryByModelAsync(from, to);
        return Ok(costs);
    }

    /// <summary>
    /// Get cost breakdown by session
    /// </summary>
    [HttpGet("cost/by-session")]
    public async Task<IActionResult> GetCostBySession([FromQuery] int days = 30)
    {
        if (days <= 0) days = 30;

        var to = DateTime.UtcNow.Date.AddDays(1);
        var from = to.AddDays(-days);

        var costs = await tokenUsageRepository.GetUsageSummaryBySessionAsync(from, to);
        return Ok(costs);
    }

    /// <summary>
    /// Get dashboard ROI — cost to run, operation breakdown, and savings
    /// </summary>
    [HttpGet("roi")]
    public async Task<IActionResult> GetDashboardRoi([FromQuery] int days = 30)
    {
        if (days <= 0) days = 30;

        var to = DateTime.UtcNow.Date.AddDays(1);
        var from = to.AddDays(-days);

        var roi = await tokenUsageRepository.GetDashboardRoiAsync(from, to);
        return Ok(roi);
    }

    /// <summary>
    /// Get cost breakdown by project
    /// </summary>
    [HttpGet("cost/by-project")]
    public async Task<IActionResult> GetCostByProject([FromQuery] int days = 30, [FromQuery] int inactiveDays = 7)
    {
        if (days <= 0) days = 30;
        if (inactiveDays <= 0) inactiveDays = 7;

        var to = DateTime.UtcNow.Date.AddDays(1);
        var from = to.AddDays(-days);

        var costs = await tokenUsageRepository.GetUsageSummaryByProjectAsync(from, to, inactiveDays);
        return Ok(costs);
    }

    /// <summary>
    /// Get cost optimization recommendations
    /// </summary>
    [HttpGet("efficiency")]
    public async Task<IActionResult> GetEfficiencyRecommendations([FromQuery] int days = 30)
    {
        if (days <= 0) days = 30;

        var to = DateTime.UtcNow.Date.AddDays(1);
        var from = to.AddDays(-days);

        var usageData = await tokenUsageRepository.GetUsageByDateRangeAsync(from, to);
        var recommendations = await costEfficiencyAnalyzer.AnalyzeAsync(usageData, from, to);

        return Ok(recommendations);
    }

    /// <summary>
    /// Get quick totals for a period
    /// </summary>
    [HttpGet("totals")]
    public async Task<IActionResult> GetTotals([FromQuery] string period = "month")
    {
        var to = DateTime.UtcNow.Date.AddDays(1);
        var from = period.ToLower() switch
        {
            "day" => to.AddDays(-1),
            "week" => to.AddDays(-7),
            "month" => to.AddDays(-30),
            "year" => to.AddDays(-365),
            _ => to.AddDays(-30)
        };

        var totalCost = await tokenUsageRepository.GetTotalCostAsync(from, to);
        var usageData = await tokenUsageRepository.GetUsageByDateRangeAsync(from, to);

        var daysInPeriod = (int)(to - from).TotalDays;
        if (daysInPeriod <= 0) daysInPeriod = 1;

        return Ok(new
        {
            period,
            fromDate = from,
            toDate = to,
            totalCost,
            averageDailyCost = totalCost / daysInPeriod,
            totalRequests = usageData.Count,
            totalTokens = usageData.Sum(u => u.TotalTokens)
        });
    }

    /// <summary>
    /// Get comprehensive analytics dashboard data
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] int days = 30)
    {
        if (days <= 0) days = 30;

        var to = DateTime.UtcNow.Date.AddDays(1);
        var from = to.AddDays(-days);

        var usageData = await tokenUsageRepository.GetUsageByDateRangeAsync(from, to);
        var dailyCosts = await tokenUsageRepository.GetUsageSummaryByDayAsync(from, to);
        var costByModel = await tokenUsageRepository.GetUsageSummaryByModelAsync(from, to);
        var costBySession = await tokenUsageRepository.GetUsageSummaryBySessionAsync(from, to);
        var costByProject = await tokenUsageRepository.GetUsageSummaryByProjectAsync(from, to);
        var recommendations = await costEfficiencyAnalyzer.AnalyzeAsync(usageData, from, to);

        var totalCost = await tokenUsageRepository.GetTotalCostAsync(from, to);

        // Top individual expensive requests
        var topRequests = usageData
            .OrderByDescending(u => u.CostUsd)
            .Take(15)
            .Select(u => new ExpensiveRequest
            {
                Timestamp = u.Timestamp,
                Model = u.Model,
                Project = u.Project,
                TaskCategory = u.TaskCategory,
                InputTokens = u.InputTokens,
                OutputTokens = u.OutputTokens,
                CostUsd = u.CostUsd,
                DurationMs = u.DurationMs ?? 0
            })
            .ToList();

        var topSessions = costBySession
            .OrderByDescending(s => s.TotalCost)
            .Take(10)
            .Select(s => new TopExpensiveSession
            {
                SessionKey = s.SessionKey,
                TaskCategory = s.TaskCategory,
                TotalCost = s.TotalCost,
                RequestCount = s.RequestCount,
                AverageCostPerRequest = s.RequestCount > 0 ? (double)(s.TotalCost / s.RequestCount) : 0
            })
            .ToList();

        // Filter out rejected and mark applied recommendations
        using var filterConn = connectionFactory.CreateConnection();
        var rejectedHashes = (await filterConn.QueryAsync<string>(
            "SELECT RecommendationHash FROM CostOptimizationActions WHERE Action = 'rejected'")).ToHashSet();
        var appliedHashes = (await filterConn.QueryAsync<string>(
            "SELECT RecommendationHash FROM CostOptimizationActions WHERE Action = 'applied'")).ToHashSet();

        recommendations = recommendations.Where(r => !rejectedHashes.Contains(ComputeHash(r.Title))).ToList();
        foreach (var rec in recommendations)
        {
            if (appliedHashes.Contains(ComputeHash(rec.Title)))
                rec.ActionItems = "✅ Applied — " + rec.ActionItems;
        }

        return Ok(new CostAnalyticsSummary
        {
            TotalCost = totalCost,
            AverageDailyCost = dailyCosts.Count > 0 ? dailyCosts.Average(d => d.Cost) : 0,
            TotalTokens = usageData.Sum(u => u.TotalTokens),
            TotalRequests = usageData.Count,
            FromDate = from,
            ToDate = to,
            DailyCosts = dailyCosts,
            CostByModel = costByModel,
            CostBySession = costBySession,
            CostByProject = costByProject.ToList(),
            TopSessions = topSessions,
            TopRequests = topRequests,
            Recommendations = recommendations
        });
    }

    /// <summary>
    /// Apply or reject a cost optimization recommendation
    /// </summary>
    [HttpPost("recommendation/action")]
    public async Task<IActionResult> RecommendationAction([FromBody] RecommendationActionDto dto)
    {
        if (string.IsNullOrEmpty(dto.Title) || (dto.Action != "applied" && dto.Action != "rejected"))
            return BadRequest("Title and action (applied/rejected) are required");

        var hash = ComputeHash(dto.Title);
        using var connection = connectionFactory.CreateConnection();

        // Upsert - replace if same hash+action exists
        await connection.ExecuteAsync(
            @"DELETE FROM CostOptimizationActions WHERE RecommendationHash = @Hash;
              INSERT INTO CostOptimizationActions (RecommendationTitle, RecommendationHash, Action, ActionDetails, Status, 
                  ImplementedAt, EstimatedMonthlySavings, PreviousCostPerDay, NewCostPerDay)
              VALUES (@Title, @Hash, @Action, @Details, @Status,
                  CASE WHEN @Action = 'applied' THEN SYSDATETIMEOFFSET() ELSE NULL END,
                  @EstMonthlySavings, @PrevCostPerDay, @NewCostPerDay)",
            new { 
                Hash = hash, 
                dto.Title, 
                dto.Action, 
                Details = dto.Details,
                Status = dto.Action == "applied" ? "pending" : "done",
                EstMonthlySavings = dto.EstimatedMonthlySavings,
                PrevCostPerDay = dto.PreviousCostPerDay,
                NewCostPerDay = dto.NewCostPerDay
            });

        return Ok(new { hash, action = dto.Action, status = dto.Action == "applied" ? "pending" : "done" });
    }

    /// <summary>
    /// Get recommendation history with computed savings since implementation
    /// </summary>
    [HttpGet("recommendation/actions")]
    public async Task<IActionResult> GetRecommendationActions()
    {
        using var connection = connectionFactory.CreateConnection();
        var actions = await connection.QueryAsync<RecommendationHistoryItem>(
            @"SELECT 
                Id, RecommendationTitle, RecommendationHash, Action, ActionDetails, 
                CreatedAt, ImplementedAt, Status,
                EstimatedMonthlySavings, PreviousCostPerDay, NewCostPerDay,
                CASE WHEN Action = 'applied' AND ImplementedAt IS NOT NULL AND PreviousCostPerDay IS NOT NULL
                     THEN DATEDIFF(DAY, ImplementedAt, SYSDATETIMEOFFSET()) * (PreviousCostPerDay - ISNULL(NewCostPerDay, 0))
                     ELSE 0 END as ActualSavingsSinceImplementation,
                CASE WHEN ImplementedAt IS NOT NULL
                     THEN DATEDIFF(DAY, ImplementedAt, SYSDATETIMEOFFSET())
                     ELSE 0 END as DaysSinceImplementation
              FROM CostOptimizationActions 
              ORDER BY CreatedAt DESC");
        
        var totalSaved = actions.Where(a => a.Action == "applied").Sum(a => a.ActualSavingsSinceImplementation);
        
        return Ok(new { 
            actions = actions.ToList(), 
            totalSavedAllTime = totalSaved,
            appliedCount = actions.Count(a => a.Action == "applied"),
            rejectedCount = actions.Count(a => a.Action == "rejected")
        });
    }

    private static string ComputeHash(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}

public record RecommendationActionDto(
    string Title, string Action, string? Details = null,
    decimal? EstimatedMonthlySavings = null, decimal? PreviousCostPerDay = null, decimal? NewCostPerDay = null);

public class RecommendationHistoryItem
{
    public Guid Id { get; set; }
    public string RecommendationTitle { get; set; } = "";
    public string RecommendationHash { get; set; } = "";
    public string Action { get; set; } = "";
    public string? ActionDetails { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ImplementedAt { get; set; }
    public string Status { get; set; } = "";
    public decimal? EstimatedMonthlySavings { get; set; }
    public decimal? PreviousCostPerDay { get; set; }
    public decimal? NewCostPerDay { get; set; }
    public decimal ActualSavingsSinceImplementation { get; set; }
    public int DaysSinceImplementation { get; set; }
}

public record TokenUsageDto(
    string Provider,
    string Model,
    int InputTokens,
    int OutputTokens,
    decimal CostUsd,
    int? DurationMs = null,
    string? SessionKey = null,
    string? TaskCategory = null,
    string? Project = null,
    int? ContextPercent = null,
    DateTime? Timestamp = null);
