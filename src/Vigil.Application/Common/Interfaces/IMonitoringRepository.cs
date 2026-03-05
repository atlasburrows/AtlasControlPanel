using Vigil.Domain.Entities;
using Vigil.Domain.ValueObjects;

namespace Vigil.Application.Common.Interfaces;

public interface IMonitoringRepository
{
    Task<SystemStatus?> GetSystemStatusAsync();
    Task<SystemStatus> UpsertSystemStatusAsync(SystemStatus status);
    Task<CostSummary?> GetDailyCostAsync(DateTime date);
    Task<CostSummary?> GetMonthlyCostAsync(int year, int month);
    Task UpsertCostSummaryAsync(DateTime date, CostSummary summary);
    Task IncrementDailyCostAsync(decimal costUsd);
}
