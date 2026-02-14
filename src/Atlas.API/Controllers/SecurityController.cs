using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.API.Controllers;

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
