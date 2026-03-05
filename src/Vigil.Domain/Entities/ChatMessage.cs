using Vigil.Domain.Common;

namespace Vigil.Domain.Entities;

public class ChatMessage : BaseEntity
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
