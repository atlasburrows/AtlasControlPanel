namespace Atlas.Web.Controllers;

using Atlas.Application.Licensing;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// API endpoints for managing license keys in Atlas Control Panel
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LicenseController : ControllerBase
{
    private const string LICENSE_KEY_FILE = "license.key";
    private const string APPSETTINGS_KEY = "Licensing:Key";

    private readonly LicenseValidator _licenseValidator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LicenseController> _logger;
    private readonly string _licensePath;

    public LicenseController(
        LicenseValidator licenseValidator,
        IConfiguration configuration,
        ILogger<LicenseController> logger)
    {
        _licenseValidator = licenseValidator;
        _configuration = configuration;
        _logger = logger;
        _licensePath = Path.Combine(AppContext.BaseDirectory, LICENSE_KEY_FILE);
    }

    /// <summary>
    /// Gets the current license status
    /// </summary>
    /// <returns>Current license information (tier, modules, expiry)</returns>
    [HttpGet]
    public IActionResult GetLicense()
    {
        try
        {
            var licenseKey = GetStoredLicenseKey();
            var license = _licenseValidator.GetCurrentLicense(licenseKey);

            return Ok(new LicenseStatusResponse
            {
                IsValid = license.IsValid,
                Tier = license.Tier,
                Email = license.Email,
                Modules = license.Modules,
                IssuedAt = license.IssuedAt,
                ExpiresAt = license.ExpiresAt,
                LicenseId = license.LicenseId,
                DaysUntilExpiry = license.IsValid 
                    ? (int)(license.ExpiresAt - DateTime.UtcNow).TotalDays 
                    : -1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving license status");
            return StatusCode(500, new { error = "Failed to retrieve license status" });
        }
    }

    /// <summary>
    /// Activates a new license key
    /// </summary>
    /// <param name="request">License key string to activate</param>
    /// <returns>Validation result</returns>
    [HttpPost]
    public IActionResult ActivateLicense([FromBody] ActivateLicenseRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.LicenseKey))
            {
                return BadRequest(new { error = "License key is required" });
            }

            // Validate the license key
            var result = _licenseValidator.ValidateLicense(request.LicenseKey);

            if (!result.IsValid)
            {
                _logger.LogWarning("License activation failed: {ErrorMessage}", result.ErrorMessage);
                return BadRequest(new { error = result.ErrorMessage });
            }

            // Store the license key
            StoreLicenseKey(request.LicenseKey);

            _logger.LogInformation(
                "License activated for {Email} (Tier: {Tier})",
                result.License?.Email,
                result.License?.Tier);

            return Ok(new LicenseStatusResponse
            {
                IsValid = result.License!.IsValid,
                Tier = result.License.Tier,
                Email = result.License.Email,
                Modules = result.License.Modules,
                IssuedAt = result.License.IssuedAt,
                ExpiresAt = result.License.ExpiresAt,
                LicenseId = result.License.LicenseId,
                DaysUntilExpiry = result.License.IsValid 
                    ? (int)(result.License.ExpiresAt - DateTime.UtcNow).TotalDays 
                    : -1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating license");
            return StatusCode(500, new { error = "Failed to activate license" });
        }
    }

    /// <summary>
    /// Clears the stored license key (reverts to free tier)
    /// </summary>
    [HttpDelete]
    public IActionResult ClearLicense()
    {
        try
        {
            if (System.IO.File.Exists(_licensePath))
            {
                System.IO.File.Delete(_licensePath);
            }

            _logger.LogInformation("License cleared, reverted to free tier");

            // Return free tier status
            var freeLicense = _licenseValidator.GetCurrentLicense(null);
            return Ok(new LicenseStatusResponse
            {
                IsValid = true,
                Tier = freeLicense.Tier,
                Email = freeLicense.Email,
                Modules = freeLicense.Modules,
                IssuedAt = freeLicense.IssuedAt,
                ExpiresAt = freeLicense.ExpiresAt,
                LicenseId = freeLicense.LicenseId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing license");
            return StatusCode(500, new { error = "Failed to clear license" });
        }
    }

    /// <summary>
    /// Gets the stored license key (for display/verification only)
    /// </summary>
    private string? GetStoredLicenseKey()
    {
        // First, try to get from file
        if (System.IO.File.Exists(_licensePath))
        {
            try
            {
                return System.IO.File.ReadAllText(_licensePath).Trim();
            }
            catch
            {
                // Fall through to configuration
            }
        }

        // Fall back to configuration
        return _configuration[APPSETTINGS_KEY];
    }

    /// <summary>
    /// Stores a license key to file
    /// </summary>
    private void StoreLicenseKey(string licenseKey)
    {
        // Also store in configuration if possible
        try
        {
            System.IO.File.WriteAllText(_licensePath, licenseKey.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to store license key to file, will use configuration only");
        }
    }
}

/// <summary>
/// Request to activate a license
/// </summary>
public class ActivateLicenseRequest
{
    public string? LicenseKey { get; set; }
}

/// <summary>
/// Current license status response
/// </summary>
public class LicenseStatusResponse
{
    public bool IsValid { get; set; }
    public string Tier { get; set; } = "free";
    public string Email { get; set; } = string.Empty;
    public List<string> Modules { get; set; } = [];
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string LicenseId { get; set; } = string.Empty;
    public int DaysUntilExpiry { get; set; }
}

