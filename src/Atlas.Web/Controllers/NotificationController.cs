using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.Enums;
using Atlas.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationController(
    TelegramNotificationService telegram,
    ISecurityRepository securityRepository) : ControllerBase
{
    /// <summary>
    /// Send a push notification for a credential request (called by plugin).
    /// </summary>
    [HttpPost("credential-request")]
    public async Task<IActionResult> SendCredentialRequestNotification([FromBody] CredentialNotificationDto dto)
    {
        var sent = await telegram.SendCredentialRequestNotification(
            dto.RequestId, dto.CredentialName, dto.Reason, dto.DurationMinutes);
        
        return Ok(new { sent });
    }

    /// <summary>
    /// URL-based approve endpoint (opened from Telegram inline button).
    /// </summary>
    [HttpGet("credential/{requestId:guid}/approve")]
    public async Task<ContentResult> ApproveCredential(Guid requestId)
    {
        return await HandleCredentialDecision(requestId, approved: true);
    }

    /// <summary>
    /// URL-based deny endpoint (opened from Telegram inline button).
    /// </summary>
    [HttpGet("credential/{requestId:guid}/deny")]
    public async Task<ContentResult> DenyCredential(Guid requestId)
    {
        return await HandleCredentialDecision(requestId, approved: false);
    }

    private async Task<ContentResult> HandleCredentialDecision(Guid requestId, bool approved)
    {
        // Check if already resolved
        var permissions = await securityRepository.GetAllPermissionRequestsAsync();
        var existing = permissions.FirstOrDefault(p => p.Id == requestId);
        
        if (existing is not null && existing.Status != PermissionStatus.Pending)
        {
            var alreadyStatus = existing.Status == PermissionStatus.Approved ? "approved" : "denied";
            return RenderPage($"⚠️ Already {alreadyStatus}", 
                $"This credential request was already <strong>{alreadyStatus}</strong> by {System.Net.WebUtility.HtmlEncode(existing.ResolvedBy ?? "someone")}.", 
                "#f59e0b");
        }

        var status = approved ? PermissionStatus.Approved : PermissionStatus.Denied;
        var resolvedBy = "Owner (Telegram)";

        var updated = await securityRepository.UpdatePermissionRequestAsync(requestId, status, resolvedBy);
        
        string title, body, color;
        if (updated is null)
        {
            title = "⚠️ Request Not Found";
            body = "This credential request was not found.";
            color = "#f59e0b";
        }
        else
        {
            await securityRepository.CreateAuditAsync(new SecurityAudit
            {
                Action = approved ? "CredentialApproved" : "CredentialDenied",
                Details = $"Request {requestId} {(approved ? "approved" : "denied")} by {resolvedBy} via Telegram push notification",
                Severity = approved ? Severity.Warning : Severity.Info
            });

            var credName = updated.Description?.Split('\'').ElementAtOrDefault(1) ?? "Unknown";
            title = approved ? "✅ Credential Approved" : "❌ Credential Denied";
            body = approved
                ? $"Access to <strong>{System.Net.WebUtility.HtmlEncode(credName)}</strong> has been approved. The bot can now retrieve it."
                : $"Access to <strong>{System.Net.WebUtility.HtmlEncode(credName)}</strong> has been denied.";
            color = approved ? "#22c55e" : "#ef4444";

            // Update Telegram message: remove buttons, show result
            _ = telegram.UpdateAfterDecision(requestId, credName, approved);
        }

        return RenderPage(title, body, color);
    }

    private static ContentResult RenderPage(string title, string body, string color)
    {
        var html = $@"<!DOCTYPE html>
<html><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>{title}</title>
<style>
  body {{ font-family: -apple-system, BlinkMacSystemFont, sans-serif; background: #0d1117; color: #e6edf3; 
         display: flex; justify-content: center; align-items: center; min-height: 100vh; margin: 0; }}
  .card {{ background: #161b22; border: 1px solid {color}40; border-radius: 16px; padding: 32px; 
           max-width: 400px; text-align: center; }}
  h1 {{ font-size: 1.5rem; margin: 0 0 12px; }}
  p {{ color: #8b949e; line-height: 1.5; }}
</style></head>
<body><div class=""card""><h1>{title}</h1><p>{body}</p><p style=""margin-top:16px;font-size:0.8rem;color:#484f58;"">You can close this window.</p></div></body></html>";

        return new ContentResult { Content = html, ContentType = "text/html", StatusCode = 200 };
    }
}

public record CredentialNotificationDto(Guid RequestId, string CredentialName, string Reason, int DurationMinutes);
