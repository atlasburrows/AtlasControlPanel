using System.Data;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.Enums;
using Atlas.Domain.ValueObjects;
using Dapper;

namespace Atlas.Infrastructure.Repositories;

public class TaskRepository(IDbConnectionFactory connectionFactory) : ITaskRepository
{
    public async Task<IEnumerable<TaskItem>> GetAllAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<TaskRow>("sp_Tasks_GetAll", commandType: CommandType.StoredProcedure);
        return rows.Select(MapRow);
    }

    public async Task<TaskItem?> GetByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<TaskRow>("sp_Tasks_GetById", new { Id = id }, commandType: CommandType.StoredProcedure);
        return row is null ? null : MapRow(row);
    }

    public async Task<TaskItem> CreateAsync(TaskItem task)
    {
        using var connection = connectionFactory.CreateConnection();
        var id = await connection.QuerySingleAsync<Guid>("sp_Tasks_Create", CreateParams(task), commandType: CommandType.StoredProcedure);
        task.Id = id;
        return task;
    }

    private static object CreateParams(TaskItem task) => new
    {
        task.Id,
        task.Title,
        task.Description,
        Status = task.Status.ToString(),
        Priority = task.Priority.ToString(),
        task.AssignedTo,
        TokensUsed = task.Cost?.TokensUsed ?? 0,
        ApiCalls = task.Cost?.ApiCalls ?? 0,
        EstimatedCost = task.Cost?.EstimatedCost ?? 0m,
        Currency = task.Cost?.Currency ?? "USD",
        task.ScheduledAt,
        RecurrenceType = task.RecurrenceType.ToString(),
        task.RecurrenceInterval,
        task.RecurrenceDays,
        task.NextRunAt
    };

    public async Task<TaskItem?> UpdateAsync(TaskItem task)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("sp_Tasks_Update", CreateParams(task), commandType: CommandType.StoredProcedure);
        return task;
    }

    public async Task UpdateStatusAsync(Guid id, TaskItemStatus status)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("sp_Tasks_UpdateStatus", new { Id = id, Status = status.ToString() }, commandType: CommandType.StoredProcedure);
    }

    public async Task DeleteAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("sp_Tasks_Delete", new { Id = id }, commandType: CommandType.StoredProcedure);
    }

    private static TaskItem MapRow(TaskRow r) => new()
    {
        Id = r.Id,
        Title = r.Title ?? "",
        Description = r.Description ?? "",
        Status = ParseStatus(r.Status),
        Priority = ParsePriority(r.Priority),
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
        AssignedTo = r.AssignedTo,
        Cost = new TaskCost
        {
            TokensUsed = r.TokensUsed,
            ApiCalls = r.ApiCalls,
            EstimatedCost = r.EstimatedCost,
            Currency = r.Currency ?? "USD"
        },
        ScheduledAt = r.ScheduledAt,
        RecurrenceType = ParseRecurrence(r.RecurrenceType),
        RecurrenceInterval = r.RecurrenceInterval,
        RecurrenceDays = r.RecurrenceDays,
        NextRunAt = r.NextRunAt,
        LastRunAt = r.LastRunAt
    };

    private static TaskItemStatus ParseStatus(string? s)
    {
        if (string.IsNullOrEmpty(s)) return TaskItemStatus.ToDo;
        if (Enum.TryParse<TaskItemStatus>(s, ignoreCase: true, out var status)) return status;
        return TaskItemStatus.ToDo;
    }

    private static Priority ParsePriority(string? s)
    {
        if (string.IsNullOrEmpty(s)) return Priority.Medium;
        if (Enum.TryParse<Priority>(s, ignoreCase: true, out var priority)) return priority;
        // Handle numeric values: 1=Critical, 2=High, 3=Medium, 4=Low
        if (int.TryParse(s, out var num)) return num switch
        {
            1 => Priority.Critical,
            2 => Priority.High,
            3 => Priority.Medium,
            4 => Priority.Low,
            _ => Priority.Medium
        };
        return Priority.Medium;
    }

    private static RecurrenceType ParseRecurrence(string? s)
    {
        if (string.IsNullOrEmpty(s)) return RecurrenceType.None;
        if (Enum.TryParse<RecurrenceType>(s, ignoreCase: true, out var r)) return r;
        return RecurrenceType.None;
    }

    private class TaskRow
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? AssignedTo { get; set; }
        public int TokensUsed { get; set; }
        public int ApiCalls { get; set; }
        public decimal EstimatedCost { get; set; }
        public string? Currency { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public string? RecurrenceType { get; set; }
        public int RecurrenceInterval { get; set; }
        public string? RecurrenceDays { get; set; }
        public DateTime? NextRunAt { get; set; }
        public DateTime? LastRunAt { get; set; }
    }
}
