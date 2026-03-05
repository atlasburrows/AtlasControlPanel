using Vigil.Domain.Entities;

namespace Vigil.Application.Common.Interfaces;

public interface IActivityRepository
{
    Task<IEnumerable<ActivityLog>> GetAllAsync(int take = 50);
    Task<ActivityLog?> GetByIdAsync(Guid id);
    Task<ActivityLog> CreateAsync(ActivityLog log);
    Task<IEnumerable<ActivityLog>> GetByTaskIdAsync(Guid taskId);
}
