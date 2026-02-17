namespace Atlas.Application.Licensing;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Generates signed JWT license keys for Vigil.
/// This is server-side only and should NEVER be shipped to customers.
/// The private key is loaded from environment or file, never embedded in code.
/// </summary>
public class LicenseGenerator
{
    private const string ISSUER = "atlas-control-panel";
    private readonly string _privateKeyPem;

    public LicenseGenerator(string privateKeyPem)
    {
        if (string.IsNullOrWhiteSpace(privateKeyPem))
        {
            throw new ArgumentException("Private key cannot be empty", nameof(privateKeyPem));
        }

        _privateKeyPem = privateKeyPem;
    }

    /// <summary>
    /// Creates a generator by loading the private key from a file path
    /// </summary>
    public static LicenseGenerator FromFile(string privateKeyPath)
    {
        if (!File.Exists(privateKeyPath))
        {
            throw new FileNotFoundException($"Private key file not found: {privateKeyPath}");
        }

        var privateKeyPem = File.ReadAllText(privateKeyPath);
        return new LicenseGenerator(privateKeyPem);
    }

    /// <summary>
    /// Creates a generator by loading the private key from an environment variable
    /// </summary>
    public static LicenseGenerator FromEnvironment(string envVarName = "ATLAS_LICENSE_PRIVATE_KEY")
    {
        var privateKeyPem = Environment.GetEnvironmentVariable(envVarName);
        if (string.IsNullOrWhiteSpace(privateKeyPem))
        {
            throw new InvalidOperationException($"Environment variable '{envVarName}' is not set or empty");
        }

        return new LicenseGenerator(privateKeyPem);
    }

    /// <summary>
    /// Generates a signed JWT license key string
    /// </summary>
    public string GenerateLicense(LicenseKey license)
    {
        if (license == null)
        {
            throw new ArgumentNullException(nameof(license));
        }

        if (string.IsNullOrWhiteSpace(license.Email))
        {
            throw new ArgumentException("License email cannot be empty", nameof(license));
        }

        if (license.ExpiresAt <= DateTime.UtcNow)
        {
            throw new ArgumentException("License expiration date must be in the future", nameof(license));
        }

        var rsa = RSA.Create();
        rsa.ImportFromPem(_privateKeyPem);

        var credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new Claim("sub", license.Email),
            new Claim("license_id", license.LicenseId),
            new Claim("tier", license.Tier),
            new Claim("modules", JsonSerializer.Serialize(license.Modules)),
            new Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim("exp", new DateTimeOffset(license.ExpiresAt).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: ISSUER,
            claims: claims,
            notBefore: license.IssuedAt,
            expires: license.ExpiresAt,
            signingCredentials: credentials
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Generates a license key string with convenience parameters
    /// </summary>
    public string GenerateLicense(
        string email,
        string tier,
        List<string>? modules = null,
        DateTime? expiresAt = null,
        string? licenseId = null)
    {
        // Default modules based on tier if not provided
        modules ??= LicenseValidator.GetModulesForTier(tier);

        // Default: 1 year from now
        expiresAt ??= DateTime.UtcNow.AddYears(1);

        var license = new LicenseKey
        {
            LicenseId = licenseId ?? Guid.NewGuid().ToString(),
            Email = email,
            Tier = tier,
            Modules = modules,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt.Value
        };

        return GenerateLicense(license);
    }
}
