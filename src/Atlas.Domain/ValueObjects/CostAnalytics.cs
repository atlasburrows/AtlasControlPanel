namespace Atlas.Domain.ValueObjects;

public class DailyCostPoint
{
    public DateTime Date { get; set; }
    public decimal Cost { get; set; }
    public int RequestCount { get; set; }
}

public class ModelCostBreakdown
{
    public string Model { get; set; } = string.Empty;
    public decimal TotalCost { get; set; }
    public int RequestCount { get; set; }
    public long TotalTokens { get; set; }
}

public class SessionCostBreakdown
{
    public string? SessionKey { get; set; }
    public decimal TotalCost { get; set; }
    public int RequestCount { get; set; }
    public string? TaskCategory { get; set; }
}

public class ProjectCostSummary
{
    public string Project { get; set; } = "";
    public decimal TotalCost { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public int RequestCount { get; set; }
    public int DaysActive { get; set; }
    public decimal AverageDailyCost { get; set; }
    public bool IsActive { get; set; }
    public DateTime LastActivity { get; set; }
    public int DaysSinceLastActivity { get; set; }
    public decimal RollingAvg7Day { get; set; }
    public decimal RollingAvg14Day { get; set; }
    public decimal RollingAvg30Day { get; set; }
}

public class TopExpensiveSession
{
    public string? SessionKey { get; set; }
    public string? TaskCategory { get; set; }
    public decimal TotalCost { get; set; }
    public int RequestCount { get; set; }
    public double AverageCostPerRequest { get; set; }
}

public class CostEfficiencyRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal EstimatedMonthlySavings { get; set; }
    public string ActionItems { get; set; } = string.Empty;
    public int Priority { get; set; } // 1=high, 2=medium, 3=low
}

public class RoiSummary
{
    public decimal TotalCost { get; set; }
    public decimal OperationalCost { get; set; }  // System/heartbeat/cron overhead
    public decimal DevelopmentCost { get; set; }   // Direct dev work cost
    public decimal ActualSpend { get; set; }       // What was actually spent (all models)
    public decimal IfAllOpusSpend { get; set; }    // What it would cost if everything ran on Opus
    public decimal ModelTierSavings { get; set; }  // Savings from using cheaper models
    public decimal ProjectedMonthlySavings { get; set; }
    public int TotalRequests { get; set; }
    public int DelegatedRequests { get; set; }     // Requests on non-Opus models
    public int OpusRequests { get; set; }
    public decimal CostPerRequest { get; set; }
    public decimal OperationalPercent { get; set; } // % of total that's overhead
    public int DaysTracked { get; set; }
}

public class CostAnalyticsSummary
{
    public decimal TotalCost { get; set; }
    public decimal AverageDailyCost { get; set; }
    public long TotalTokens { get; set; }
    public int TotalRequests { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<DailyCostPoint> DailyCosts { get; set; } = new();
    public List<ModelCostBreakdown> CostByModel { get; set; } = new();
    public List<SessionCostBreakdown> CostBySession { get; set; } = new();
    public List<ProjectCostSummary> CostByProject { get; set; } = new();
    public List<TopExpensiveSession> TopSessions { get; set; } = new();
    public List<CostEfficiencyRecommendation> Recommendations { get; set; } = new();
}
