using System.Data;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.Enums;
using Atlas.Domain.ValueObjects;
using Dapper;

namespace Atlas.Infrastructure.Repositories.Sqlite;

public class SqliteActivityRepository(IDbConnectionFactory connectionFactory) : IActivityRepository
{
    public async Task<IEnumerable<ActivityLog>> GetAllAsync(int take = 50)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ActivityLogRow>(
            "SELECT * FROM ActivityLogs ORDER BY Timestamp DESC LIMIT @Take",
            new { Take = take });
        return rows.Select(MapRow);
    }

    public async Task<ActivityLog?> GetByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<ActivityLogRow>(
            "SELECT * FROM ActivityLogs WHERE Id = @Id",
            new { Id = id.ToString() });
        return row is null ? null : MapRow(row);
    }

    public async Task<ActivityLog> CreateAsync(ActivityLog log)
    {
        log.Id = Guid.NewGuid();
        log.Timestamp = DateTime.UtcNow;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO ActivityLogs (Id, Action, Description, Timestamp, Category, TokensUsed, ApiCalls, 
                EstimatedCost, Currency, RelatedTaskId, ParentId, Details)
              VALUES (@Id, @Action, @Description, @Timestamp, @Category, @TokensUsed, @ApiCalls, 
                @EstimatedCost, @Currency, @RelatedTaskId, @ParentId, @Details)",
            CreateParams(log));

        return log;
    }

    public async Task<IEnumerable<ActivityLog>> GetByTaskIdAsync(Guid taskId)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ActivityLogRow>(
            "SELECT * FROM ActivityLogs WHERE RelatedTaskId = @TaskId ORDER BY Timestamp DESC",
            new { TaskId = taskId.ToString() });
        return rows.Select(MapRow);
    }

    private static object CreateParams(ActivityLog log) => new
    {
        Id = log.Id.ToString(),
        log.Action,
        log.Description,
        Timestamp = log.Timestamp.ToString("O"),
        Category = log.Category.ToString(),
        TokensUsed = log.CostInfo?.TokensUsed ?? 0,
        ApiCalls = log.CostInfo?.ApiCalls ?? 0,
        EstimatedCost = log.CostInfo?.EstimatedCost ?? 0m,
        Currency = log.CostInfo?.Currency ?? "USD",
        RelatedTaskId = log.RelatedTaskId?.ToString(),
        ParentId = log.ParentId?.ToString(),
        log.Details
    };

    private static ActivityLog MapRow(ActivityLogRow r) => new()
    {
        Id = Guid.Parse(r.Id),
        Action = r.Action ?? "",
        Description = r.Description ?? "",
        Timestamp = DateTime.Parse(r.Timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind),
        Category = ParseCategory(r.Category),
        RelatedTaskId = string.IsNullOrEmpty(r.RelatedTaskId) ? null : Guid.Parse(r.RelatedTaskId),
        ParentId = string.IsNullOrEmpty(r.ParentId) ? null : Guid.Parse(r.ParentId),
        Details = r.Details,
        CostInfo = new TaskCost
        {
            TokensUsed = r.TokensUsed,
            ApiCalls = r.ApiCalls,
            EstimatedCost = r.EstimatedCost,
            Currency = r.Currency ?? "USD"
        }
    };

    private static ActivityCategory ParseCategory(string? s)
    {
        if (string.IsNullOrEmpty(s)) return ActivityCategory.FileAccess;
        if (Enum.TryParse<ActivityCategory>(s, ignoreCase: true, out var cat)) return cat;
        return ActivityCategory.FileAccess;
    }

    private class ActivityLogRow
    {
        public string Id { get; set; } = "";
        public string? Action { get; set; }
        public string? Description { get; set; }
        public string Timestamp { get; set; } = "";
        public string? Category { get; set; }
        public int TokensUsed { get; set; }
        public int ApiCalls { get; set; }
        public decimal EstimatedCost { get; set; }
        public string? Currency { get; set; }
        public string? RelatedTaskId { get; set; }
        public string? ParentId { get; set; }
        public string? Details { get; set; }
    }
}
