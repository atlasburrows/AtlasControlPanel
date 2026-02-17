namespace Atlas.Application.Licensing;

/// <summary>
/// Domain model representing a license key for Vigil.
/// Contains licensing information including tier, modules, and expiration.
/// </summary>
public class LicenseKey
{
    /// <summary>
    /// Unique identifier for this license (typically a GUID)
    /// </summary>
    public string LicenseId { get; set; } = string.Empty;

    /// <summary>
    /// Email address of the license holder
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// License tier: "free", "pro", or "team"
    /// </summary>
    public string Tier { get; set; } = "free";

    /// <summary>
    /// List of unlocked module names
    /// Examples: "analytics", "security", "chat", "monitoring", "cost-optimization", "api", "multi-user"
    /// </summary>
    public List<string> Modules { get; set; } = [];

    /// <summary>
    /// UTC timestamp when the license was issued
    /// </summary>
    public DateTime IssuedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the license expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Checks if the license is currently valid (not expired)
    /// </summary>
    public bool IsValid => ExpiresAt > DateTime.UtcNow;

    /// <summary>
    /// Checks if a specific module is unlocked in this license
    /// </summary>
    public bool HasModule(string moduleName) 
        => !string.IsNullOrWhiteSpace(moduleName) && Modules.Contains(moduleName, StringComparer.OrdinalIgnoreCase);
}
