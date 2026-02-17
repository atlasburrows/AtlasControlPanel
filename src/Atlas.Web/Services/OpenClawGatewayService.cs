using System.Text.Json;

namespace Atlas.Web.Services;

/// <summary>
/// Calls the OpenClaw Gateway's /tools/invoke endpoint to inject system events
/// (e.g., credential approval/denial feedback) into the AI session.
/// </summary>
public class OpenClawGatewayService(IConfiguration config, IHttpClientFactory httpFactory, ILogger<OpenClawGatewayService> logger)
{
    private string GatewayUrl => config["OpenClaw:GatewayUrl"] ?? "http://localhost:18789";
    private string? GatewayToken => config["OpenClaw:Token"];

    /// <summary>
    /// Send a wake event to the OpenClaw gateway, which injects text as a system event
    /// into the main session. The AI will process it on its next turn.
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

            var body = new
            {
                tool = "cron",
                args = new { action = "wake", text, mode }
            };

            request.Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Wake event sent to OpenClaw gateway: {Text}", text);
                return true;
            }
            
            logger.LogWarning("Failed to send wake event: {Status} {Body}", response.StatusCode, responseBody);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending wake event to OpenClaw gateway");
            return false;
        }
    }
}
