using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.Common.Interfaces;

namespace Atlas.Infrastructure.Security;

/// <summary>
/// Cross-platform secret store using AES-256 encryption with a machine-specific key.
/// Secrets are stored in an encrypted JSON file.
/// </summary>
public class EncryptedFileSecretStore : ISecretStore
{
    private readonly string _filePath;
    private readonly byte[] _key;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public EncryptedFileSecretStore(string? dataDirectory = null)
    {
        var dir = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AtlasControlPanel");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, ".vault");

        // Derive key from machine name + username (deterministic per machine)
        // Users can override with ATLAS_VAULT_KEY environment variable
        var keySource = Environment.GetEnvironmentVariable("ATLAS_VAULT_KEY")
            ?? $"{Environment.MachineName}:{Environment.UserName}:atlas-vault-v1";
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(keySource));
    }

    public async Task SetSecretAsync(string key, string value)
    {
        await _lock.WaitAsync();
        try
        {
            var secrets = await LoadAsync();
            secrets[key] = value;
            await SaveAsync(secrets);
        }
        finally { _lock.Release(); }
    }

    public async Task<string?> GetSecretAsync(string key)
    {
        await _lock.WaitAsync();
        try
        {
            var secrets = await LoadAsync();
            return secrets.TryGetValue(key, out var val) ? val : null;
        }
        finally { _lock.Release(); }
    }

    public async Task DeleteSecretAsync(string key)
    {
        await _lock.WaitAsync();
        try
        {
            var secrets = await LoadAsync();
            secrets.Remove(key);
            await SaveAsync(secrets);
        }
        finally { _lock.Release(); }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        await _lock.WaitAsync();
        try
        {
            var secrets = await LoadAsync();
            return secrets.ContainsKey(key);
        }
        finally { _lock.Release(); }
    }

    private async Task<Dictionary<string, string>> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, string>();

        try
        {
            var encrypted = await File.ReadAllBytesAsync(_filePath);
            var json = Decrypt(encrypted);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }
        catch
        {
            // Corrupted file — start fresh
            return new Dictionary<string, string>();
        }
    }

    private async Task SaveAsync(Dictionary<string, string> secrets)
    {
        var json = JsonSerializer.Serialize(secrets);
        var encrypted = Encrypt(json);
        await File.WriteAllBytesAsync(_filePath, encrypted);
    }

    private byte[] Encrypt(string plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length); // Prepend IV
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs, Encoding.UTF8))
        {
            sw.Write(plaintext);
        }
        return ms.ToArray();
    }

    private string Decrypt(byte[] ciphertext)
    {
        using var aes = Aes.Create();
        aes.Key = _key;

        var iv = new byte[aes.IV.Length];
        Array.Copy(ciphertext, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var ms = new MemoryStream(ciphertext, iv.Length, ciphertext.Length - iv.Length);
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var sr = new StreamReader(cs, Encoding.UTF8);
        return sr.ReadToEnd();
    }
}
