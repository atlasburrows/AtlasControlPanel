using System.Net.Http.Json;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.ValueObjects;

namespace Atlas.MAUI.Services;

public class HttpMonitoringRepository(HttpClient http) : IMonitoringRepository
{
    public async Task<SystemStatus?> GetSystemStatusAsync()
    {
        try { return await http.GetFromJsonAsync<SystemStatus>("api/monitoring/status"); }
        catch (HttpRequestException) { return null; }
    }

    public async Task<SystemStatus> UpsertSystemStatusAsync(SystemStatus status)
    {
        var response = await http.PutAsJsonAsync("api/monitoring/status", status);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SystemStatus>())!;
    }

    public async Task<CostSummary?> GetDailyCostAsync(DateTime date)
    {
        try { return await http.GetFromJsonAsync<CostSummary>($"api/monitoring/cost/daily?date={date:yyyy-MM-dd}"); }
        catch (HttpRequestException) { return null; }
    }

    public async Task<CostSummary?> GetMonthlyCostAsync(int year, int month)
    {
        try { return await http.GetFromJsonAsync<CostSummary>($"api/monitoring/cost/monthly?year={year}&month={month}"); }
        catch (HttpRequestException) { return null; }
    }

    public async Task UpsertCostSummaryAsync(DateTime date, CostSummary summary)
    {
        // Not exposed via API currently — MAUI is read-only for costs
        await Task.CompletedTask;
    }

    public async Task IncrementDailyCostAsync(decimal costUsd)
    {
        await http.PostAsJsonAsync("api/monitoring/cost", new { costUsd });
    }
}
