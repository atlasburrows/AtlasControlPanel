using System.Text.Json;

namespace Atlas.Web.Services;

/// <summary>
/// Sends all notifications through the OpenClaw Gateway messaging API.
/// Channel-agnostic: works with Telegram, Signal, Discord, etc.
/// No direct Telegram API calls — OpenClaw handles delivery.
/// </summary>
public class NotificationService(IConfiguration config, IHttpClientFactory httpFactory, ILogger<NotificationService> logger)
{
    private string GatewayUrl => config["OpenClaw:GatewayUrl"] ?? "http://localhost:18789";
    private string? GatewayToken => config["OpenClaw:Token"];
    private string? OwnerChatId => config["OpenClaw:OwnerChatId"] ?? config["Telegram:ChatId"];
    private string? Channel => config["OpenClaw:Channel"] ?? "telegram";
    
    private static string? _cachedPublicUrl;
    private string PublicUrl => _cachedPublicUrl ??= DetectPublicUrl();

    // Track which notifications have been sent (dedup)
    private static readonly HashSet<Guid> _sentNotificationIds = new();
    
    // Track message IDs for credential notifications so we can edit them after approval/denial
    private static readonly Dictionary<Guid, string> _requestMessageIds = new();

    public bool IsConfigured => !string.IsNullOrEmpty(GatewayToken) && !string.IsNullOrEmpty(OwnerChatId);

    private string DetectPublicUrl()
    {
        try
        {
            using var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var ip = client.GetStringAsync("http://api.ipify.org").Result.Trim();
            var port = config["App:Port"] ?? "5263";
            var url = $"http://{ip}:{port}";
            logger.LogInformation("Auto-detected public URL: {Url}", url);
            return url;
        }
        catch (Exception ex)
        {
            var configured = config["App:PublicUrl"];
            logger.LogWarning(ex, "External IP detection failed, falling back to config");
            return configured ?? config["App:BaseUrl"] ?? "http://127.0.0.1:5263";
        }
    }

