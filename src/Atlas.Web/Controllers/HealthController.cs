using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController(IHealthRepository healthRepository) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var services = new[] { "OpenClaw Gateway", "Vigil Server" };
        var results = new List<object>();

        foreach (var svc in services)
        {
            var latest = await healthRepository.GetLatestCheckForServiceAsync(svc);
            results.Add(new
            {
                service = svc,
                status = latest?.Status ?? "Unknown",
                checkedAt = latest?.CheckedAt,
                responseTimeMs = latest?.ResponseTimeMs
            });
        }

        return Ok(results);
    }

    [HttpGet("checks")]
    public async Task<ActionResult<IEnumerable<HealthCheck>>> GetChecks([FromQuery] int take = 50)
        => Ok(await healthRepository.GetLatestChecksAsync(take));

    [HttpGet("events")]
    public async Task<ActionResult<IEnumerable<HealthEvent>>> GetEvents([FromQuery] int take = 100)
        => Ok(await healthRepository.GetEventsAsync(take));

    [HttpGet("uptime")]
    public async Task<IActionResult> GetUptime([FromQuery] string service = "OpenClaw Gateway", [FromQuery] int days = 7)
        => Ok(await healthRepository.GetUptimeStatsAsync(service, days));

    [HttpPost("restart/{serviceName}")]
    public async Task<IActionResult> ManualRestart(string serviceName)
    {
        if (serviceName != "OpenClaw Gateway")
            return BadRequest(new { error = "Only OpenClaw Gateway supports manual restart" });

        try
        {
            var npmPath = Environment.GetEnvironmentVariable("APPDATA") ?? "";
            var psi = new System.Diagnostics.ProcessStartInfo("node",
                $"\"{Path.Combine(npmPath, "npm", "node_modules", "openclaw", "dist", "index.mjs")}\" gateway restart")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
                var output = await proc.StandardOutput.ReadToEndAsync();

                await healthRepository.RecordEventAsync(new HealthEvent
                {
                    ServiceName = serviceName,
                    EventType = "ManualRestart",
                    OccurredAt = DateTime.UtcNow,
                    Details = $"Manual restart triggered. Exit code: {proc.ExitCode}",
                    NotificationSent = false
                });

                return Ok(new { success = proc.ExitCode == 0, output });
            }
            return StatusCode(500, new { error = "Failed to start process" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
