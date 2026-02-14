namespace Atlas.Domain.ValueObjects;

public class TaskCost
{
    public int TokensUsed { get; set; }
    public int ApiCalls { get; set; }
    public decimal EstimatedCost { get; set; }
    public string Currency { get; set; } = "USD";
}
