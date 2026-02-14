using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.API.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController(ITaskRepository taskRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TaskItem>>> GetAll()
        => Ok(await taskRepository.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskItem>> GetById(Guid id)
    {
        var task = await taskRepository.GetByIdAsync(id);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpPost]
    public async Task<ActionResult<TaskItem>> Create(TaskItem task)
    {
        var created = await taskRepository.CreateAsync(task);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TaskItem>> Update(Guid id, TaskItem task)
    {
        task.Id = id;
        var updated = await taskRepository.UpdateAsync(task);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] TaskItemStatus status)
    {
        await taskRepository.UpdateStatusAsync(id, status);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await taskRepository.DeleteAsync(id);
        return NoContent();
    }
}
