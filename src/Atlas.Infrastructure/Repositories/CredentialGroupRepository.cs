using System.Data;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Dapper;

namespace Atlas.Infrastructure.Repositories;

public class CredentialGroupRepository(IDbConnectionFactory connectionFactory) : ICredentialGroupRepository
{
    public async Task<IEnumerable<CredentialGroup>> GetAllAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryAsync<CredentialGroup>("sp_CredentialGroups_GetAll", commandType: CommandType.StoredProcedure);
    }

    public async Task<CredentialGroup?> GetByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CredentialGroup>("sp_CredentialGroups_GetById", new { Id = id }, commandType: CommandType.StoredProcedure);
    }

    public async Task<CredentialGroup?> GetByNameAsync(string name)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CredentialGroup>("sp_CredentialGroups_GetByName", new { Name = name }, commandType: CommandType.StoredProcedure);
    }

    public async Task<CredentialGroup> CreateAsync(CredentialGroup group)
    {
        using var connection = connectionFactory.CreateConnection();
        var id = await connection.QuerySingleAsync<Guid>("sp_CredentialGroups_Create", new
        {
            group.Name,
            group.Category,
            group.Description,
            group.Icon
        }, commandType: CommandType.StoredProcedure);
        group.Id = id;
        return group;
    }

    public async Task DeleteAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("sp_CredentialGroups_Delete", new { Id = id }, commandType: CommandType.StoredProcedure);
    }

    public async Task AddCredentialToGroupAsync(Guid groupId, Guid credentialId)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("sp_CredentialGroups_AddMember", new { GroupId = groupId, CredentialId = credentialId }, commandType: CommandType.StoredProcedure);
    }

    public async Task RemoveCredentialFromGroupAsync(Guid groupId, Guid credentialId)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("sp_CredentialGroups_RemoveMember", new { GroupId = groupId, CredentialId = credentialId }, commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<SecureCredential>> GetCredentialsInGroupAsync(Guid groupId)
    {
        using var connection = connectionFactory.CreateConnection();
        var credentials = await connection.QueryAsync<SecureCredential>("sp_CredentialGroups_GetMembers", new { GroupId = groupId }, commandType: CommandType.StoredProcedure);
        foreach (var cred in credentials)
            cred.StorageKey = "********";
        return credentials;
    }
}
