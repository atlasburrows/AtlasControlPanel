using Vigil.Domain.Entities;

namespace Vigil.Application.Common.Interfaces;

public interface ICredentialAccessLogRepository
{
    Task<IEnumerable<CredentialAccessLog>> GetByCredentialIdAsync(Guid credentialId);
    Task<IEnumerable<CredentialAccessLog>> GetAllAsync(int take = 100);
    Task CreateAsync(CredentialAccessLog log);
}
