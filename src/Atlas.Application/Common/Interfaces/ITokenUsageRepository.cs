using Atlas.Domain.Entities;
using Atlas.Domain.ValueObjects;

namespace Atlas.Application.Common.Interfaces;

public interface ITokenUsageRepository
{
    Task LogUsageAsync(TokenUsage usage);
    Task<List<TokenUsage>> GetUsageByDateRangeAsync(DateTime from, DateTime to);
    Task<List<ModelCostBreakdown>> GetUsageSummaryByModelAsync(DateTime from, DateTime to);
    Task<List<DailyCostPoint>> GetUsageSummaryByDayAsync(DateTime from, DateTime to);
    Task<List<SessionCostBreakdown>> GetUsageSummaryBySessionAsync(DateTime from, DateTime to);
    Task<decimal> GetTotalCostAsync(DateTime from, DateTime to);
}
