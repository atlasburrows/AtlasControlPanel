namespace Atlas.Application.Common.Interfaces;

/// <summary>
/// Abstraction for secret storage. Implementations can target:
/// - Windows Credential Manager
/// - macOS Keychain
/// - Encrypted file-based storage (cross-platform default)
/// </summary>
public interface ISecretStore
{
    /// <summary>Store a secret value by key.</summary>
    Task SetSecretAsync(string key, string value);

    /// <summary>Retrieve a secret value by key. Returns null if not found.</summary>
    Task<string?> GetSecretAsync(string key);

    /// <summary>Delete a secret by key.</summary>
    Task DeleteSecretAsync(string key);

    /// <summary>Check if a secret exists.</summary>
    Task<bool> ExistsAsync(string key);
}
