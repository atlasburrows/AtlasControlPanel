using System.Data;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.Enums;
using Dapper;

namespace Atlas.Infrastructure.Repositories.Sqlite;

public class SqliteSecurityRepository(IDbConnectionFactory connectionFactory) : ISecurityRepository
{
    public async Task<IEnumerable<PermissionRequest>> GetAllPermissionRequestsAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<PermissionRequestRow>(
            "SELECT * FROM PermissionRequests ORDER BY RequestedAt DESC");
        return rows.Select(MapPermissionRow);
    }

    public async Task<IEnumerable<PermissionRequest>> GetPendingPermissionRequestsAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<PermissionRequestRow>(
            "SELECT * FROM PermissionRequests WHERE Status = @Status ORDER BY RequestedAt DESC",
            new { Status = PermissionStatus.Pending.ToString() });
        return rows.Select(MapPermissionRow);
    }

    public async Task<PermissionRequest> CreatePermissionRequestAsync(PermissionRequest request)
    {
        request.Id = Guid.NewGuid();
        request.RequestedAt = DateTime.UtcNow;
        request.Status = PermissionStatus.Pending;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO PermissionRequests (Id, RequestType, Description, Status, RequestedAt, ResolvedAt, 
                ResolvedBy, Urgency, CredentialId, Category, ExpiresAt)
              VALUES (@Id, @RequestType, @Description, @Status, @RequestedAt, @ResolvedAt, @ResolvedBy, 
                @Urgency, @CredentialId, @Category, @ExpiresAt)",
            CreatePermissionParams(request));

        return request;
    }

    public async Task<PermissionRequest?> UpdatePermissionRequestAsync(Guid id, PermissionStatus status, string resolvedBy)
    {
        using var connection = connectionFactory.CreateConnection();
        var now = DateTime.UtcNow.ToString("O");

        await connection.ExecuteAsync(
            @"UPDATE PermissionRequests 
              SET Status = @Status, ResolvedAt = @ResolvedAt, ResolvedBy = @ResolvedBy
              WHERE Id = @Id",
            new { Status = status.ToString(), ResolvedAt = now, ResolvedBy = resolvedBy, Id = id.ToString() });

        // Return the updated record
        var row = await connection.QuerySingleOrDefaultAsync<PermissionRequestRow>(
            "SELECT * FROM PermissionRequests WHERE Id = @Id",
            new { Id = id.ToString() });

        return row is null ? null : MapPermissionRow(row);
    }

    public async Task<IEnumerable<SecurityAudit>> GetAllAuditsAsync(int take = 100)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<SecurityAuditRow>(
            "SELECT * FROM SecurityAudits ORDER BY Timestamp DESC LIMIT @Take",
            new { Take = take });
        return rows.Select(MapAuditRow);
    }

    public async Task<SecurityAudit> CreateAuditAsync(SecurityAudit audit)
    {
        audit.Id = Guid.NewGuid();
        audit.Timestamp = DateTime.UtcNow;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO SecurityAudits (Id, Action, Severity, Details, Timestamp)
              VALUES (@Id, @Action, @Severity, @Details, @Timestamp)",
            CreateAuditParams(audit));

        return audit;
    }

    public async Task<IEnumerable<SecurityAudit>> GetAuditsBySeverityAsync(Severity severity)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<SecurityAuditRow>(
            "SELECT * FROM SecurityAudits WHERE Severity = @Severity ORDER BY Timestamp DESC",
            new { Severity = severity.ToString() });
        return rows.Select(MapAuditRow);
    }

    private static object CreatePermissionParams(PermissionRequest request) => new
    {
        Id = request.Id.ToString(),
        RequestType = request.RequestType.ToString(),
        request.Description,
        Status = request.Status.ToString(),
        RequestedAt = request.RequestedAt.ToString("O"),
        ResolvedAt = request.ResolvedAt?.ToString("O"),
        request.ResolvedBy,
        request.Urgency,
        CredentialId = request.CredentialId?.ToString(),
        request.Category,
        ExpiresAt = request.ExpiresAt?.ToString("O")
    };

    private static object CreateAuditParams(SecurityAudit audit) => new
    {
        Id = audit.Id.ToString(),
        audit.Action,
        Severity = audit.Severity.ToString(),
        audit.Details,
        Timestamp = audit.Timestamp.ToString("O")
    };

    private static PermissionRequest MapPermissionRow(PermissionRequestRow r) => new()
    {
        Id = Guid.Parse(r.Id),
        RequestType = ParseRequestType(r.RequestType),
        Description = r.Description ?? "",
        Status = ParseStatus(r.Status),
        RequestedAt = DateTime.Parse(r.RequestedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
        ResolvedAt = string.IsNullOrEmpty(r.ResolvedAt) ? null : DateTime.Parse(r.ResolvedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
        ResolvedBy = r.ResolvedBy,
        Urgency = r.Urgency,
        CredentialId = string.IsNullOrEmpty(r.CredentialId) ? null : Guid.Parse(r.CredentialId),
        Category = r.Category,
        ExpiresAt = string.IsNullOrEmpty(r.ExpiresAt) ? null : DateTime.Parse(r.ExpiresAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
    };

    private static SecurityAudit MapAuditRow(SecurityAuditRow r) => new()
    {
        Id = Guid.Parse(r.Id),
        Action = r.Action ?? "",
        Severity = ParseSeverity(r.Severity),
        Details = r.Details,
        Timestamp = DateTime.Parse(r.Timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind)
    };

    private static PermissionRequestType ParseRequestType(string? s)
    {
        if (string.IsNullOrEmpty(s)) return PermissionRequestType.CredentialAccess;
        if (Enum.TryParse<PermissionRequestType>(s, ignoreCase: true, out var rt)) return rt;
        return PermissionRequestType.CredentialAccess;
    }

    private static PermissionStatus ParseStatus(string? s)
    {
        if (string.IsNullOrEmpty(s)) return PermissionStatus.Pending;
        if (Enum.TryParse<PermissionStatus>(s, ignoreCase: true, out var ps)) return ps;
        return PermissionStatus.Pending;
    }

    private static Severity ParseSeverity(string? s)
    {
        if (string.IsNullOrEmpty(s)) return Severity.Info;
        if (Enum.TryParse<Severity>(s, ignoreCase: true, out var sev)) return sev;
        return Severity.Info;
    }

    private class PermissionRequestRow
    {
        public string Id { get; set; } = "";
        public string? RequestType { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string RequestedAt { get; set; } = "";
        public string? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }
        public string? Urgency { get; set; }
        public string? CredentialId { get; set; }
        public string? Category { get; set; }
        public string? ExpiresAt { get; set; }
    }

    private class SecurityAuditRow
    {
        public string Id { get; set; } = "";
        public string? Action { get; set; }
        public string? Severity { get; set; }
        public string? Details { get; set; }
        public string Timestamp { get; set; } = "";
    }
}
