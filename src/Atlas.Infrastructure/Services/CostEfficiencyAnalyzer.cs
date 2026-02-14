using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.ValueObjects;

namespace Atlas.Infrastructure.Services;

public interface ICostEfficiencyAnalyzer
{
    Task<List<CostEfficiencyRecommendation>> AnalyzeAsync(
        List<TokenUsage> usageData,
        DateTime periodStart,
        DateTime periodEnd);
}

public class CostEfficiencyAnalyzer : ICostEfficiencyAnalyzer
{
    // Model pricing per 1M tokens (approximate)
    private static readonly Dictionary<string, (decimal InputPrice, decimal OutputPrice)> ModelPricing = new()
    {
        { "claude-opus-4-6", (15m, 75m) },
        { "claude-opus-4", (15m, 75m) },
        { "claude-sonnet-4", (3m, 15m) },
        { "claude-haiku-4-5", (0.80m, 4m) },
        { "claude-haiku-3-5", (0.80m, 4m) },
        { "gpt-4", (30m, 60m) },
        { "gpt-4-turbo", (10m, 30m) },
        { "gpt-3.5-turbo", (0.50m, 1.50m) },
    };

    public async Task<List<CostEfficiencyRecommendation>> AnalyzeAsync(
        List<TokenUsage> usageData,
        DateTime periodStart,
        DateTime periodEnd)
    {
        var recommendations = new List<CostEfficiencyRecommendation>();

        if (usageData.Count == 0)
            return recommendations;

        var daysInPeriod = (int)(periodEnd - periodStart).TotalDays;
        if (daysInPeriod <= 0) daysInPeriod = 1;

        // 1. Analyze model usage and suggest cheaper alternatives
        var modelGroups = usageData.GroupBy(u => u.Model)
            .Select(g => new
            {
                Model = g.Key,
                Count = g.Count(),
                TotalCost = g.Sum(u => u.CostUsd),
                AvgOutputTokens = g.Average(u => u.OutputTokens),
                AvgInputTokens = g.Average(u => u.InputTokens),
                MaxContextPercent = g.Max(u => u.ContextPercent ?? 0)
            })
            .ToList();

        foreach (var modelGroup in modelGroups.Where(m => m.TotalCost > 10)) // Only for models costing >$10
        {
            // Check if Haiku would be cheaper
            if (!modelGroup.Model.Contains("haiku", StringComparison.OrdinalIgnoreCase))
            {
                if (modelGroup.AvgOutputTokens < 2000) // Low output typically means simpler task
                {
                    var currentCost = modelGroup.TotalCost;
                    var haikuEstimated = EstimateMonthlyCost(
                        modelGroup.Count,
                        modelGroup.AvgInputTokens,
                        modelGroup.AvgOutputTokens,
                        "claude-haiku-4-5",
                        daysInPeriod);

                    var savings = currentCost - haikuEstimated;

                    if (savings > 1) // Only recommend if savings > $1/month
                    {
                        recommendations.Add(new CostEfficiencyRecommendation
                        {
                            Title = $"Use Haiku for {modelGroup.Model} tasks",
                            Description = $"Task '{modelGroup.Model}' averages {modelGroup.AvgOutputTokens:F0} output tokens but uses an expensive model. " +
                                         $"Claude Haiku is optimized for quick responses with lower output. " +
                                         $"Current monthly cost: ${currentCost:F2}, estimated with Haiku: ${haikuEstimated:F2}.",
                            EstimatedMonthlySavings = savings,
                            ActionItems = $"Review requests using {modelGroup.Model} with <2000 output tokens; route to claude-haiku-4-5",
                            Priority = savings > 10 ? 1 : 2
                        });
                    }
                }
            }
        }

        // 2. Analyze high context usage
        var highContextSessions = usageData
            .Where(u => u.ContextPercent.HasValue && u.ContextPercent > 80)
            .GroupBy(u => u.SessionKey ?? "unknown")
            .Select(g => new
            {
                Session = g.Key,
                Count = g.Count(),
                AvgContextPercent = g.Average(u => u.ContextPercent ?? 0),
                TotalCost = g.Sum(u => u.CostUsd)
            })
            .Where(s => s.TotalCost > 5)
            .ToList();

        foreach (var session in highContextSessions)
        {
            recommendations.Add(new CostEfficiencyRecommendation
            {
                Title = $"High context usage in session {session.Session}",
                Description = $"Session '{session.Session}' has {session.AvgContextPercent:F0}% average context usage across {session.Count} requests. " +
                             $"This suggests accumulated context is becoming large. Monthly cost: ${session.TotalCost:F2}.",
                EstimatedMonthlySavings = session.TotalCost * 0.1m, // Conservative 10% estimate
                ActionItems = "Implement context compaction or summarization to reduce token usage per request",
                Priority = 2
            });
        }

        // 3. Analyze request frequency patterns
        var sessionActivity = usageData
            .GroupBy(u => u.SessionKey ?? "unknown")
            .Select(g => new
            {
                Session = g.Key,
                Count = g.Count(),
                TotalCost = g.Sum(u => u.CostUsd),
                AvgDuration = g.Average(u => u.DurationMs ?? 0),
                AvgOutputTokens = g.Average(u => u.OutputTokens)
            })
            .Where(s => s.Count > 100) // Only for very frequent sessions
            .ToList();

        foreach (var session in sessionActivity.Where(s => s.AvgOutputTokens < 100 && s.Count > 200))
        {
            var potentialSavings = session.TotalCost * 0.3m; // 30% reduction estimate

            recommendations.Add(new CostEfficiencyRecommendation
            {
                Title = $"High-frequency, low-output session: {session.Session}",
                Description = $"Session '{session.Session}' runs {session.Count} times in this period with average output of only {session.AvgOutputTokens:F0} tokens. " +
                             $"This pattern suggests batch processing opportunities. Current monthly cost: ${session.TotalCost:F2}.",
                EstimatedMonthlySavings = potentialSavings,
                ActionItems = "Consider batching multiple requests, caching results, or reducing invocation frequency",
                Priority = 2
            });
        }

        // 4. Identify anomalies
        var avgCostPerRequest = usageData.Average(u => u.CostUsd);
        var outliers = usageData
            .Where(u => u.CostUsd > avgCostPerRequest * 5)
            .GroupBy(u => u.Model)
            .Where(g => g.Count() > 0)
            .ToList();

        if (outliers.Count > 0)
        {
            var outlierCost = outliers.Sum(g => g.Sum(u => u.CostUsd));
            recommendations.Add(new CostEfficiencyRecommendation
            {
                Title = "High-cost outlier requests detected",
                Description = $"Found {outliers.Sum(g => g.Count())} requests that cost significantly more than average " +
                             $"({avgCostPerRequest:F6} per request). Total outlier cost: ${outlierCost:F2}.",
                EstimatedMonthlySavings = outlierCost * 0.5m, // Conservative 50% reduction estimate
                ActionItems = "Review outlier requests for unexpected token usage or model selection errors",
                Priority = 1
            });
        }

        // Sort by priority and savings
        recommendations = recommendations
            .OrderBy(r => r.Priority)
            .ThenByDescending(r => r.EstimatedMonthlySavings)
            .ToList();

        return recommendations;
    }

    private decimal EstimateMonthlyCost(
        int requestCount,
        double avgInputTokens,
        double avgOutputTokens,
        string model,
        int daysInPeriod)
    {
        if (!ModelPricing.TryGetValue(model, out var pricing))
            return 0;

        var requestsPerDay = (double)requestCount / daysInPeriod;
        var daysPerMonth = 30m;
        var monthlyRequests = (decimal)requestsPerDay * daysPerMonth;

        var inputCost = (decimal)avgInputTokens * monthlyRequests * pricing.InputPrice / 1_000_000m;
        var outputCost = (decimal)avgOutputTokens * monthlyRequests * pricing.OutputPrice / 1_000_000m;

        return inputCost + outputCost;
    }
}
