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
    public async Task<IActionResult> IncrementCost([FromBody] CostIncrement cost)
    {
        if (cost.CostUsd <= 0) return BadRequest("Cost must be positive");
        await monitoringRepository.IncrementDailyCostAsync(cost.CostUsd);
        return Ok();
    }

    [HttpGet("cost/monthly")]
    public async Task<ActionResult<CostSummary>> GetMonthlyCost([FromQuery] int year, [FromQuery] int month)
    {
        var cost = await monitoringRepository.GetMonthlyCostAsync(year, month);
        return cost is null ? NotFound() : Ok(cost);
    }
}

public record CostIncrement(decimal CostUsd);

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
