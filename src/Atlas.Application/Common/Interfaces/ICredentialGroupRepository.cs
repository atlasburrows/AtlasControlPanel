using Atlas.Domain.Entities;

namespace Atlas.Application.Common.Interfaces;

public interface ICredentialGroupRepository
{
    Task<IEnumerable<CredentialGroup>> GetAllAsync();
    Task<CredentialGroup?> GetByIdAsync(Guid id);
    Task<CredentialGroup?> GetByNameAsync(string name);
    Task<CredentialGroup> CreateAsync(CredentialGroup group);
    Task DeleteAsync(Guid id);
    Task AddCredentialToGroupAsync(Guid groupId, Guid credentialId);
    Task RemoveCredentialFromGroupAsync(Guid groupId, Guid credentialId);
    Task<IEnumerable<SecureCredential>> GetCredentialsInGroupAsync(Guid groupId);
}