    /// <summary>
    /// Send a message through the OpenClaw gateway.
    /// </summary>
    private async Task<(bool sent, string? messageId)> SendViaGateway(string message, object? buttons = null)
    {
        try
        {
            var client = httpFactory.CreateClient();
            var url = $"{GatewayUrl.TrimEnd('/')}/tools/invoke";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            if (!string.IsNullOrEmpty(GatewayToken))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GatewayToken);

            var args = new Dictionary<string, object?>
            {
                ["action"] = "send",
                ["message"] = message,
                ["target"] = OwnerChatId,
                ["channel"] = Channel
            };
            
            if (buttons is not null)
                args["buttons"] = buttons;

            var body = new { tool = "message", args };
            request.Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Notification sent via gateway: {Response}", responseBody);
                
                // Try to extract message_id from response
                string? messageId = null;
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("result", out var result))
                    {
                        // Check for messageId in the result content
                        if (result.TryGetProperty("details", out var details) &&
                            details.TryGetProperty("messageId", out var msgId))
                        {
                            messageId = msgId.ToString();
                        }
                    }
                }
                catch { /* best-effort message_id extraction */ }
                
                return (true, messageId);
            }

            logger.LogWarning("Gateway notification failed: {Status} {Body}", response.StatusCode, responseBody);
            return (false, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending notification via gateway");
            return (false, null);
        }
    }
    
    /// <summary>
    /// Edit a previously sent message via the OpenClaw gateway.
    /// </summary>
    public async Task<bool> EditMessage(string messageId, string newText)
    {
        try
        {
            var client = httpFactory.CreateClient();
            var url = $"{GatewayUrl.TrimEnd('/')}/tools/invoke";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            if (!string.IsNullOrEmpty(GatewayToken))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GatewayToken);

            var body = new
            {
                tool = "message",
                args = new
                {
                    action = "edit",
                    messageId,
                    message = newText,
                    target = OwnerChatId,
                    channel = Channel
                }
            };
            request.Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error editing message via gateway");
            return false;
        }
    }
    
    /// <summary>
    /// Update a credential notification after approval/denial — removes buttons, shows result.
    /// </summary>
    public async Task UpdateAfterDecision(Guid requestId, string credentialName, bool approved)
    {
        if (!_requestMessageIds.TryGetValue(requestId, out var messageId)) 
        {
            logger.LogWarning("No message_id found for request {RequestId} — cannot edit notification", requestId);
            return;
        }
        
        var status = approved ? "✅ APPROVED" : "❌ DENIED";
        var newText = $"🔐 Credential Access Request\n\n" +
                      $"Credential: {credentialName}\n" +
                      $"Status: {status}\n" +
                      $"Resolved: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";
        
        await EditMessage(messageId, newText);
        _requestMessageIds.Remove(requestId);
    }

    /// <summary>
    /// Send a credential access request notification with Approve/Deny URL buttons.
    /// </summary>
    public async Task<bool> SendCredentialRequestNotification(Guid requestId, string credentialName, string reason, int durationMinutes, string? hmacToken = null)
    {
        if (!IsConfigured) return false;

        // Dedup
        lock (_sentNotificationIds)
        {
            if (_sentNotificationIds.Contains(requestId))
            {
                logger.LogWarning("DEDUP: Blocked duplicate credential notification for {RequestId}", requestId);
                return false;
            }
            _sentNotificationIds.Add(requestId);
        }

        var message = $"🔐 Credential Access Request\n\n" +
                      $"Credential: {credentialName}\n" +
                      $"Reason: {reason}\n" +
                      $"Duration: {durationMinutes} minutes\n" +
                      $"Request ID: {requestId}\n\n" +
                      $"⏳ Awaiting your approval...";

        // Callback buttons with embedded HMAC token (max 64 chars for Telegram callback_data)
        // Format: v:a:{uuid-no-dashes}:{token-first-16-chars} = ~60 chars
        var shortId = requestId.ToString("N"); // 32 chars, no dashes
        var shortToken = hmacToken?[..Math.Min(16, hmacToken.Length)] ?? "";
        var buttons = new[]
        {
            new[]
            {
                new { text = "✅ Approve", callback_data = $"v:a:{shortId}:{shortToken}" },
                new { text = "❌ Deny", callback_data = $"v:d:{shortId}:{shortToken}" }
            }
        };

        var (sent, messageId) = await SendViaGateway(message, buttons);
        
        // Store message_id so we can edit the notification after approval/denial
        if (sent && messageId is not null)
        {
            _requestMessageIds[requestId] = messageId;
            logger.LogInformation("Stored message_id {MessageId} for request {RequestId}", messageId, requestId);
        }
        
        return sent;
    }

    /// <summary>
    /// Send a wake event to relay approval/denial back to the AI session.
    /// </summary>
    public async Task<bool> SendWakeEvent(string text, string mode = "now")
    {
        try
        {
            var client = httpFactory.CreateClient();
            var url = $"{GatewayUrl.TrimEnd('/')}/tools/invoke";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            if (!string.IsNullOrEmpty(GatewayToken))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GatewayToken);

            var body = new { tool = "cron", args = new { action = "wake", text, mode } };
            request.Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending wake event");
            return false;
        }
    }

    public async Task<bool> SendHealthAlert(string serviceName, string status, string? details = null)
    {
        if (!IsConfigured) return false;
        var message = $"⚠️ {serviceName} is {status}";
        if (!string.IsNullOrEmpty(details))
            message += $"\n\n{details}";
        var (sent, _) = await SendViaGateway(message);
        return sent;
    }

    public async Task<bool> SendHealthRecovery(string serviceName, TimeSpan downtime)
    {
        if (!IsConfigured) return false;
        var downtimeStr = downtime.TotalMinutes < 1
            ? $"{downtime.TotalSeconds:F0}s"
            : downtime.TotalHours < 1
                ? $"{downtime.TotalMinutes:F0}m"
                : $"{downtime.TotalHours:F1}h";
        var (sent, _) = await SendViaGateway($"✅ {serviceName} has recovered after {downtimeStr}");
        return sent;
    }
}
