using Vigil.Domain.Entities;

namespace Vigil.Application.Common.Interfaces;

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
    
    /// <summary>
    /// Get all credentials in a group by the group's name (for group credential requests).
    /// </summary>
    Task<IEnumerable<SecureCredential>> GetCredentialsInGroupByNameAsync(string groupName);
}
