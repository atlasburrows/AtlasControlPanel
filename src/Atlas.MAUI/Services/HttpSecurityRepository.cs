using System.Net.Http.Json;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
namespace Atlas.MAUI.Services;

public class HttpSecurityRepository(HttpClient http) : ISecurityRepository
{
    public async Task<IEnumerable<PermissionRequest>> GetAllPermissionRequestsAsync()
        => await http.GetFromJsonAsync<List<PermissionRequest>>("api/security/permissions") ?? [];

    public async Task<IEnumerable<PermissionRequest>> GetPendingPermissionRequestsAsync()
        => await http.GetFromJsonAsync<List<PermissionRequest>>("api/security/permissions/pending") ?? [];

    public async Task<PermissionRequest> CreatePermissionRequestAsync(PermissionRequest request)
    {
        var response = await http.PostAsJsonAsync("api/security/permissions", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PermissionRequest>())!;
    }

    public async Task<PermissionRequest?> UpdatePermissionRequestAsync(Guid id, Atlas.Domain.Enums.PermissionStatus status, string resolvedBy)
    {
        var response = await http.PutAsJsonAsync($"api/security/permissions/{id}", new { Status = status, ResolvedBy = resolvedBy });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PermissionRequest>();
    }

    public async Task<IEnumerable<SecurityAudit>> GetAllAuditsAsync(int take = 100)
        => await http.GetFromJsonAsync<List<SecurityAudit>>($"api/security/audits?take={take}") ?? [];

    public async Task<SecurityAudit> CreateAuditAsync(SecurityAudit audit)
    {
        var response = await http.PostAsJsonAsync("api/security/audits", audit);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SecurityAudit>())!;
    }

    public async Task<IEnumerable<SecurityAudit>> GetAuditsBySeverityAsync(Atlas.Domain.Enums.Severity severity)
        => await http.GetFromJsonAsync<List<SecurityAudit>>($"api/security/audits?severity={severity}") ?? [];
}
