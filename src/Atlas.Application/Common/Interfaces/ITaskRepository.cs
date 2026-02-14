using Atlas.Domain.Entities;
using Atlas.Domain.Enums;

namespace Atlas.Application.Common.Interfaces;

public interface ITaskRepository
{
    Task<IEnumerable<TaskItem>> GetAllAsync();
    Task<TaskItem?> GetByIdAsync(Guid id);
    Task<TaskItem> CreateAsync(TaskItem task);
    Task<TaskItem?> UpdateAsync(TaskItem task);
    Task UpdateStatusAsync(Guid id, TaskItemStatus status);
    Task DeleteAsync(Guid id);
}
