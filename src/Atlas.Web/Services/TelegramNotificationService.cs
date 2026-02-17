using System.Text.Json;

namespace Atlas.Web.Services;

public class TelegramNotificationService(IConfiguration config, IHttpClientFactory httpFactory, ILogger<TelegramNotificationService> logger)
{
    private string? BotToken => config["Telegram:BotToken"];
    private string? ChatId => config["Telegram:ChatId"];
    private static string? _cachedPublicUrl;
    private string PublicUrl => _cachedPublicUrl ??= DetectPublicUrl();

    private string DetectPublicUrl()
    {
        // Always prefer auto-detected external IP for notification URLs.
        // Users won't have custom domains pointing to their server — external IP is reliable.
        // The configured PublicUrl (e.g. zenidolabs.com) may not route to the correct port.
        try
        {
            using var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var ip = client.GetStringAsync("http://api.ipify.org").Result.Trim();
            var port = config["App:Port"] ?? "5263";
            var url = $"http://{ip}:{port}";
            logger.LogInformation("Auto-detected public URL for notifications: {Url}", url);
            return url;
        }
        catch (Exception ex)
        {
            // Fall back to configured URL or localhost
            var configured = config["App:PublicUrl"];
            logger.LogWarning(ex, "External IP detection failed, falling back to config");
            return configured ?? config["App:BaseUrl"] ?? "http://127.0.0.1:5263";
        }
    }

    public bool IsConfigured => !string.IsNullOrEmpty(BotToken) && !string.IsNullOrEmpty(ChatId);

    // Track Telegram message IDs for credential request notifications so we can edit them after action
    private static readonly Dictionary<Guid, int> _requestMessageIds = new();

