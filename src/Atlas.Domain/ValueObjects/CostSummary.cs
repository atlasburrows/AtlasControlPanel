namespace Atlas.Domain.ValueObjects;

public class CostSummary
{
    public decimal DailyCost { get; set; }
    public decimal MonthlyCost { get; set; }
    public Dictionary<string, decimal> TaskBreakdown { get; set; } = new();
}
