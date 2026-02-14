using Atlas.Domain.Common;

namespace Atlas.Domain.Entities;

public class TokenUsage : BaseEntity
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Provider { get; set; } = string.Empty;     // e.g. 'anthropic'
    public string Model { get; set; } = string.Empty;        // e.g. 'claude-opus-4-6'
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal CostUsd { get; set; }
    public int? DurationMs { get; set; }
    public string? SessionKey { get; set; }                  // which session used it
    public string? TaskCategory { get; set; }                // Development, Research, etc.
    public int? ContextPercent { get; set; }
}
