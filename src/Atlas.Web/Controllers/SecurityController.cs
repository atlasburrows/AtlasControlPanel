using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.Enums;
using Atlas.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Controllers;

[ApiController]
[Route("api/security")]
public class SecurityController(ISecurityRepository securityRepository, TelegramNotificationService telegram) : ControllerBase
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
}

public record UpdatePermissionDto(PermissionStatus Status, string ResolvedBy);
public record CredentialRequestDto(string CredentialName, string Reason, int? DurationMinutes);
public record TelegramCallbackDto(string? CallbackQueryId, string? Data, string? MessageId, string? FromUsername, string? FromId);
