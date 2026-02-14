using Microsoft.AspNetCore.DataProtection;

namespace Atlas.Infrastructure.Security;

public interface ICredentialEncryption
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    bool IsEncrypted(string value);
}

public class CredentialEncryption : ICredentialEncryption
{
    private const string Purpose = "Atlas.CredentialVault.v1";
    private const string EncryptedPrefix = "ENC:";
    private readonly IDataProtector _protector;

    public CredentialEncryption(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        if (IsEncrypted(plainText)) return plainText; // already encrypted
        return EncryptedPrefix + _protector.Protect(plainText);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;
        if (!IsEncrypted(cipherText)) return cipherText; // not encrypted (legacy)
        return _protector.Unprotect(cipherText[EncryptedPrefix.Length..]);
    }

    public bool IsEncrypted(string value)
        => value?.StartsWith(EncryptedPrefix) == true;
}
