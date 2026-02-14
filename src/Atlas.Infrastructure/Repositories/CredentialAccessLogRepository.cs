using System.Data;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Dapper;

namespace Atlas.Infrastructure.Repositories;

public class CredentialAccessLogRepository(IDbConnectionFactory connectionFactory) : ICredentialAccessLogRepository
{
    public async Task<IEnumerable<CredentialAccessLog>> GetByCredentialIdAsync(Guid credentialId)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryAsync<CredentialAccessLog>(
            "SELECT * FROM CredentialAccessLog WHERE CredentialId = @CredentialId ORDER BY AccessedAt DESC",
            new { CredentialId = credentialId });
    }

    public async Task<IEnumerable<CredentialAccessLog>> GetAllAsync(int take = 100)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QueryAsync<CredentialAccessLog>(
            "SELECT TOP (@Take) * FROM CredentialAccessLog ORDER BY AccessedAt DESC",
            new { Take = take });
    }

    public async Task CreateAsync(CredentialAccessLog log)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO CredentialAccessLog (Id, CredentialId, CredentialName, Requester, AccessedAt, VaultMode, AutoApproved, Details)
              VALUES (@Id, @CredentialId, @CredentialName, @Requester, @AccessedAt, @VaultMode, @AutoApproved, @Details)",
            new
            {
                log.Id,
                log.CredentialId,
                log.CredentialName,
                log.Requester,
                log.AccessedAt,
                log.VaultMode,
                log.AutoApproved,
                log.Details
            });
    }
}
