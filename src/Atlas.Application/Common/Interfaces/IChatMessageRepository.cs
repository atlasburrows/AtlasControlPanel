using Atlas.Domain.Entities;

namespace Atlas.Application.Common.Interfaces;

public interface IChatMessageRepository
{
    Task<IEnumerable<ChatMessage>> GetRecentAsync(int limit = 100);
    Task<ChatMessage> AddAsync(ChatMessage message);
}
