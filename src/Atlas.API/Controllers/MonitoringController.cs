using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.API.Controllers;

[ApiController]
[Route("api/monitoring")]
public class MonitoringController(IMonitoringRepository monitoringRepository) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<SystemStatus>> GetStatus()
    {
        var status = await monitoringRepository.GetSystemStatusAsync();
        return status is null ? NotFound() : Ok(status);
    }

    [HttpPut("status")]
    public async Task<ActionResult<SystemStatus>> UpsertStatus(SystemStatus status)
        => Ok(await monitoringRepository.UpsertSystemStatusAsync(status));

    [HttpGet("cost/daily")]
    public async Task<ActionResult<CostSummary>> GetDailyCost([FromQuery] DateTime date)
    {
        var cost = await monitoringRepository.GetDailyCostAsync(date);
        return cost is null ? NotFound() : Ok(cost);
    }

    [HttpPost("cost")]
    public async Task<IActionResult> IncrementCost([FromBody] CostIncrement cost, [FromServices] ITokenUsageRepository? tokenUsageRepository = null)
    {
        if (cost.CostUsd <= 0) return BadRequest("Cost must be positive");
        await monitoringRepository.IncrementDailyCostAsync(cost.CostUsd);
        
        // Also log to TokenUsage if the service is available and extra fields are provided
        if (tokenUsageRepository != null && !string.IsNullOrEmpty(cost.Provider) && !string.IsNullOrEmpty(cost.Model))
        {
            var usage = new Atlas.Domain.Entities.TokenUsage
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Provider = cost.Provider,
                Model = cost.Model,
                InputTokens = cost.InputTokens,
                OutputTokens = cost.OutputTokens,
                TotalTokens = cost.InputTokens + cost.OutputTokens,
                CostUsd = cost.CostUsd,
                DurationMs = cost.DurationMs,
                SessionKey = cost.SessionKey,
                TaskCategory = cost.TaskCategory,
                ContextPercent = cost.ContextPercent
            };
            await tokenUsageRepository.LogUsageAsync(usage);
        }
        
        return Ok();
    }

    [HttpGet("cost/monthly")]
    public async Task<ActionResult<CostSummary>> GetMonthlyCost([FromQuery] int year, [FromQuery] int month)
    {
        var cost = await monitoringRepository.GetMonthlyCostAsync(year, month);
        return cost is null ? NotFound() : Ok(cost);
    }
}

public record CostIncrement(
    decimal CostUsd,
    string? Provider = null,
    string? Model = null,
    int InputTokens = 0,
    int OutputTokens = 0,
    int? DurationMs = null,
    string? SessionKey = null,
    string? TaskCategory = null,
    int? ContextPercent = null);

[ApiController]
[Route("api/export")]
public class ExportController(
    IMonitoringRepository monitoringRepository,
    Atlas.Application.Common.Interfaces.ITaskRepository taskRepository,
    Atlas.Application.Common.Interfaces.IActivityRepository activityRepository,
    Atlas.Application.Common.Interfaces.ISecurityRepository securityRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ExportAll()
    {
        var tasks = await taskRepository.GetAllAsync();
        var activities = await activityRepository.GetAllAsync(10000);
        var permissions = await securityRepository.GetAllPermissionRequestsAsync();
        var audits = await securityRepository.GetAllAuditsAsync();
        var status = await monitoringRepository.GetSystemStatusAsync();

        return Ok(new
        {
            exportedAt = DateTime.UtcNow,
            tasks,
            activityLogs = activities,
            permissionRequests = permissions,
            securityAudits = audits,
            systemStatus = status
        });
    }
}
