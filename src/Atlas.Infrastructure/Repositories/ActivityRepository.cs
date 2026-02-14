using System.Data;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.Enums;
using Atlas.Domain.ValueObjects;
using Dapper;

namespace Atlas.Infrastructure.Repositories;

public class ActivityRepository(IDbConnectionFactory connectionFactory) : IActivityRepository
{
    public async Task<IEnumerable<ActivityLog>> GetAllAsync(int take = 50)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ActivityLogRow>("sp_ActivityLogs_GetAll", new { Take = take }, commandType: CommandType.StoredProcedure);
        return rows.Select(MapRow);
    }

    public async Task<ActivityLog?> GetByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<ActivityLogRow>("sp_ActivityLogs_GetById", new { Id = id }, commandType: CommandType.StoredProcedure);
        return row is null ? null : MapRow(row);
    }

    public async Task<ActivityLog> CreateAsync(ActivityLog log)
    {
        using var connection = connectionFactory.CreateConnection();
        var id = await connection.QuerySingleAsync<Guid>("sp_ActivityLogs_Create", new
        {
            log.Action,
            log.Description,
            Category = log.Category.ToString(),
            TokensUsed = log.CostInfo?.TokensUsed ?? 0,
            ApiCalls = log.CostInfo?.ApiCalls ?? 0,
            EstimatedCost = log.CostInfo?.EstimatedCost ?? 0m,
            Currency = log.CostInfo?.Currency ?? "USD",
            log.RelatedTaskId,
            log.ParentId,
            log.Details
        }, commandType: CommandType.StoredProcedure);
        log.Id = id;
        return log;
    }

    public async Task<IEnumerable<ActivityLog>> GetByTaskIdAsync(Guid taskId)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ActivityLogRow>("sp_ActivityLogs_GetByTaskId", new { TaskId = taskId }, commandType: CommandType.StoredProcedure);
        return rows.Select(MapRow);
    }

    private static ActivityLog MapRow(ActivityLogRow r) => new()
    {
        Id = r.Id,
        Action = r.Action ?? "",
        Description = r.Description ?? "",
        Timestamp = r.Timestamp,
        Category = Enum.TryParse<ActivityCategory>(r.Category, out var cat) ? cat : ActivityCategory.FileAccess,
        RelatedTaskId = r.RelatedTaskId,
        ParentId = r.ParentId,
        Details = r.Details,
        CostInfo = new TaskCost
        {
            TokensUsed = r.TokensUsed,
            ApiCalls = r.ApiCalls,
            EstimatedCost = r.EstimatedCost,
            Currency = r.Currency ?? "USD"
        }
    };

    private class ActivityLogRow
    {
        public Guid Id { get; set; }
        public string? Action { get; set; }
        public string? Description { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Category { get; set; }
        public Guid? RelatedTaskId { get; set; }
        public Guid? ParentId { get; set; }
        public string? Details { get; set; }
        public int TokensUsed { get; set; }
        public int ApiCalls { get; set; }
        public decimal EstimatedCost { get; set; }
        public string? Currency { get; set; }
    }
}
