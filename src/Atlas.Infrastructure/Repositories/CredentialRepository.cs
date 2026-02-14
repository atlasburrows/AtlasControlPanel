using System.Data;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Infrastructure.Security;
using Dapper;

namespace Atlas.Infrastructure.Repositories;

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
            credential.StorageKey
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
}
