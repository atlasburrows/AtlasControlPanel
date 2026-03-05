using System.Data;
using Vigil.Application.Common.Interfaces;
using Vigil.Domain.Entities;
using Vigil.Infrastructure.Security;
using Dapper;

namespace Vigil.Infrastructure.Repositories;

public class CredentialRepository(IDbConnectionFactory connectionFactory, ICredentialEncryption encryption) : ICredentialRepository
{
    public async Task<IEnumerable<SecureCredential>> GetAllAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        var credentials = await connection.QueryAsync<SecureCredential>("sp_Credentials_GetAll", commandType: CommandType.StoredProcedure);
        // Don't decrypt StorageKey for listing — only expose metadata
        foreach (var cred in credentials)
            cred.StorageKey = "********";
        return credentials;
    }

    public async Task<SecureCredential?> GetByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var cred = await connection.QuerySingleOrDefaultAsync<SecureCredential>("sp_Credentials_GetById", new { Id = id }, commandType: CommandType.StoredProcedure);
        if (cred is not null)
            cred.StorageKey = "********";
        return cred;
    }

    public async Task<SecureCredential?> GetByNameAsync(string name)
    {
        using var connection = connectionFactory.CreateConnection();
        var cred = await connection.QuerySingleOrDefaultAsync<SecureCredential>(
            "SELECT TOP 1 * FROM SecureCredentials WHERE Name = @Name", new { Name = name });
        if (cred is not null)
            cred.StorageKey = "********";
        return cred;
    }

    public async Task<SecureCredential> CreateAsync(SecureCredential credential)
    {
        // Encrypt before storing
        credential.StorageKey = encryption.Encrypt(credential.StorageKey);
        
        using var connection = connectionFactory.CreateConnection();
        var id = await connection.QuerySingleAsync<Guid>("sp_Credentials_Create", new
        {
            credential.Name,
            credential.Category,
            credential.Username,
            credential.Description,
            credential.StorageKey,
            VaultMode = credential.VaultMode ?? "locked"
        }, commandType: CommandType.StoredProcedure);
        credential.Id = id;
        credential.StorageKey = "********"; // Don't return the encrypted value
        return credential;
    }

    public async Task DeleteAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("sp_Credentials_Delete", new { Id = id }, commandType: CommandType.StoredProcedure);
    }

    public async Task RecordAccessAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("sp_Credentials_RecordAccess", new { Id = id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<string?> GetDecryptedStorageKeyAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var cred = await connection.QuerySingleOrDefaultAsync<SecureCredential>("sp_Credentials_GetById", new { Id = id }, commandType: CommandType.StoredProcedure);
        if (cred is null) return null;
        
        // Record the access
        await RecordAccessAsync(id);
        
        return encryption.Decrypt(cred.StorageKey);
    }

    public async Task UpdateVaultModeAsync(Guid id, string vaultMode)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("UPDATE SecureCredentials SET VaultMode = @VaultMode WHERE Id = @Id", 
            new { VaultMode = vaultMode, Id = id });
    }

    public async Task<string?> GetVaultModeAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT VaultMode FROM SecureCredentials WHERE Id = @Id", new { Id = id });
    }

    public async Task<IEnumerable<CredentialGroup>> GetAllGroupsAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryAsync<CredentialGroup>(
            "SELECT Id, Name, Category, Description, Icon, CreatedAt, UpdatedAt FROM CredentialGroups ORDER BY Name");
    }

    public async Task<IEnumerable<CredentialGroupMembership>> GetAllGroupMembershipsAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryAsync<CredentialGroupMembership>(
            "SELECT GroupId, CredentialId FROM CredentialGroupMembers");
    }
}
