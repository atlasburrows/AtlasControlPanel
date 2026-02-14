using System.Data;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Entities;
using Dapper;

namespace Atlas.Infrastructure.Repositories;

public class ChatMessageRepository(IDbConnectionFactory connectionFactory) : IChatMessageRepository
{
    public async Task<IEnumerable<ChatMessage>> GetRecentAsync(int limit = 100)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ChatMessageRow>(
            "sp_ChatMessages_GetRecent",
            new { Limit = limit },
            commandType: CommandType.StoredProcedure);
        
        return rows.Select(MapRow);
    }

    public async Task<ChatMessage> AddAsync(ChatMessage message)
    {
        message.Id = Guid.NewGuid();
        message.CreatedAt = DateTime.UtcNow;

        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "sp_ChatMessages_Add",
            new
            {
                message.Role,
                message.Content
            },
            commandType: CommandType.StoredProcedure);

        return message;
    }

    private static ChatMessage MapRow(ChatMessageRow r) => new()
    {
        Id = r.Id,
        Role = r.Role ?? "",
        Content = r.Content ?? "",
        CreatedAt = r.CreatedAt
    };

    private class ChatMessageRow
    {
        public Guid Id { get; set; }
        public string? Role { get; set; }
        public string? Content { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
