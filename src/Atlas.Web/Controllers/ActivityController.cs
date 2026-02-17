using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Web.Controllers;

[ApiController]
[Route("api/activity")]
public class ActivityController(IActivityRepository activityRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ActivityLog>>> GetAll([FromQuery] int take = 50)
        => Ok(await activityRepository.GetAllAsync(take));

    [HttpGet("detailed")]
    public async Task<ActionResult<IEnumerable<ActivityLog>>> GetDetailed([FromQuery] int take = 50)
    {
        var all = (await activityRepository.GetAllAsync(take)).ToList();
        var lookup = all.ToLookup(a => a.ParentId);
        var roots = all.Where(a => a.ParentId == null).ToList();
        foreach (var root in roots)
            root.SubEntries = lookup[root.Id].ToList();
        return Ok(roots);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ActivityLog>> GetById(Guid id)
    {
        var log = await activityRepository.GetByIdAsync(id);
        return log is null ? NotFound() : Ok(log);
    }

    [HttpPost]
    public async Task<ActionResult<ActivityLog>> Create([FromBody] ActivityLog log)
    {
        var created = await activityRepository.CreateAsync(log);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
