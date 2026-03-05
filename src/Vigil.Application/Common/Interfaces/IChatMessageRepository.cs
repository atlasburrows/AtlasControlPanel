using Vigil.Domain.Entities;

namespace Vigil.Application.Common.Interfaces;

public interface IChatMessageRepository
{
    Task<IEnumerable<ChatMessage>> GetRecentAsync(int limit = 100);
    Task<ChatMessage> AddAsync(ChatMessage message);
}
