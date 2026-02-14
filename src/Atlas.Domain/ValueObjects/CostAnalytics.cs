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
    public string Tradeoff { get; set; } = string.Empty;  // What you give up
    public int WorthinessPercent { get; set; }             // 0-100, how worth the tradeoff is
}

public class DashboardRoiSummary
{
    // What the dashboard costs to run
    public decimal DashboardOperationCost { get; set; }
    public decimal DailyAverageCost { get; set; }
    public decimal DashboardPercent { get; set; }
    public int DaysTracked { get; set; }
    public decimal TotalUserSpend { get; set; }

    // Operation breakdown
    public int ActivityLogCount { get; set; }
    public int TaskCount { get; set; }
    public int CredentialRequestCount { get; set; }
    public int MessageRelayCount { get; set; }
    public int MonitoringCheckCount { get; set; }

    // Savings
    public decimal ModelTierSavings { get; set; }
    public decimal PotentialMonthlySavings { get; set; }
    public int DelegatedRequests { get; set; }
    public int TotalRequests { get; set; }
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
