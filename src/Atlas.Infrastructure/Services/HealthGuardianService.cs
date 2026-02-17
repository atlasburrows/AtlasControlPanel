using System.Diagnostics;
using System.Net.Sockets;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services;

public class HealthGuardianService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<HealthGuardianService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);
    private const int MaxRestartsPerHour = 3;
    private const string OpenClawGateway = "OpenClaw Gateway";
    private const string VigilServer = "Vigil Server";

    // Track service state for down/recovery detection
    private readonly Dictionary<string, bool> _lastKnownState = new();
    private readonly Dictionary<string, DateTime> _downSince = new();
    private readonly List<DateTime> _gatewayRestartAttempts = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Health Guardian service started");

        // Wait a bit before first check to let the app fully start
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunHealthChecksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Health Guardian check cycle failed");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Health Guardian service stopped");
    }

    private async Task RunHealthChecksAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        IHealthRepository? healthRepo = null;
        try
        {
            healthRepo = scope.ServiceProvider.GetRequiredService<IHealthRepository>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not resolve IHealthRepository — DB may be unavailable");
        }

        // Get TelegramNotificationService (singleton, resolve from root)
        object? telegramService = null;
        try
        {
            telegramService = scope.ServiceProvider.GetService(
                Type.GetType("Atlas.Web.Services.TelegramNotificationService, Atlas.Web") ?? typeof(object));
        }
        catch { }

        await CheckServiceAsync(OpenClawGateway, CheckOpenClawHealthAsync, healthRepo, telegramService, ct);
        await CheckServiceAsync(VigilServer, CheckVigilHealthAsync, healthRepo, telegramService, ct);
    }

    private async Task CheckServiceAsync(
        string serviceName,
        Func<CancellationToken, Task<(string status, double? responseMs, string? details)>> checker,
        IHealthRepository? repo,
        object? telegram,
        CancellationToken ct)
    {
        var (status, responseMs, details) = await checker(ct);
        var isHealthy = status == "Healthy";
        var wasHealthy = !_lastKnownState.TryGetValue(serviceName, out var prev) || prev;

        logger.LogDebug("Health check: {Service} = {Status} ({ResponseMs}ms)", serviceName, status, responseMs);

        // Record check
        var check = new HealthCheck
        {
            ServiceName = serviceName,
            Status = status,
            CheckedAt = DateTime.UtcNow,
            ResponseTimeMs = responseMs,
            Details = details
        };

        // Handle state transitions
        if (!isHealthy && wasHealthy)
        {
            // Just went down
            _downSince[serviceName] = DateTime.UtcNow;
            logger.LogWarning("{Service} is {Status}: {Details}", serviceName, status, details);

            await TrySendHealthAlert(telegram, serviceName, status, details);
            await TryRecordEvent(repo, serviceName, "Down", details, true);

            // Attempt auto-restart for OpenClaw
            if (serviceName == OpenClawGateway)
            {
                var restarted = await TryAutoRestartGateway();
                check.AutoRestarted = restarted;
                if (restarted)
                    await TryRecordEvent(repo, serviceName, "AutoRestart", "Auto-restart attempted", true);
            }
        }
        else if (!isHealthy && !wasHealthy)
        {
            // Still down — try restart if within limits
            if (serviceName == OpenClawGateway)
            {
                var restarted = await TryAutoRestartGateway();
                check.AutoRestarted = restarted;
            }
        }
        else if (isHealthy && !wasHealthy)
        {
            // Recovered
            var downtime = _downSince.TryGetValue(serviceName, out var since)
                ? DateTime.UtcNow - since
                : TimeSpan.Zero;
            _downSince.Remove(serviceName);

            logger.LogInformation("{Service} has recovered after {Downtime}", serviceName, downtime);
            await TrySendHealthRecovery(telegram, serviceName, downtime);
            await TryRecordEvent(repo, serviceName, "Recovered", $"Recovered after {downtime}", true);
        }

        _lastKnownState[serviceName] = isHealthy;

        // Record the check to DB
        if (repo != null)
        {
            try { await repo.RecordCheckAsync(check); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to record health check for {Service}", serviceName); }
        }
    }

    private async Task<(string status, double? responseMs, string? details)> CheckOpenClawHealthAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Try HTTP health endpoint first
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync("http://127.0.0.1:18789/health", ct);
            sw.Stop();
            if (response.IsSuccessStatusCode)
                return ("Healthy", sw.Elapsed.TotalMilliseconds, null);
            return ("Degraded", sw.Elapsed.TotalMilliseconds, $"HTTP {(int)response.StatusCode}");
        }
        catch
        {
            // Fallback: TCP connect to port 18789
            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync("127.0.0.1", 18789);
                if (await Task.WhenAny(connectTask, Task.Delay(3000, ct)) == connectTask && tcp.Connected)
                {
                    sw.Stop();
                    return ("Healthy", sw.Elapsed.TotalMilliseconds, "TCP connect OK (HTTP failed)");
                }
            }
            catch { }
        }
        sw.Stop();
        return ("Unhealthy", sw.Elapsed.TotalMilliseconds, "Gateway unreachable on port 18789");
    }

    private async Task<(string status, double? responseMs, string? details)> CheckVigilHealthAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync("http://127.0.0.1:5263/api/health", ct);
            sw.Stop();
            if (response.IsSuccessStatusCode)
                return ("Healthy", sw.Elapsed.TotalMilliseconds, null);
            return ("Degraded", sw.Elapsed.TotalMilliseconds, $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ("Unhealthy", sw.Elapsed.TotalMilliseconds, $"Self-check failed: {ex.Message}");
        }
    }

    private async Task<bool> TryAutoRestartGateway()
    {
        // Prune old restart attempts
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        _gatewayRestartAttempts.RemoveAll(t => t < oneHourAgo);

        if (_gatewayRestartAttempts.Count >= MaxRestartsPerHour)
        {
            logger.LogWarning("Max gateway restart attempts ({Max}) reached in the last hour", MaxRestartsPerHour);
            return false;
        }

        try
        {
            logger.LogInformation("Attempting auto-restart of OpenClaw Gateway...");
            _gatewayRestartAttempts.Add(DateTime.UtcNow);

            var npmPath = Environment.GetEnvironmentVariable("APPDATA") ?? "";
            var psi = new ProcessStartInfo("node", $"\"{Path.Combine(npmPath, "npm", "node_modules", "openclaw", "dist", "index.mjs")}\" gateway start")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
                var output = await proc.StandardOutput.ReadToEndAsync();
                logger.LogInformation("Gateway restart result (exit {Code}): {Output}", proc.ExitCode, output);
                return proc.ExitCode == 0;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to auto-restart gateway");
        }
        return false;
    }

    private async Task TrySendHealthAlert(object? telegram, string serviceName, string status, string? details)
    {
        if (telegram == null) return;
        try
        {
            var method = telegram.GetType().GetMethod("SendHealthAlert");
            if (method != null)
            {
                var task = (Task<bool>?)method.Invoke(telegram, [serviceName, status, details]);
                if (task != null) await task;
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to send health alert notification"); }
    }

    private async Task TrySendHealthRecovery(object? telegram, string serviceName, TimeSpan downtime)
    {
        if (telegram == null) return;
        try
        {
            var method = telegram.GetType().GetMethod("SendHealthRecovery");
            if (method != null)
            {
                var task = (Task<bool>?)method.Invoke(telegram, [serviceName, downtime]);
                if (task != null) await task;
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to send health recovery notification"); }
    }

    private static async Task TryRecordEvent(IHealthRepository? repo, string serviceName, string eventType, string? details, bool notificationSent)
    {
        if (repo == null) return;
        try
        {
            await repo.RecordEventAsync(new HealthEvent
            {
                ServiceName = serviceName,
                EventType = eventType,
                OccurredAt = DateTime.UtcNow,
                Details = details,
                NotificationSent = notificationSent
            });
        }
        catch { }
    }
}
