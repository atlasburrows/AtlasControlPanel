using Atlas.Domain.Entities;
using Atlas.Domain.Enums;

namespace Atlas.Application.Common.Interfaces;

public interface ISecurityRepository
{
    Task<IEnumerable<PermissionRequest>> GetAllPermissionRequestsAsync();
    Task<IEnumerable<PermissionRequest>> GetPendingPermissionRequestsAsync();
    Task<PermissionRequest> CreatePermissionRequestAsync(PermissionRequest request);
    Task<PermissionRequest?> UpdatePermissionRequestAsync(Guid id, PermissionStatus status, string resolvedBy);
    Task<IEnumerable<SecurityAudit>> GetAllAuditsAsync(int take = 100);
    Task<SecurityAudit> CreateAuditAsync(SecurityAudit audit);
    Task<IEnumerable<SecurityAudit>> GetAuditsBySeverityAsync(Severity severity);
}
