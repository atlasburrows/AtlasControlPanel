using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Security;

public interface IApprovalTokenService
{
    string GenerateToken(Guid requestId);
    bool ValidateToken(Guid requestId, string token);
}

public class ApprovalTokenService : IApprovalTokenService
{
    private readonly byte[] _secret;

    public ApprovalTokenService(IConfiguration config, ILogger<ApprovalTokenService> logger)
    {
        var secretBase64 = config["Security:ApprovalHmacSecret"];
        if (!string.IsNullOrEmpty(secretBase64))
        {
            _secret = Convert.FromBase64String(secretBase64);
            logger.LogInformation("HMAC approval secret loaded from configuration");
        }
        else
        {
            // Generate a new secret and persist it
            _secret = RandomNumberGenerator.GetBytes(32);
            config["Security:ApprovalHmacSecret"] = Convert.ToBase64String(_secret);
            logger.LogWarning("Generated new HMAC approval secret. Add to appsettings.json to persist across restarts: Security:ApprovalHmacSecret = {Secret}", Convert.ToBase64String(_secret));
            
            // Try to persist to appsettings.json
            PersistSecret(Convert.ToBase64String(_secret), logger);
        }
    }

    public string GenerateToken(Guid requestId)
    {
        using var hmac = new HMACSHA256(_secret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(requestId.ToString()));
        return Base64UrlEncode(hash);
    }

    public bool ValidateToken(Guid requestId, string token)
    {
        var expected = GenerateToken(requestId);
        // Support partial token matching (callback_data has 64-char limit, token may be truncated)
        if (token.Length < expected.Length)
        {
            var expectedPrefix = expected[..token.Length];
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedPrefix),
                Encoding.UTF8.GetBytes(token));
        }
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(token));
    }

    private static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static void PersistSecret(string secretBase64, ILogger logger)
    {
        try
        {
            var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            // Also check working directory
            if (!File.Exists(appSettingsPath))
                appSettingsPath = "appsettings.json";
            if (!File.Exists(appSettingsPath))
            {
                logger.LogWarning("appsettings.json not found, HMAC secret will not persist across restarts");
                return;
            }

            var json = File.ReadAllText(appSettingsPath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            using var ms = new MemoryStream();
            using var writer = new System.Text.Json.Utf8JsonWriter(ms, new System.Text.Json.JsonWriterOptions { Indented = true });
            writer.WriteStartObject();

            bool wroteSection = false;
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name == "Security")
                {
                    writer.WriteStartObject("Security");
                    foreach (var inner in prop.Value.EnumerateObject())
                    {
                        if (inner.Name == "ApprovalHmacSecret") continue; // replace
                        inner.WriteTo(writer);
                    }
                    writer.WriteString("ApprovalHmacSecret", secretBase64);
                    writer.WriteEndObject();
                    wroteSection = true;
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }

            if (!wroteSection)
            {
                writer.WriteStartObject("Security");
                writer.WriteString("ApprovalHmacSecret", secretBase64);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.Flush();

            File.WriteAllBytes(appSettingsPath, ms.ToArray());
            logger.LogInformation("HMAC approval secret persisted to appsettings.json");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist HMAC secret to appsettings.json");
        }
    }
}
