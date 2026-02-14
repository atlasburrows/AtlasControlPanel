using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Atlas.Application;
using Atlas.Infrastructure;
using Atlas.Infrastructure.Hubs;
using Atlas.Web.Components;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddInfrastructure(connectionString!);
builder.Services.AddApplicationServices();
builder.Services.AddMudServices();
builder.Services.AddSignalR();

// Add Controllers with JsonStringEnumConverter
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// CORS policy for localhost only
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalhostOnly", policy =>
        policy
            .WithOrigins("http://localhost:3000", "http://localhost:5000", "http://localhost:5263", "http://127.0.0.1:3000", "http://127.0.0.1:5000", "http://127.0.0.1:5263")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddHttpClient();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5263") });
builder.Services.AddSingleton<Atlas.Web.Services.TelegramNotificationService>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
// No HTTPS redirect — running plain HTTP on port 5263

app.UseCors("LocalhostOnly");

// API Key middleware for /api/ routes (except /api/auth/ and /api/chat/)
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
    
    // Check if this is an API route (but not /api/auth/ or /api/chat/)
    if (path.StartsWith("/api/") && !path.StartsWith("/api/auth/") && !path.StartsWith("/api/chat/") && !path.StartsWith("/api/notifications/credential/"))
    {
        var apiConfig = app.Configuration.GetSection("Api");
        var requiredKey = apiConfig["Key"];
        
        // If API key is configured, require it for non-cookie-authenticated requests
        if (!string.IsNullOrEmpty(requiredKey))
        {
            // Check for existing cookie authentication
            var user = context.User?.Identity?.IsAuthenticated ?? false;
            if (!user)
            {
                // No cookie auth, require API key header
                context.Request.Headers.TryGetValue("X-Api-Key", out var providedKey);
                if (string.IsNullOrEmpty(providedKey) || providedKey != requiredKey)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing API key" });
                    return;
                }
            }
        }
        // If no key configured, allow all (dev mode)
    }
    
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.UseStaticFiles(); // Fallback for debug builds in Production mode
app.MapStaticAssets();

// Map controllers BEFORE other endpoints
app.MapControllers();

// Auth API endpoints (DisableAntiforgery so JS fetch works on all platforms)
app.MapPost("/api/auth/login", async (HttpContext ctx, IConfiguration config, LoginRequest req) =>
{
    var authConfig = config.GetSection("Auth");
    if (req.Username == authConfig["Username"] && req.Password == authConfig["Password"])
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, req.Username) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
        return Results.Ok();
    }
    return Results.Unauthorized();
}).DisableAntiforgery();

app.MapPost("/api/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync();
    return Results.Ok();
}).DisableAntiforgery();

app.MapGet("/api/health", async () =>
{
    var result = new Dictionary<string, object?>();

    async Task<System.Text.Json.JsonDocument?> RunCommand(string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("cmd", $"/c \"openclaw {args}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                return System.Text.Json.JsonDocument.Parse(output);
        }
        catch { }
        return null;
    }

    using var healthDoc = await RunCommand("health --json");
    using var statusDoc = await RunCommand("status --json");

    if (healthDoc != null)
        result["health"] = healthDoc.RootElement.Clone();
    if (statusDoc != null)
        result["status"] = statusDoc.RootElement.Clone();

    // Derive a simple overall status
    var gatewayReachable = false;
    var channelOk = false;
    try
    {
        if (healthDoc != null)
        {
            var root = healthDoc.RootElement;
            if (root.TryGetProperty("gateway", out var gw) && gw.TryGetProperty("reachable", out var r))
                gatewayReachable = r.GetBoolean();
            if (root.TryGetProperty("channels", out var ch))
            {
                foreach (var chan in ch.EnumerateArray())
                {
                    if (chan.TryGetProperty("probeOk", out var p) && p.GetBoolean())
                        channelOk = true;
                }
            }
        }
    }
    catch { }

    var overall = gatewayReachable && channelOk ? "green" : gatewayReachable ? "yellow" : "red";
    result["overall"] = overall;

    return Results.Json(result);
}).DisableAntiforgery().RequireAuthorization();

