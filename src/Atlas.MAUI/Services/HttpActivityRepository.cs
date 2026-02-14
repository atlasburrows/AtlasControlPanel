using System.Net.Http.Json;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;

namespace Atlas.MAUI.Services;

public class HttpActivityRepository(HttpClient http) : IActivityRepository
{
    public async Task<IEnumerable<ActivityLog>> GetAllAsync(int take = 50)
        => await http.GetFromJsonAsync<List<ActivityLog>>($"api/activity?take={take}") ?? [];

    public async Task<ActivityLog?> GetByIdAsync(Guid id)
        => await http.GetFromJsonAsync<ActivityLog>($"api/activity/{id}");

    public async Task<ActivityLog> CreateAsync(ActivityLog log)
    {
        var response = await http.PostAsJsonAsync("api/activity", log);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ActivityLog>())!;
    }

    public async Task<IEnumerable<ActivityLog>> GetByTaskIdAsync(Guid taskId)
        => await http.GetFromJsonAsync<List<ActivityLog>>($"api/activity?taskId={taskId}") ?? [];
}
