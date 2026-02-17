namespace Atlas.Application.Licensing;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Validates JWT-based license keys for Vigil.
/// Uses an embedded public key for offline verification - no phone home required.
/// </summary>
public class LicenseValidator
{
    /// <summary>
    /// Embedded RSA 2048 public key (SubjectPublicKeyInfo format)
    /// Generated via: openssl rsa -in private.pem -pubout -out public.pem
    /// </summary>
    private const string PUBLIC_KEY = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAq6q3un+8x/R1ej2zpEFY
oDdHrb6rzlPp/loV9st3TI/BIBCQkFrrbY/vOV3JLmCxsWsjSB7qsr5ggZcjBBqc
Wa/Vv4dFzS/rn4eGoCEnMMvpFpqgt0t80WoMvvnDRISglGWkBmDPFJAJuswdeMpy
UxFYD3tM/Uhie2Msv2kAmTSMosrTq3vz5mM8QyXnNUc6fA4GCn+iN9Vgi5WiR2fh
Glk11BEvZ81HgMwEn9xVsUloJxzS2XsRpqGSsjEVr0H0oVHE58dH4/hj1f+hLLH1
9YouVmVcBI9Q+bizLGzP5T6vIQ4ETmlO2nLDiFgoq9GKdI66xT0RoesRnhmqcYej
SwIDAQAB
-----END PUBLIC KEY-----";

    private const string ISSUER = "atlas-control-panel";
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    /// <summary>
    /// Validates a license key JWT string
    /// </summary>
    public LicenseValidationResult ValidateLicense(string? keyString)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(keyString))
            {
                return LicenseValidationResult.Failure("License key is empty");
            }

            // Parse and validate JWT
            var rsa = RSA.Create();
            rsa.ImportFromPem(PUBLIC_KEY);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new RsaSecurityKey(rsa),
                ValidateIssuer = true,
                ValidIssuer = ISSUER,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = _tokenHandler.ValidateToken(keyString, validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken)
            {
                return LicenseValidationResult.Failure("Invalid token format");
            }

            // Extract claims
            var email = principal.FindFirst("sub")?.Value;
            var tier = principal.FindFirst("tier")?.Value ?? "free";
            var modulesJson = principal.FindFirst("modules")?.Value ?? "[]";
            var licenseId = principal.FindFirst("license_id")?.Value ?? Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(email))
            {
                return LicenseValidationResult.Failure("License missing email (sub claim)");
            }

            // Parse modules
            var modules = ParseModules(modulesJson);

            var license = new LicenseKey
            {
                LicenseId = licenseId,
                Email = email,
                Tier = tier,
                Modules = modules,
                IssuedAt = jwtToken.IssuedAt,
                ExpiresAt = jwtToken.ValidTo
            };

            return LicenseValidationResult.Success(license);
        }
        catch (SecurityTokenExpiredException)
        {
            return LicenseValidationResult.Failure("License key has expired");
        }
        catch (SecurityTokenInvalidSignatureException)
        {
            return LicenseValidationResult.Failure("License key signature is invalid");
        }
        catch (Exception ex)
        {
            return LicenseValidationResult.Failure($"Failed to validate license: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the currently active license (or free tier if none is set)
    /// </summary>
    public LicenseKey GetCurrentLicense(string? storedKeyString)
    {
        var result = ValidateLicense(storedKeyString);
        
        if (result.IsValid && result.License != null)
        {
            return result.License;
        }

        // Return free tier as fallback
        return new LicenseKey
        {
            LicenseId = "free-tier",
            Email = "anonymous@atlas",
            Tier = "free",
            Modules = GetModulesForTier("free"),
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.MaxValue
        };
    }

    /// <summary>
    /// Gets the current tier ("free", "pro", or "team")
    /// </summary>
    public string GetCurrentTier(string? storedKeyString)
    {
        var license = GetCurrentLicense(storedKeyString);
        return license.Tier;
    }

    /// <summary>
    /// Checks if a specific module is unlocked
    /// </summary>
    public bool IsModuleUnlocked(string? storedKeyString, string moduleName)
    {
        var license = GetCurrentLicense(storedKeyString);
        return license.HasModule(moduleName);
    }

    /// <summary>
    /// Gets the default modules for a given tier
    /// </summary>
    public static List<string> GetModulesForTier(string tier) => tier.ToLowerInvariant() switch
    {
        "free" => ["dashboard", "tasks", "activity"],
        "pro" => ["dashboard", "tasks", "activity", "analytics", "security", "monitoring", "chat", "cost-optimization"],
        "team" => ["dashboard", "tasks", "activity", "analytics", "security", "monitoring", "chat", "cost-optimization", "multi-user", "api"],
        _ => ["dashboard", "tasks", "activity"]
    };

    /// <summary>
    /// Parses a JSON array of module names from a JWT claim
    /// </summary>
    private static List<string> ParseModules(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json) || json == "[]")
                return [];

            // Simple JSON array parser for module names
            var trimmed = json.Trim();
            if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']'))
                return [];

            var content = trimmed[1..^1]; // Remove [ and ]
            if (string.IsNullOrWhiteSpace(content))
                return [];

            var modules = content
                .Split(',')
                .Select(m => m.Trim().Trim('"'))
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToList();

            return modules;
        }
        catch
        {
            return [];
        }
    }
}