// Chat history — reads from OpenClaw session transcript (unified across Telegram + control panel)
app.MapGet("/api/chat/history", async (IConfiguration config, int? limit) =>
{
    var maxMessages = limit ?? 100;
    try
    {
        // Find the main session transcript
        var sessionsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".openclaw", "agents", "main", "sessions", "sessions.json");

        if (!File.Exists(sessionsPath))
            return Results.Json(new { messages = Array.Empty<object>(), error = "sessions.json not found" });

        var sessionsJson = await File.ReadAllTextAsync(sessionsPath);
        using var sessionsDoc = System.Text.Json.JsonDocument.Parse(sessionsJson);

        if (!sessionsDoc.RootElement.TryGetProperty("agent:main:main", out var mainSession) ||
            !mainSession.TryGetProperty("sessionId", out var sessionIdProp))
            return Results.Json(new { messages = Array.Empty<object>(), error = "main session not found" });

        var sessionId = sessionIdProp.GetString();
        var transcriptPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".openclaw", "agents", "main", "sessions", $"{sessionId}.jsonl");

        if (!File.Exists(transcriptPath))
            return Results.Json(new { messages = Array.Empty<object>(), error = "transcript not found" });

        // Read all lines and extract user/assistant text messages
        var allLines = await File.ReadAllLinesAsync(transcriptPath);
        var messages = new List<object>();

        foreach (var line in allLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.GetProperty("type").GetString() != "message") continue;

                var msg = root.GetProperty("message");
                var role = msg.GetProperty("role").GetString();
                var timestamp = root.GetProperty("timestamp").GetString();

                if (role == "user")
                {
                    // User messages: content is a string
                    if (msg.TryGetProperty("content", out var contentProp))
                    {
                        string? text = null;
                        if (contentProp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            text = contentProp.GetString();
                        }
                        else if (contentProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            // Sometimes content is array of {type, text}
                            var sb = new System.Text.StringBuilder();
                            foreach (var item in contentProp.EnumerateArray())
                            {
                                if (item.TryGetProperty("type", out var t) && t.GetString() == "text" &&
                                    item.TryGetProperty("text", out var txt))
                                    sb.Append(txt.GetString());
                            }
                            text = sb.ToString();
                        }
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        // Filter out heartbeat prompts, system events, compaction summaries
                        if (text.StartsWith("Read HEARTBEAT.md")) continue;
                        if (text.StartsWith("The conversation history before this point was compacted")) continue;
                        if (text.StartsWith("System:")) continue;
                        messages.Add(new { role, content = text, timestamp });
                    }
                }
                else if (role == "assistant")
                {
                    // Assistant messages: content is array, extract text parts
                    if (msg.TryGetProperty("content", out var contentProp) &&
                        contentProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var sb = new System.Text.StringBuilder();
                        foreach (var item in contentProp.EnumerateArray())
                        {
                            if (item.TryGetProperty("type", out var t) && t.GetString() == "text" &&
                                item.TryGetProperty("text", out var txt))
                            {
                                if (sb.Length > 0) sb.Append('\n');
                                sb.Append(txt.GetString());
                            }
                        }
                        var text = sb.ToString();
                        if (!string.IsNullOrWhiteSpace(text) && text != "HEARTBEAT_OK" && text != "NO_REPLY")
                            messages.Add(new { role, content = text, timestamp });
                    }
                }
            }
            catch { }
        }

        // Return only the last N messages
        var result = messages.TakeLast(maxMessages).ToList();
        return Results.Json(new { messages = result, total = messages.Count });
    }
    catch (Exception ex)
    {
        return Results.Json(new { messages = Array.Empty<object>(), error = ex.Message });
    }
}).DisableAntiforgery();

