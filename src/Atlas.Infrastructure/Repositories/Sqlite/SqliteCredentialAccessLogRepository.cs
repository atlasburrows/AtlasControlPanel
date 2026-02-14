using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Microsoft.Data.Sqlite;

namespace Atlas.Infrastructure.Repositories.Sqlite;

public class SqliteCredentialAccessLogRepository(string connectionString) : ICredentialAccessLogRepository
{
    public async Task<IEnumerable<CredentialAccessLog>> GetByCredentialIdAsync(Guid credentialId)
    {
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, CredentialId, CredentialName, Requester, AccessedAt, VaultMode, AutoApproved, Details
            FROM CredentialAccessLog 
            WHERE CredentialId = @CredentialId 
            ORDER BY AccessedAt DESC";
        command.Parameters.AddWithValue("@CredentialId", credentialId.ToString());

        var logs = new List<CredentialAccessLog>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            logs.Add(new CredentialAccessLog
            {
                Id = Guid.Parse(reader["Id"].ToString()!),
                CredentialId = Guid.Parse(reader["CredentialId"].ToString()!),
                CredentialName = reader["CredentialName"].ToString() ?? "",
                Requester = reader["Requester"]?.ToString(),
                AccessedAt = DateTime.Parse(reader["AccessedAt"].ToString()!),
                VaultMode = reader["VaultMode"].ToString() ?? "locked",
                AutoApproved = reader["AutoApproved"].ToString() == "1",
                Details = reader["Details"]?.ToString()
            });
        }
        return logs;
    }

    public async Task<IEnumerable<CredentialAccessLog>> GetAllAsync(int take = 100)
    {
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, CredentialId, CredentialName, Requester, AccessedAt, VaultMode, AutoApproved, Details
            FROM CredentialAccessLog 
            ORDER BY AccessedAt DESC
            LIMIT @Take";
        command.Parameters.AddWithValue("@Take", take);

        var logs = new List<CredentialAccessLog>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            logs.Add(new CredentialAccessLog
            {
                Id = Guid.Parse(reader["Id"].ToString()!),
                CredentialId = Guid.Parse(reader["CredentialId"].ToString()!),
                CredentialName = reader["CredentialName"].ToString() ?? "",
                Requester = reader["Requester"]?.ToString(),
                AccessedAt = DateTime.Parse(reader["AccessedAt"].ToString()!),
                VaultMode = reader["VaultMode"].ToString() ?? "locked",
                AutoApproved = reader["AutoApproved"].ToString() == "1",
                Details = reader["Details"]?.ToString()
            });
        }
        return logs;
    }

    public async Task CreateAsync(CredentialAccessLog log)
    {
        using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO CredentialAccessLog (Id, CredentialId, CredentialName, Requester, AccessedAt, VaultMode, AutoApproved, Details)
            VALUES (@Id, @CredentialId, @CredentialName, @Requester, @AccessedAt, @VaultMode, @AutoApproved, @Details)";
        command.Parameters.AddWithValue("@Id", log.Id.ToString());
        command.Parameters.AddWithValue("@CredentialId", log.CredentialId.ToString());
        command.Parameters.AddWithValue("@CredentialName", log.CredentialName);
        command.Parameters.AddWithValue("@Requester", log.Requester ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@AccessedAt", log.AccessedAt);
        command.Parameters.AddWithValue("@VaultMode", log.VaultMode);
        command.Parameters.AddWithValue("@AutoApproved", log.AutoApproved ? 1 : 0);
        command.Parameters.AddWithValue("@Details", log.Details ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }
}
