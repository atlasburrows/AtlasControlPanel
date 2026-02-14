using System.Data;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.Enums;
using Dapper;

namespace Atlas.Infrastructure.Repositories;

public class SecurityRepository(IDbConnectionFactory connectionFactory) : ISecurityRepository
{
    public async Task<IEnumerable<PermissionRequest>> GetAllPermissionRequestsAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<PermissionRequestRow>("sp_PermissionRequests_GetAll", commandType: CommandType.StoredProcedure);
        return rows.Select(MapPermissionRow);
    }

    public async Task<IEnumerable<PermissionRequest>> GetPendingPermissionRequestsAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<PermissionRequestRow>("sp_PermissionRequests_GetPending", commandType: CommandType.StoredProcedure);
        return rows.Select(MapPermissionRow);
    }

    public async Task<PermissionRequest> CreatePermissionRequestAsync(PermissionRequest request)
    {
        using var connection = connectionFactory.CreateConnection();
        var id = await connection.QuerySingleAsync<Guid>("sp_PermissionRequests_Create", new
        {
            RequestType = request.RequestType.ToString(),
            request.Description,
            request.Urgency,
            request.CredentialId,
            request.Category,
            request.ExpiresAt
        }, commandType: CommandType.StoredProcedure);
        request.Id = id;
        return request;
    }

    public async Task<PermissionRequest?> UpdatePermissionRequestAsync(Guid id, PermissionStatus status, string resolvedBy)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<PermissionRequestRow>("sp_PermissionRequests_Resolve", new
        {
            Id = id,
            Status = status.ToString(),
            ResolvedBy = resolvedBy
        }, commandType: CommandType.StoredProcedure);
        return row is null ? null : MapPermissionRow(row);
    }

    public async Task<IEnumerable<SecurityAudit>> GetAllAuditsAsync(int take = 100)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<SecurityAuditRow>("sp_SecurityAudits_GetAll", new { Take = take }, commandType: CommandType.StoredProcedure);
        return rows.Select(MapAuditRow);
    }

    public async Task<SecurityAudit> CreateAuditAsync(SecurityAudit audit)
    {
        using var connection = connectionFactory.CreateConnection();
        var id = await connection.QuerySingleAsync<Guid>("sp_SecurityAudits_Create", new
        {
            audit.Action,
            Severity = audit.Severity.ToString(),
            audit.Details
        }, commandType: CommandType.StoredProcedure);
        audit.Id = id;
        return audit;
    }

    public async Task<IEnumerable<SecurityAudit>> GetAuditsBySeverityAsync(Severity severity)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<SecurityAuditRow>("sp_SecurityAudits_GetBySeverity", new { Severity = severity.ToString() }, commandType: CommandType.StoredProcedure);
        return rows.Select(MapAuditRow);
    }

    private static PermissionRequest MapPermissionRow(PermissionRequestRow r) => new()
    {
        Id = r.Id,
        RequestType = Enum.TryParse<PermissionRequestType>(r.RequestType, ignoreCase: true, out var rt) ? rt : PermissionRequestType.CredentialAccess,
        Description = r.Description ?? "",
        Status = Enum.TryParse<PermissionStatus>(r.Status, ignoreCase: true, out var ps) ? ps : PermissionStatus.Pending,
        RequestedAt = r.RequestedAt,
        ResolvedAt = r.ResolvedAt,
        ResolvedBy = r.ResolvedBy,
        Urgency = r.Urgency,
        CredentialId = r.CredentialId,
        Category = r.Category,
        ExpiresAt = r.ExpiresAt
    };

    private static SecurityAudit MapAuditRow(SecurityAuditRow r) => new()
    {
        Id = r.Id,
        Action = r.Action ?? "",
        Severity = Enum.TryParse<Severity>(r.Severity, ignoreCase: true, out var sev) ? sev : Severity.Info,
        Details = r.Details,
        Timestamp = r.Timestamp
    };

    private class PermissionRequestRow
    {
        public Guid Id { get; set; }
        public string? RequestType { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }
        public string? Urgency { get; set; }
        public Guid? CredentialId { get; set; }
        public string? Category { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    private class SecurityAuditRow
    {
        public Guid Id { get; set; }
        public string? Action { get; set; }
        public string? Severity { get; set; }
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
