using Atlas.Domain.Entities;

namespace Atlas.Application.Common.Interfaces;

public interface ICredentialRepository
{
    Task<IEnumerable<SecureCredential>> GetAllAsync();
    Task<SecureCredential?> GetByIdAsync(Guid id);
    Task<SecureCredential?> GetByNameAsync(string name);
    Task<SecureCredential> CreateAsync(SecureCredential credential);
    Task DeleteAsync(Guid id);
    Task RecordAccessAsync(Guid id);
    Task<string?> GetDecryptedStorageKeyAsync(Guid id);
    Task UpdateVaultModeAsync(Guid id, string vaultMode);
    Task<string?> GetVaultModeAsync(Guid id);
}
