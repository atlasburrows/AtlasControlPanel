using System.Data;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.Enums;
using Atlas.Domain.ValueObjects;
using Dapper;

namespace Atlas.Infrastructure.Repositories.Sqlite;

public class SqliteTaskRepository(IDbConnectionFactory connectionFactory) : ITaskRepository
{
    public async Task<IEnumerable<TaskItem>> GetAllAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<TaskRow>("SELECT * FROM Tasks ORDER BY CreatedAt DESC");
        return rows.Select(MapRow);
    }

    public async Task<TaskItem?> GetByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<TaskRow>("SELECT * FROM Tasks WHERE Id = @Id", new { Id = id.ToString() });
        return row is null ? null : MapRow(row);
    }

    public async Task<TaskItem> CreateAsync(TaskItem task)
    {
        task.Id = Guid.NewGuid();
        task.CreatedAt = DateTime.UtcNow;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO Tasks (Id, Title, Description, Status, Priority, CreatedAt, UpdatedAt, AssignedTo, 
                TokensUsed, ApiCalls, EstimatedCost, Currency, ScheduledAt, RecurrenceType, 
                RecurrenceInterval, RecurrenceDays, NextRunAt, LastRunAt)
              VALUES (@Id, @Title, @Description, @Status, @Priority, @CreatedAt, @UpdatedAt, @AssignedTo, 
                @TokensUsed, @ApiCalls, @EstimatedCost, @Currency, @ScheduledAt, @RecurrenceType, 
                @RecurrenceInterval, @RecurrenceDays, @NextRunAt, @LastRunAt)",
            CreateParams(task));

        return task;
    }

    public async Task<TaskItem?> UpdateAsync(TaskItem task)
    {
        task.UpdatedAt = DateTime.UtcNow;

        using var connection = connectionFactory.CreateConnection();
        var rowsAffected = await connection.ExecuteAsync(
            @"UPDATE Tasks 
              SET Title = @Title, Description = @Description, Status = @Status, Priority = @Priority, 
                  UpdatedAt = @UpdatedAt, AssignedTo = @AssignedTo, TokensUsed = @TokensUsed, 
                  ApiCalls = @ApiCalls, EstimatedCost = @EstimatedCost, Currency = @Currency, 
                  ScheduledAt = @ScheduledAt, RecurrenceType = @RecurrenceType, 
                  RecurrenceInterval = @RecurrenceInterval, RecurrenceDays = @RecurrenceDays, 
                  NextRunAt = @NextRunAt, LastRunAt = @LastRunAt
              WHERE Id = @Id",
            CreateParams(task));

        return rowsAffected > 0 ? task : null;
    }

    public async Task UpdateStatusAsync(Guid id, TaskItemStatus status)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Tasks SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id",
            new { Status = status.ToString(), UpdatedAt = DateTime.UtcNow.ToString("O"), Id = id.ToString() });
    }

    public async Task DeleteAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM Tasks WHERE Id = @Id", new { Id = id.ToString() });
    }

    private static object CreateParams(TaskItem task) => new
    {
        Id = task.Id.ToString(),
        task.Title,
        task.Description,
        Status = task.Status.ToString(),
        Priority = task.Priority.ToString(),
        CreatedAt = task.CreatedAt.ToString("O"),
        UpdatedAt = task.UpdatedAt?.ToString("O"),
        task.AssignedTo,
        TokensUsed = task.Cost?.TokensUsed ?? 0,
        ApiCalls = task.Cost?.ApiCalls ?? 0,
        EstimatedCost = task.Cost?.EstimatedCost ?? 0m,
        Currency = task.Cost?.Currency ?? "USD",
        ScheduledAt = task.ScheduledAt?.ToString("O"),
        RecurrenceType = task.RecurrenceType.ToString(),
        task.RecurrenceInterval,
        task.RecurrenceDays,
        NextRunAt = task.NextRunAt?.ToString("O"),
        LastRunAt = task.LastRunAt?.ToString("O")
    };

    private static TaskItem MapRow(TaskRow r) => new()
    {
        Id = Guid.Parse(r.Id),
        Title = r.Title ?? "",
        Description = r.Description ?? "",
        Status = ParseStatus(r.Status),
        Priority = ParsePriority(r.Priority),
        CreatedAt = DateTime.Parse(r.CreatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
        UpdatedAt = string.IsNullOrEmpty(r.UpdatedAt) ? null : DateTime.Parse(r.UpdatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
        AssignedTo = r.AssignedTo,
        Cost = new TaskCost
        {
            TokensUsed = r.TokensUsed,
            ApiCalls = r.ApiCalls,
            EstimatedCost = r.EstimatedCost,
            Currency = r.Currency ?? "USD"
        },
        ScheduledAt = string.IsNullOrEmpty(r.ScheduledAt) ? null : DateTime.Parse(r.ScheduledAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
        RecurrenceType = ParseRecurrence(r.RecurrenceType),
        RecurrenceInterval = r.RecurrenceInterval,
        RecurrenceDays = r.RecurrenceDays,
        NextRunAt = string.IsNullOrEmpty(r.NextRunAt) ? null : DateTime.Parse(r.NextRunAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
        LastRunAt = string.IsNullOrEmpty(r.LastRunAt) ? null : DateTime.Parse(r.LastRunAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
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
        public string Id { get; set; } = "";
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public string CreatedAt { get; set; } = "";
        public string? UpdatedAt { get; set; }
        public string? AssignedTo { get; set; }
        public int TokensUsed { get; set; }
        public int ApiCalls { get; set; }
        public decimal EstimatedCost { get; set; }
        public string? Currency { get; set; }
        public string? ScheduledAt { get; set; }
        public string? RecurrenceType { get; set; }
        public int RecurrenceInterval { get; set; }
        public string? RecurrenceDays { get; set; }
        public string? NextRunAt { get; set; }
        public string? LastRunAt { get; set; }
    }
}
