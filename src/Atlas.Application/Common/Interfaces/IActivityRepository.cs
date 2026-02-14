using Atlas.Domain.Entities;

namespace Atlas.Application.Common.Interfaces;

public interface IActivityRepository
{
    Task<IEnumerable<ActivityLog>> GetAllAsync(int take = 50);
    Task<ActivityLog?> GetByIdAsync(Guid id);
    Task<ActivityLog> CreateAsync(ActivityLog log);
    Task<IEnumerable<ActivityLog>> GetByTaskIdAsync(Guid taskId);
}
