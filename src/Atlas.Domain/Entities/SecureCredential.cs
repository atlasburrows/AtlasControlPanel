using Atlas.Domain.Common;

namespace Atlas.Domain.Entities;

public class SecureCredential : BaseEntity
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string? Username { get; set; }
    public string? Description { get; set; }
    public string StorageKey { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public int AccessCount { get; set; }
}
