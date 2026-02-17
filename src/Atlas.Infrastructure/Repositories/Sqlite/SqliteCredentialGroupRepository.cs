using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Dapper;

namespace Atlas.Infrastructure.Repositories.Sqlite;

public class SqliteCredentialGroupRepository(IDbConnectionFactory connectionFactory) : ICredentialGroupRepository
{
    public async Task<IEnumerable<CredentialGroup>> GetAllAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<CredentialGroupRow>(
            "SELECT * FROM CredentialGroups ORDER BY Name");
        return rows.Select(MapRow);
    }

    public async Task<CredentialGroup?> GetByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<CredentialGroupRow>(
            "SELECT * FROM CredentialGroups WHERE Id = @Id", new { Id = id.ToString() });
        return row is null ? null : MapRow(row);
    }

    public async Task<CredentialGroup?> GetByNameAsync(string name)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<CredentialGroupRow>(
            "SELECT * FROM CredentialGroups WHERE Name = @Name", new { Name = name });
        return row is null ? null : MapRow(row);
    }

    public async Task<CredentialGroup> CreateAsync(CredentialGroup group)
    {
        group.Id = Guid.NewGuid();
        group.CreatedAt = DateTime.UtcNow;
        group.UpdatedAt = DateTime.UtcNow;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO CredentialGroups (Id, Name, Category, Description, Icon, CreatedAt, UpdatedAt)
              VALUES (@Id, @Name, @Category, @Description, @Icon, @CreatedAt, @UpdatedAt)",
            new
            {
                Id = group.Id.ToString(),
                group.Name,
                group.Category,
                group.Description,
                group.Icon,
                CreatedAt = group.CreatedAt.ToString("O"),
                UpdatedAt = group.UpdatedAt.ToString("O")
            });

        return group;
    }

    public async Task DeleteAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM CredentialGroupMembers WHERE GroupId = @Id", new { Id = id.ToString() });
        await connection.ExecuteAsync("DELETE FROM CredentialGroups WHERE Id = @Id", new { Id = id.ToString() });
    }

    public async Task AddCredentialToGroupAsync(Guid groupId, Guid credentialId)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT OR IGNORE INTO CredentialGroupMembers (GroupId, CredentialId) VALUES (@GroupId, @CredentialId)",
            new { GroupId = groupId.ToString(), CredentialId = credentialId.ToString() });
    }

    public async Task RemoveCredentialFromGroupAsync(Guid groupId, Guid credentialId)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "DELETE FROM CredentialGroupMembers WHERE GroupId = @GroupId AND CredentialId = @CredentialId",
            new { GroupId = groupId.ToString(), CredentialId = credentialId.ToString() });
    }

    public async Task<IEnumerable<SecureCredential>> GetCredentialsInGroupAsync(Guid groupId)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<SqliteCredentialRow>(
            @"SELECT c.* FROM SecureCredentials c
              INNER JOIN CredentialGroupMembers m ON c.Id = m.CredentialId
              WHERE m.GroupId = @GroupId",
            new { GroupId = groupId.ToString() });

        return rows.Select(r => new SecureCredential
        {
            Id = Guid.Parse(r.Id),
            Name = r.Name ?? "",
            Category = r.Category ?? "",
            Username = r.Username,
            Description = r.Description,
            StorageKey = "********",
            CreatedAt = DateTime.Parse(r.CreatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTime.Parse(r.UpdatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
            AccessCount = r.AccessCount,
            VaultMode = r.VaultMode ?? "locked"
        });
    }

    private static CredentialGroup MapRow(CredentialGroupRow r) => new()
    {
        Id = Guid.Parse(r.Id),
        Name = r.Name ?? "",
        Category = r.Category,
        Description = r.Description,
        Icon = r.Icon,
        CreatedAt = DateTime.Parse(r.CreatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
        UpdatedAt = DateTime.Parse(r.UpdatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
    };

    private class CredentialGroupRow
    {
        public string Id { get; set; } = "";
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string CreatedAt { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
    }

    private class SqliteCredentialRow
    {
        public string Id { get; set; } = "";
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Username { get; set; }
        public string? Description { get; set; }
        public string? StorageKey { get; set; }
        public string CreatedAt { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
        public string? LastAccessedAt { get; set; }
        public int AccessCount { get; set; }
        public string? VaultMode { get; set; }
    }
}
