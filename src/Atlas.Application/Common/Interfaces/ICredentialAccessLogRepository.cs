using Atlas.Domain.Entities;

namespace Atlas.Application.Common.Interfaces;

public interface ICredentialAccessLogRepository
{
    Task<IEnumerable<CredentialAccessLog>> GetByCredentialIdAsync(Guid credentialId);
    Task<IEnumerable<CredentialAccessLog>> GetAllAsync(int take = 100);
    Task CreateAsync(CredentialAccessLog log);
}
