using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Dapper;

namespace Atlas.Infrastructure.Repositories.Sqlite;

public class SqliteChatMessageRepository(IDbConnectionFactory connectionFactory) : IChatMessageRepository
{
    public async Task<IEnumerable<ChatMessage>> GetRecentAsync(int limit = 100)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ChatMessageRow>(
            @"SELECT Id, Role, Content, CreatedAt FROM ChatMessages 
              ORDER BY CreatedAt DESC LIMIT @Limit",
            new { Limit = limit });
        
        return rows.OrderBy(r => r.CreatedAt).Select(MapRow).ToList();
    }

    public async Task<ChatMessage> AddAsync(ChatMessage message)
    {
        message.Id = Guid.NewGuid();
        message.CreatedAt = DateTime.UtcNow;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            @"INSERT INTO ChatMessages (Id, Role, Content, CreatedAt)
              VALUES (@Id, @Role, @Content, @CreatedAt)",
            new
            {
                Id = message.Id.ToString(),
                message.Role,
                message.Content,
                CreatedAt = message.CreatedAt.ToString("O")
            });

        return message;
    }

    private static ChatMessage MapRow(ChatMessageRow r) => new()
    {
        Id = Guid.Parse(r.Id),
        Role = r.Role ?? "",
        Content = r.Content ?? "",
        CreatedAt = DateTime.Parse(r.CreatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
    };

    private class ChatMessageRow
    {
        public string Id { get; set; } = "";
        public string? Role { get; set; }
        public string? Content { get; set; }
        public string CreatedAt { get; set; } = "";
    }
}
