using System.Data;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Atlas.Infrastructure.Security;
using Dapper;

namespace Atlas.Infrastructure.Repositories.Sqlite;

public class SqliteCredentialRepository(IDbConnectionFactory connectionFactory, ICredentialEncryption encryption) : ICredentialRepository
{
    public async Task<IEnumerable<SecureCredential>> GetAllAsync()
    {
        using var connection = connectionFactory.CreateConnection();
        var credentials = await connection.QueryAsync<SecureCredentialRow>("SELECT * FROM SecureCredentials");
        
        // Don't decrypt StorageKey for listing — only expose metadata
        return credentials.Select(r => MapRow(r, maskStorageKey: true));
    }

    public async Task<SecureCredential?> GetByIdAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SecureCredentialRow>(
            "SELECT * FROM SecureCredentials WHERE Id = @Id",
            new { Id = id.ToString() });
        return row is null ? null : MapRow(row, maskStorageKey: true);
    }

    public async Task<SecureCredential?> GetByNameAsync(string name)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SecureCredentialRow>(
            "SELECT * FROM SecureCredentials WHERE Name = @Name LIMIT 1",
            new { Name = name });
        return row is null ? null : MapRow(row, maskStorageKey: true);
    }

    public async Task<SecureCredential> CreateAsync(SecureCredential credential)
    {
        // Encrypt before storing
        credential.StorageKey = encryption.Encrypt(credential.StorageKey);

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO SecureCredentials (Id, Name, Category, Username, Description, StorageKey, 
                CreatedAt, UpdatedAt, LastAccessedAt, AccessCount)
              VALUES (@Id, @Name, @Category, @Username, @Description, @StorageKey, 
                @CreatedAt, @UpdatedAt, @LastAccessedAt, @AccessCount)",
            new
            {
                Id = credential.Id.ToString(),
                credential.Name,
                credential.Category,
                credential.Username,
                credential.Description,
                credential.StorageKey,
                CreatedAt = credential.CreatedAt.ToString("O"),
                UpdatedAt = credential.UpdatedAt.ToString("O"),
                LastAccessedAt = credential.LastAccessedAt?.ToString("O"),
                AccessCount = credential.AccessCount
            });

        credential.StorageKey = "********"; // Don't return the encrypted value
        return credential;
    }

    public async Task DeleteAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM SecureCredentials WHERE Id = @Id", new { Id = id.ToString() });
    }

    public async Task RecordAccessAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"UPDATE SecureCredentials 
              SET LastAccessedAt = @LastAccessedAt, AccessCount = AccessCount + 1
              WHERE Id = @Id",
            new { LastAccessedAt = DateTime.UtcNow.ToString("O"), Id = id.ToString() });
    }

    public async Task<string?> GetDecryptedStorageKeyAsync(Guid id)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SecureCredentialRow>(
            "SELECT StorageKey FROM SecureCredentials WHERE Id = @Id",
            new { Id = id.ToString() });
        
        if (row is null) return null;

        // Record the access
        await RecordAccessAsync(id);

        return encryption.Decrypt(row.StorageKey);
    }

    private static SecureCredential MapRow(SecureCredentialRow r, bool maskStorageKey = false) => new()
    {
        Id = Guid.Parse(r.Id),
        Name = r.Name,
        Category = r.Category,
        Username = r.Username,
        Description = r.Description,
        StorageKey = maskStorageKey ? "********" : r.StorageKey,
        CreatedAt = DateTime.Parse(r.CreatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
        UpdatedAt = DateTime.Parse(r.UpdatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
        LastAccessedAt = string.IsNullOrEmpty(r.LastAccessedAt) ? null : DateTime.Parse(r.LastAccessedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
        AccessCount = r.AccessCount
    };

    private class SecureCredentialRow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string? Username { get; set; }
        public string? Description { get; set; }
        public string StorageKey { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public string UpdatedAt { get; set; } = "";
        public string? LastAccessedAt { get; set; }
        public int AccessCount { get; set; }
    }
}
