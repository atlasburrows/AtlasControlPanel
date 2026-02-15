namespace Atlas.Application.Licensing;

/// <summary>
/// Result of validating a license key
/// </summary>
public class LicenseValidationResult
{
    /// <summary>
    /// Whether the license key is valid and currently active
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Error message if validation failed
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// The parsed and validated license key (if successful)
    /// </summary>
    public LicenseKey? License { get; set; }

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    public static LicenseValidationResult Success(LicenseKey license) 
        => new() { IsValid = true, License = license };

    /// <summary>
    /// Creates a failed validation result
    /// </summary>
    public static LicenseValidationResult Failure(string errorMessage) 
        => new() { IsValid = false, ErrorMessage = errorMessage };
}