// Token usage & cost — reads from openclaw status --json
app.MapGet("/api/tokens", async () =>
{
    try
    {
        var psi = new System.Diagnostics.ProcessStartInfo("cmd", "/c \"openclaw status --json\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["NO_COLOR"] = "1";
        using var proc = System.Diagnostics.Process.Start(psi)!;
        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();

        // Strip ANSI escape codes and find JSON start
        output = System.Text.RegularExpressions.Regex.Replace(output, @"\x1B\[[^@-~]*[@-~]", "").Trim();
        var jsonStart = output.IndexOf('{');
        if (jsonStart < 0 || string.IsNullOrWhiteSpace(output))
            return Results.Json(new { error = "openclaw status returned no JSON" });
        output = output.Substring(jsonStart);

        using var doc = System.Text.Json.JsonDocument.Parse(output);
        var sessions = doc.RootElement.GetProperty("sessions");
        var recent = sessions.GetProperty("recent");

        // Find main session
        object? mainSessionData = null;
        long totalInputAll = 0;
        long totalOutputAll = 0;

        foreach (var session in recent.EnumerateArray())
        {
            try
            {
                var key = session.GetProperty("key").GetString();
                var inputTokens = session.TryGetProperty("inputTokens", out var inp) ? inp.GetInt64() : 0;
                var outputTokens = session.TryGetProperty("outputTokens", out var outp) ? outp.GetInt64() : 0;
                totalInputAll += inputTokens;
                totalOutputAll += outputTokens;

                if (key == "agent:main:main")
                {
                    mainSessionData = new
                    {
                        totalTokens = session.TryGetProperty("totalTokens", out var tt) ? tt.GetInt64() : 0,
                        remainingTokens = session.TryGetProperty("remainingTokens", out var rt) ? rt.GetInt64() : 0,
                        contextTokens = session.TryGetProperty("contextTokens", out var ct) ? ct.GetInt64() : 200000,
                        percentUsed = session.TryGetProperty("percentUsed", out var pu) ? pu.GetInt32() : 0,
                        inputTokens,
                        outputTokens,
                        model = session.TryGetProperty("model", out var m) ? m.GetString() : "unknown"
                    };
                }
            }
            catch { }
        }

        // Claude Opus 4 pricing: $15/MTok input, $75/MTok output
        // With caching: ~$3.75/MTok write, $0.30/MTok read (estimate lower bound)
        // Use full price as upper estimate
        var inputCostPer1M = 15.0;
        var outputCostPer1M = 75.0;
        var estimatedCost = (totalInputAll / 1_000_000.0 * inputCostPer1M) +
                            (totalOutputAll / 1_000_000.0 * outputCostPer1M);

        return Results.Json(new
        {
            mainSession = mainSessionData,
            allSessions = new { totalInput = totalInputAll, totalOutput = totalOutputAll },
            estimatedSessionCost = Math.Round(estimatedCost, 4),
            pricing = new { model = "claude-opus-4-6", inputPer1M = inputCostPer1M, outputPer1M = outputCostPer1M }
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message });
    }
}).DisableAntiforgery();

// Relay messages to Telegram — two separate messages: user then assistant
app.MapPost("/api/chat/relay", async (IConfiguration config, IHttpClientFactory httpFactory, ChatRelayRequest req) =>
{
    try
    {
        var botToken = config["Telegram:BotToken"];
        var chatId = config["Telegram:ChatId"];
        if (string.IsNullOrEmpty(botToken) || string.IsNullOrEmpty(chatId))
            return Results.Ok(new { relayed = false, reason = "Telegram not configured" });

        var client = httpFactory.CreateClient();
        var apiUrl = $"https://api.telegram.org/bot{botToken}/sendMessage";

        // Message 1: User's message with CP source icon
        var userPayload = new
        {
            chat_id = chatId,
            text = $"📱 {req.UserMessage}",
            disable_notification = true
        };
        var userResp = await client.PostAsJsonAsync(apiUrl, userPayload);
        var userBody = await userResp.Content.ReadAsStringAsync();
        Console.WriteLine($"[TelegramRelay] User msg: {userResp.StatusCode} {userBody}");

        // Message 2: Atlas response with globe icon
        var atlasPayload = new
        {
            chat_id = chatId,
            text = $"🌐 {req.AssistantMessage}",
            disable_notification = true
        };
        var response = await client.PostAsJsonAsync(apiUrl, atlasPayload);
        var atlasBody = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[TelegramRelay] Atlas msg: {response.StatusCode} {atlasBody}");

        return Results.Ok(new { relayed = response.IsSuccessStatusCode });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { relayed = false, error = ex.Message });
    }
}).DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Atlas.Shared.SharedAssemblyMarker).Assembly);

app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<ActivityHub>("/hubs/activity");

app.Run();

public record LoginRequest(string Username, string Password);
public record ChatRelayRequest(string UserMessage, string AssistantMessage);

static partial class ChatHelpers
{
    public static string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Replace("_", "\\_").Replace("*", "\\*").Replace("[", "\\[").Replace("`", "\\`");
    }
}

