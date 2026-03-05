using Vigil.Application.Common.Interfaces;
using Vigil.Domain.Entities;
using Vigil.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Vigil.API.Controllers;

[ApiController]
[Route("api/security")]
public class SecurityController(ISecurityRepository securityRepository) : ControllerBase
{
    [HttpGet("permissions")]
    public async Task<ActionResult<IEnumerable<PermissionRequest>>> GetPermissions()
        => Ok(await securityRepository.GetAllPermissionRequestsAsync());

    [HttpGet("permissions/pending")]
    public async Task<ActionResult<IEnumerable<PermissionRequest>>> GetPending()
        => Ok(await securityRepository.GetPendingPermissionRequestsAsync());

    [HttpGet("permissions/{id:guid}")]
    public async Task<ActionResult<PermissionRequest>> GetPermission(Guid id)
    {
        var permissions = await securityRepository.GetAllPermissionRequestsAsync();
        var permission = permissions.FirstOrDefault(p => p.Id == id);
        return permission is null ? NotFound() : Ok(permission);
    }

    [HttpPost("permissions")]
    public async Task<ActionResult<PermissionRequest>> CreatePermission(PermissionRequest request)
    {
        var created = await securityRepository.CreatePermissionRequestAsync(request);
        return Created($"api/security/permissions/{created.Id}", created);
    }

    [HttpPut("permissions/{id:guid}")]
    public async Task<ActionResult<PermissionRequest>> UpdatePermission(Guid id, [FromBody] UpdatePermissionDto dto)
    {
        var updated = await securityRepository.UpdatePermissionRequestAsync(id, dto.Status, dto.ResolvedBy);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("credentials/request")]
    public async Task<ActionResult<PermissionRequest>> RequestCredential([FromBody] CredentialRequestDto dto)
    {
        var request = new PermissionRequest
        {
            RequestType = PermissionRequestType.CredentialAccess,
            Description = $"Access requested for '{dto.CredentialName}': {dto.Reason}",
            Status = PermissionStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            Urgency = "Normal",
            Category = "Credential",
            ExpiresAt = DateTime.UtcNow.AddMinutes(dto.DurationMinutes ?? 30)
        };
        var created = await securityRepository.CreatePermissionRequestAsync(request);
        
        // Log the access attempt as a security audit
        await securityRepository.CreateAuditAsync(new SecurityAudit
        {
            Action = "CredentialAccessRequest",
            Details = $"Bot requested access to '{dto.CredentialName}': {dto.Reason} (Duration: {dto.DurationMinutes ?? 30}min)",
            Severity = Severity.Warning
        });
        
        return Created($"api/security/permissions/{created.Id}", created);
    }

    /// <summary>
    /// Request access to a credential group. Creates a single permission request for all credentials in the group.
    /// </summary>
    [HttpPost("credentials/request-group")]
    public async Task<ActionResult<PermissionRequest>> RequestCredentialGroup(
        [FromBody] CredentialGroupRequestDto dto,
        [FromServices] ICredentialGroupRepository groupRepository)
    {
        // Find the group by name
        var group = await groupRepository.GetByNameAsync(dto.GroupName);
        if (group == null)
            return NotFound(new { error = $"Credential group '{dto.GroupName}' not found" });

        // Get all active credentials in the group
        var credentials = await groupRepository.GetCredentialsInGroupAsync(group.Id);
        var activeCredentials = credentials
            .Where(c => !c.Description?.ToLower().Contains("deprecated") == true)
            .ToList();

        if (activeCredentials.Count == 0)
            return BadRequest(new { error = "No active credentials found in this group" });

        // Create a single permission request for the group
        var request = new PermissionRequest
        {
            RequestType = PermissionRequestType.CredentialAccess,
            Description = $"Access requested for '{dto.GroupName}' group ({activeCredentials.Count} credentials): {dto.Reason}",
            Status = PermissionStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            Urgency = "Normal",
            CredentialGroupId = group.Id,
            Category = "Credential",
            ExpiresAt = DateTime.UtcNow.AddMinutes(dto.DurationMinutes ?? 30)
        };
        var created = await securityRepository.CreatePermissionRequestAsync(request);
        
        // Log the access attempt as a security audit
        await securityRepository.CreateAuditAsync(new SecurityAudit
        {
            Action = "CredentialGroupAccessRequest",
            Details = $"Bot requested access to '{dto.GroupName}' group with {activeCredentials.Count} credentials: {dto.Reason} (Duration: {dto.DurationMinutes ?? 30}min)",
            Severity = Severity.Warning
        });
        
        return Created($"api/security/permissions/{created.Id}", new { 
            requestId = created.Id, 
            groupName = dto.GroupName, 
            credentialCount = activeCredentials.Count,
            credentialNames = activeCredentials.Select(c => c.Name).ToList()
        });
    }

    [HttpGet("audits")]
    public async Task<ActionResult<IEnumerable<SecurityAudit>>> GetAudits([FromQuery] int take = 100)
        => Ok(await securityRepository.GetAllAuditsAsync(take));

    [HttpPost("audits")]
    public async Task<ActionResult<SecurityAudit>> CreateAudit(SecurityAudit audit)
    {
        var created = await securityRepository.CreateAuditAsync(audit);
        return Created($"api/security/audits/{created.Id}", created);
    }
}

public record UpdatePermissionDto(PermissionStatus Status, string ResolvedBy);
public record CredentialRequestDto(string CredentialName, string Reason, int? DurationMinutes);
public record CredentialGroupRequestDto(string GroupName, string Reason, int? DurationMinutes);
