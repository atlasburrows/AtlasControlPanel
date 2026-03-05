using System.Net.Http.Json;
using Vigil.Application.Common.Interfaces;
using Vigil.Domain.Entities;
using Vigil.Domain.Enums;

namespace Vigil.MAUI.Services;

public class HttpTaskRepository(HttpClient http) : ITaskRepository
{
    public async Task<IEnumerable<TaskItem>> GetAllAsync()
        => await http.GetFromJsonAsync<List<TaskItem>>("api/tasks") ?? [];

    public async Task<TaskItem?> GetByIdAsync(Guid id)
        => await http.GetFromJsonAsync<TaskItem>($"api/tasks/{id}");

    public async Task<TaskItem> CreateAsync(TaskItem task)
    {
        var response = await http.PostAsJsonAsync("api/tasks", task);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaskItem>())!;
    }

    public async Task<TaskItem?> UpdateAsync(TaskItem task)
    {
        var response = await http.PutAsJsonAsync($"api/tasks/{task.Id}", task);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TaskItem>();
    }

    public async Task UpdateStatusAsync(Guid id, TaskItemStatus status)
    {
        var response = await http.PutAsJsonAsync($"api/tasks/{id}/status", status);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(Guid id)
    {
        var response = await http.DeleteAsync($"api/tasks/{id}");
        response.EnsureSuccessStatusCode();
    }
}