    /// <summary>
    /// Send a credential access request notification with Approve/Deny inline buttons.
    /// </summary>
    public async Task<bool> SendCredentialRequestNotification(Guid requestId, string credentialName, string reason, int durationMinutes, string? hmacToken = null)
    {
        if (!IsConfigured) return false;

        try
        {
            var client = httpFactory.CreateClient();
            var apiUrl = $"https://api.telegram.org/bot{BotToken}/sendMessage";

            var payload = new
            {
                chat_id = ChatId,
                text = $"🔐 <b>Credential Access Request</b>\n\n" +
                       $"<b>Credential:</b> {EscapeHtml(credentialName)}\n" +
                       $"<b>Reason:</b> {EscapeHtml(reason)}\n" +
                       $"<b>Duration:</b> {durationMinutes} minutes\n" +
                       $"<b>Request ID:</b> <code>{requestId}</code>\n\n" +
                       $"⏳ Awaiting your approval...",
                parse_mode = "HTML",
                reply_markup = new
                {
                    inline_keyboard = new[]
                    {
                        new object[]
                        {
                            new { text = "✅ Approve", url = $"{PublicUrl}/api/notifications/credential/{requestId}/approve{(hmacToken is not null ? $"?token={hmacToken}" : "")}" },
                            new { text = "❌ Deny", url = $"{PublicUrl}/api/notifications/credential/{requestId}/deny{(hmacToken is not null ? $"?token={hmacToken}" : "")}" }
                        }
                    }
                }
            };

            var response = await client.PostAsJsonAsync(apiUrl, payload);
            var body = await response.Content.ReadAsStringAsync();
            logger.LogInformation("Credential notification sent: {Status} {Body}", response.StatusCode, body);

            // Extract and store the message_id so we can edit it later
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("result", out var result) &&
                        result.TryGetProperty("message_id", out var msgId))
                    {
                        _requestMessageIds[requestId] = msgId.GetInt32();
                    }
                }
                catch { }
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send credential notification");
            return false;
        }
    }

    /// <summary>
    /// Update a message to show the approval/denial result (removes buttons).
    /// </summary>
    public async Task<bool> UpdateCredentialNotification(string messageId, string credentialName, bool approved, string resolvedBy)
    {
        if (!IsConfigured) return false;

        try
        {
            var client = httpFactory.CreateClient();
            var apiUrl = $"https://api.telegram.org/bot{BotToken}/editMessageText";

            var status = approved ? "✅ APPROVED" : "❌ DENIED";
            var payload = new
            {
                chat_id = ChatId,
                message_id = messageId,
                text = $"🔐 <b>Credential Access Request</b>\n\n" +
                       $"<b>Credential:</b> {EscapeHtml(credentialName)}\n" +
                       $"<b>Status:</b> {status}\n" +
                       $"<b>By:</b> {EscapeHtml(resolvedBy)}\n" +
                       $"<b>At:</b> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                parse_mode = "HTML"
            };

            var response = await client.PostAsJsonAsync(apiUrl, payload);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update credential notification");
            return false;
        }
    }

    /// <summary>
    /// Answer a callback query (dismiss the "loading" indicator on the button).
    /// </summary>
    public async Task AnswerCallbackQuery(string callbackQueryId, string text)
    {
        if (!IsConfigured) return;

        try
        {
            var client = httpFactory.CreateClient();
            var apiUrl = $"https://api.telegram.org/bot{BotToken}/answerCallbackQuery";

            await client.PostAsJsonAsync(apiUrl, new
            {
                callback_query_id = callbackQueryId,
                text
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to answer callback query");
        }
    }

    /// <summary>
    /// After approve/deny, edit the original notification to show the result and remove buttons.
    /// </summary>
    public async Task UpdateAfterDecision(Guid requestId, string credentialName, bool approved)
    {
        if (!IsConfigured) return;
        if (!_requestMessageIds.TryGetValue(requestId, out var messageId)) return;

        try
        {
            var client = httpFactory.CreateClient();
            var status = approved ? "✅ APPROVED" : "❌ DENIED";
            var apiUrl = $"https://api.telegram.org/bot{BotToken}/editMessageText";

            await client.PostAsJsonAsync(apiUrl, new
            {
                chat_id = ChatId,
                message_id = messageId,
                text = $"🔐 <b>Credential Access Request</b>\n\n" +
                       $"<b>Credential:</b> {EscapeHtml(credentialName)}\n" +
                       $"<b>Status:</b> {status}\n" +
                       $"<b>Resolved:</b> {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
                parse_mode = "HTML"
                // No reply_markup = buttons removed
            });

            _requestMessageIds.Remove(requestId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update credential notification message");
        }
    }

    public async Task<bool> SendHealthAlert(string serviceName, string status, string? details = null)
    {
        if (!IsConfigured) return false;
        try
        {
            var client = httpFactory.CreateClient();
            var apiUrl = $"https://api.telegram.org/bot{BotToken}/sendMessage";
            var text = $"⚠️ <b>{EscapeHtml(serviceName)}</b> is <b>{EscapeHtml(status)}</b>";
            if (!string.IsNullOrEmpty(details))
                text += $"\n\n{EscapeHtml(details)}";

            var payload = new { chat_id = ChatId, text, parse_mode = "HTML" };
            var response = await client.PostAsJsonAsync(apiUrl, payload);
            logger.LogInformation("Health alert sent for {Service}: {Status}", serviceName, response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send health alert");
            return false;
        }
    }

    public async Task<bool> SendHealthRecovery(string serviceName, TimeSpan downtime)
    {
        if (!IsConfigured) return false;
        try
        {
            var client = httpFactory.CreateClient();
            var apiUrl = $"https://api.telegram.org/bot{BotToken}/sendMessage";
            var downtimeStr = downtime.TotalMinutes < 1
                ? $"{downtime.TotalSeconds:F0}s"
                : downtime.TotalHours < 1
                    ? $"{downtime.TotalMinutes:F0}m"
                    : $"{downtime.TotalHours:F1}h";

            var payload = new
            {
                chat_id = ChatId,
                text = $"✅ <b>{EscapeHtml(serviceName)}</b> has recovered after {downtimeStr}",
                parse_mode = "HTML"
            };
            var response = await client.PostAsJsonAsync(apiUrl, payload);
            logger.LogInformation("Health recovery sent for {Service}: {Status}", serviceName, response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send health recovery notification");
            return false;
        }
    }

    private static string EscapeHtml(string text)
        => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
