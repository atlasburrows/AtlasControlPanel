using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.Enums;
using Atlas.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Controllers;

[ApiController]
[Route("api/security")]
public class SecurityController(
    ISecurityRepository securityRepository,
    ICredentialRepository credentialRepository,
    ICredentialAccessLogRepository accessLogRepository,
    ICredentialGroupRepository credentialGroupRepository,
    TelegramNotificationService telegram) : ControllerBase
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
        // Fix 3: Block credential-type approval via this general endpoint
        if (dto.Status == PermissionStatus.Approved)
        {
            var permissions = await securityRepository.GetAllPermissionRequestsAsync();
            var existing = permissions.FirstOrDefault(p => p.Id == id);
            if (existing is not null && existing.RequestType == PermissionRequestType.CredentialAccess)
            {
                return StatusCode(403, new
                {
                    error = "Credential requests cannot be approved through this endpoint. Use the dedicated notification approve endpoint with a valid HMAC token."
                });
            }
        }

        var updated = await securityRepository.UpdatePermissionRequestAsync(id, dto.Status, dto.ResolvedBy);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpGet("credentials")]
    public async Task<ActionResult<IEnumerable<SecureCredential>>> GetCredentials()
        => Ok(await credentialRepository.GetAllAsync());

    [HttpPost("credentials/request")]
    public async Task<ActionResult<PermissionRequest>> RequestCredential([FromBody] CredentialRequestDto dto)
    {
        // If a groupName is provided, create a single permission request for the entire group
        if (!string.IsNullOrEmpty(dto.GroupName))
        {
            var group = await credentialGroupRepository.GetByNameAsync(dto.GroupName);
            if (group is null)
                return NotFound($"Credential group '{dto.GroupName}' not found");

            var groupCredentials = (await credentialGroupRepository.GetCredentialsInGroupAsync(group.Id)).ToList();
            if (groupCredentials.Count == 0)
                return BadRequest("Group has no credentials");

            var credNames = string.Join(", ", groupCredentials.Select(c => c.Name));
            var groupRequest = new PermissionRequest
            {
                RequestType = PermissionRequestType.CredentialAccess,
                Description = $"Group access requested for '{group.Name}' ({credNames}): {dto.Reason}",
                Status = PermissionStatus.Pending,
                RequestedAt = DateTime.UtcNow,
                Urgency = "Normal",
                Category = $"CredentialGroup:{group.Name}",
                ExpiresAt = DateTime.UtcNow.AddMinutes(dto.DurationMinutes ?? 30),
                CredentialId = groupCredentials.First().Id
            };
            var createdGroup = await securityRepository.CreatePermissionRequestAsync(groupRequest);

            await securityRepository.CreateAuditAsync(new SecurityAudit
            {
                Action = "CredentialGroupAccessRequest",
                Details = $"Group '{group.Name}' access requested ({groupCredentials.Count} credentials): {dto.Reason}",
                Severity = Severity.Warning
            });

            return Created($"api/security/permissions/{createdGroup.Id}", createdGroup);
        }

        var credentialId = Guid.Empty;
        var vaultMode = "locked";
        
        // Try to find the credential to check its vault mode
        var cred = await credentialRepository.GetByNameAsync(dto.CredentialName);
        if (cred is not null)
        {
            credentialId = cred.Id;
            vaultMode = await credentialRepository.GetVaultModeAsync(cred.Id) ?? "locked";
        }

        // If the credential is in unlocked mode, auto-approve and log it
        if (vaultMode == "unlocked" && cred is not null)
        {
            // Log the access with auto-approval
            var log = new CredentialAccessLog
            {
                Id = Guid.NewGuid(),
                CredentialId = cred.Id,
                CredentialName = dto.CredentialName,
                Requester = "System/Bot",
                AccessedAt = DateTime.UtcNow,
                VaultMode = "unlocked",
                AutoApproved = true,
                Details = $"Auto-approved access for '{dto.CredentialName}': {dto.Reason}"
            };
            await accessLogRepository.CreateAsync(log);

            // Still create an audit log
            await securityRepository.CreateAuditAsync(new SecurityAudit
            {
                Action = "CredentialAccessAutoApproved",
                Details = $"Unlocked credential access auto-approved for '{dto.CredentialName}': {dto.Reason}",
                Severity = Severity.Info
            });

            // Return a successful response
            return Ok(new { message = "Access auto-approved", credentialId = cred.Id, autoApproved = true });
        }

        // For locked mode, create a permission request as before
        var request = new PermissionRequest
        {
            RequestType = PermissionRequestType.CredentialAccess,
            Description = $"Access requested for '{dto.CredentialName}': {dto.Reason}",
            Status = PermissionStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            Urgency = "Normal",
            Category = "Credential",
            ExpiresAt = DateTime.UtcNow.AddMinutes(dto.DurationMinutes ?? 30),
            CredentialId = credentialId != Guid.Empty ? credentialId : null
        };
        var created = await securityRepository.CreatePermissionRequestAsync(request);
        
        // Log the access attempt as a security audit
        await securityRepository.CreateAuditAsync(new SecurityAudit
        {
            Action = "CredentialAccessRequest",
            Details = $"Bot requested access to '{dto.CredentialName}': {dto.Reason} (Duration: {dto.DurationMinutes ?? 30}min)",
            Severity = Severity.Warning
        });

        // Log it to the access log for audit trail
        if (cred is not null)
        {
            var log = new CredentialAccessLog
            {
                Id = Guid.NewGuid(),
                CredentialId = cred.Id,
                CredentialName = dto.CredentialName,
                Requester = "System/Bot",
                AccessedAt = DateTime.UtcNow,
                VaultMode = "locked",
                AutoApproved = false,
                Details = $"Access request pending approval for '{dto.CredentialName}': {dto.Reason}"
            };
            await accessLogRepository.CreateAsync(log);
        }

        // Telegram push notification is handled by the OpenClaw plugin (uses public URL)
        // Do NOT send a duplicate from the server (which uses localhost)
        
        return Created($"api/security/permissions/{created.Id}", created);
    }

    /// <summary>
    /// Webhook for Telegram inline button callbacks (approve/deny credential requests).
    /// </summary>
    [HttpPost("credentials/callback")]
    public async Task<IActionResult> HandleCredentialCallback([FromBody] TelegramCallbackDto dto)
    {
        if (string.IsNullOrEmpty(dto.Data) || !dto.Data.Contains(':'))
            return Ok();

        var parts = dto.Data.Split(':', 2);
        var action = parts[0]; // "cred_approve" or "cred_deny"
        if (!Guid.TryParse(parts[1], out var requestId))
            return Ok();

        var approved = action == "cred_approve";
        var status = approved ? PermissionStatus.Approved : PermissionStatus.Denied;
        var resolvedBy = dto.FromUsername ?? dto.FromId ?? "Owner";

        var updated = await securityRepository.UpdatePermissionRequestAsync(requestId, status, resolvedBy);
        if (updated is null)
        {
            if (!string.IsNullOrEmpty(dto.CallbackQueryId))
                await telegram.AnswerCallbackQuery(dto.CallbackQueryId, "Request not found");
            return Ok();
        }

        // Log the decision
        await securityRepository.CreateAuditAsync(new SecurityAudit
        {
            Action = approved ? "CredentialApproved" : "CredentialDenied",
            Details = $"Request {requestId} {(approved ? "approved" : "denied")} by {resolvedBy}",
            Severity = approved ? Severity.Warning : Severity.Info
        });

        // Update the Telegram message (remove buttons, show result)
        if (!string.IsNullOrEmpty(dto.MessageId))
        {
            // Extract credential name from description
            var credName = updated.Description?.Split('\'').ElementAtOrDefault(1) ?? "Unknown";
            await telegram.UpdateCredentialNotification(dto.MessageId, credName, approved, resolvedBy);
        }

        // Answer the callback query
        if (!string.IsNullOrEmpty(dto.CallbackQueryId))
            await telegram.AnswerCallbackQuery(dto.CallbackQueryId, approved ? "✅ Approved" : "❌ Denied");

        return Ok();
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

    [HttpPut("credentials/{id:guid}/vault-mode")]
    public async Task<ActionResult> UpdateCredentialVaultMode(Guid id, [FromBody] UpdateVaultModeDto dto)
    {
        var credential = await credentialRepository.GetByIdAsync(id);
        if (credential is null)
            return NotFound();

        var validModes = new[] { "locked", "unlocked" };
        if (!validModes.Contains(dto.VaultMode?.ToLower()))
            return BadRequest("VaultMode must be 'locked' or 'unlocked'");

        await credentialRepository.UpdateVaultModeAsync(id, dto.VaultMode!.ToLower());

        // Log the mode change
        await securityRepository.CreateAuditAsync(new SecurityAudit
        {
            Action = "CredentialVaultModeChanged",
            Details = $"Credential '{credential.Name}' vault mode changed to '{dto.VaultMode}'",
            Severity = Severity.Warning
        });

        return Ok(new { id, vaultMode = dto.VaultMode?.ToLower() });
    }

    [HttpGet("credentials/{id:guid}/vault-mode")]
    public async Task<ActionResult> GetCredentialVaultMode(Guid id)
    {
        var vaultMode = await credentialRepository.GetVaultModeAsync(id);
        if (vaultMode is null)
            return NotFound();

        return Ok(new { vaultMode });
    }

    [HttpGet("credentials/{id:guid}/access-logs")]
    public async Task<ActionResult<IEnumerable<CredentialAccessLog>>> GetCredentialAccessLogs(Guid id)
    {
        var logs = await accessLogRepository.GetByCredentialIdAsync(id);
        return Ok(logs);
    }

    [HttpGet("access-logs")]
    public async Task<ActionResult<IEnumerable<CredentialAccessLog>>> GetAllAccessLogs([FromQuery] int take = 100)
    {
        var logs = await accessLogRepository.GetAllAsync(take);
        return Ok(logs);
    }
}

public record UpdatePermissionDto(PermissionStatus Status, string ResolvedBy);
public record CredentialRequestDto(string CredentialName, string Reason, int? DurationMinutes, string? GroupName = null);
public record TelegramCallbackDto(string? CallbackQueryId, string? Data, string? MessageId, string? FromUsername, string? FromId);
public record UpdateVaultModeDto(string? VaultMode);
