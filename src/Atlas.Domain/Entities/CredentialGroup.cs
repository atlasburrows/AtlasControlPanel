using Atlas.Domain.Common;

namespace Atlas.Domain.Entities;

public class CredentialGroup : BaseEntity
{
    public string Name { get; set; } = "";
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<SecureCredential>? Credentials { get; set; }
}
