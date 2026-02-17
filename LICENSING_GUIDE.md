# Vigil - Licensing System

## Overview

The licensing system uses **signed JWT keys** verified offline with an embedded public key. No phone home required, no user database needed. The system is designed to be simple, secure, and customer-friendly.

### Architecture
- **Signed JWT tokens** (RS256 algorithm, RSA 2048)
- **Offline verification** - public key embedded in client
- **No phone home** - completely autonomous
- **Tier-based licensing** - Free, Pro, Team
- **Module-based feature unlocking** - Fine-grained control

## File Structure

```
src/
├── Atlas.Application/Licensing/
│   ├── LicenseKey.cs                 # Domain model
│   ├── LicenseValidator.cs           # JWT validation (with embedded public key)
│   ├── LicenseGenerator.cs           # Key generation (server-side only)
│   └── LicenseValidationResult.cs    # Validation result model
├── Atlas.Web/Controllers/
│   └── LicenseController.cs          # API endpoints
└── Atlas.Shared/Pages/
    └── Settings.razor               # License UI (updated with License section)

scripts/
├── generate-keypair.ps1             # Generates RSA keypair
└── generate-license.ps1             # CLI tool to generate license keys

keys/
├── private.pem                       # Private key (NEVER commit, store securely)
└── public.pem                        # Public key (embedded in code)
```

## License Tiers

### Free Tier
- Default, no key required
- Modules: `dashboard`, `tasks`, `activity`

### Pro Tier
- Requires valid license key
- Modules: `analytics`, `security`, `monitoring`, `chat`, `cost-optimization`
- Includes all Free modules

### Team Tier
- Requires valid license key
- Modules: `multi-user`, `api`
- Includes all Free and Pro modules

## API Endpoints

### GET /api/license
Returns current license status
```json
{
  "isValid": true,
  "tier": "pro",
  "email": "user@example.com",
  "modules": ["dashboard", "tasks", "activity", "analytics", ...],
  "issuedAt": "2026-02-14T00:00:00Z",
  "expiresAt": "2027-02-14T00:00:00Z",
  "licenseId": "550e8400-e29b-41d4-a716-446655440000",
  "daysUntilExpiry": 365
}
```

### POST /api/license
Activate/update a license key
```json
{
  "licenseKey": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

Returns the same format as GET on success, or error:
```json
{
  "error": "License key signature is invalid"
}
```

### DELETE /api/license
Clear the license (revert to free tier)

Returns license status for free tier.

## Generating License Keys

### Prerequisites
- OpenSSL installed
- Powershell 5.1+
- Access to `keys/private.pem`

### Generate a License Key

```powershell
powershell -ExecutionPolicy Bypass -File scripts/generate-license.ps1 `
  -Email "customer@example.com" `
  -Tier "pro" `
  -ExpiresInDays 365
```

Optional parameters:
- `-Modules "analytics,security,chat"` - Comma-separated list (uses tier defaults if not provided)
- `-LicenseId "custom-id"` - Custom license ID (defaults to new GUID)

### Example Output
```
License key generated successfully!

License Key:
eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJjdXN0b21lckBleGFtcGxlLmNvbSIsImxpY2Vuc2VfaWQiOiI1NTBlODQwMC1lMjliLTQxZDQtYTcxNi00NDY2NTU0NDAwMDAiLCJ0aWVyIjoicHJvIiwibW9kdWxlcyI6WyJkYXNoYm9hcmQiLCJ0YXNrcyIsImFjdGl2aXR5IiwiYW5hbHl0aWNzIiwic2VjdXJpdHkiLCJtb25pdG9yaW5nIiwiY2hhdCIsImNvc3Qtb3B0aW1pemF0aW9uIl0sImlzcyI6ImF0bGFzLWNvbnRyb2wtcGFuZWwiLCJpYXQiOjE3MzkxNzA0MDAsImV4cCI6MTc3MDcwNjQwMCwibmJmIjoxNzM5MTcwNDAwfQ.SIGNATURE...

License ID: 550e8400-e29b-41d4-a716-446655440000
Expires: 2/14/2027 12:00:00 AM UTC
```

## Code Usage

### Validating a License

```csharp
var validator = new LicenseValidator();
var result = validator.ValidateLicense(licenseKeyJwtString);

if (result.IsValid)
{
    var license = result.License;
    Console.WriteLine($"Tier: {license.Tier}");
    Console.WriteLine($"Expires: {license.ExpiresAt}");
}
else
{
    Console.WriteLine($"Invalid: {result.ErrorMessage}");
}
```

### Checking Module Access

```csharp
var validator = new LicenseValidator();
bool hasAnalytics = validator.IsModuleUnlocked(storedLicenseKey, "analytics");

if (!hasAnalytics)
{
    // Show upgrade prompt
}
```

### Getting Current Tier

```csharp
var validator = new LicenseValidator();
var tier = validator.GetCurrentTier(storedLicenseKey); // "free", "pro", or "team"
```

### Generating Keys (Server-Side Only)

```csharp
// Load from file
var generator = LicenseGenerator.FromFile("keys/private.pem");

// Or from environment variable
var generator = LicenseGenerator.FromEnvironment("ATLAS_LICENSE_PRIVATE_KEY");

// Generate a license
var jwtString = generator.GenerateLicense(
    email: "user@example.com",
    tier: "pro",
    modules: new List<string> { "analytics", "security" },
    expiresAt: DateTime.UtcNow.AddYears(1),
    licenseId: "custom-id"
);
```

## UI Integration

The Settings page now has a **License** section showing:
- Current tier (colored badge)
- Licensed email address
- Expiration date and days remaining
- List of unlocked modules as chips
- "Activate License" / "Update License" button
- Link to pricing page

### Activate License in UI
1. User enters license key in the dialog
2. System validates via POST to `/api/license`
3. If valid, key is stored and UI updates
4. If invalid, error message is displayed

### License Status Display
- **Green checkmark** if active premium license
- **Free badge** for free tier
- **Color-coded tier badges** (Default=Free, Warning=Pro, Success=Team)

## Security Notes

### Private Key Management
- **NEVER** commit `keys/private.pem` to version control
- Store in secure location (e.g., 1Password, Azure KeyVault)
- Load via environment variable `ATLAS_LICENSE_PRIVATE_KEY` in production
- Use file only for local development

### Public Key Security
- Embedded in `LicenseValidator.cs` as const string
- Safe to distribute - no secret material
- Can be verified against the private key

### JWT Verification
- Signature verified using embedded public key
- Expiration checked automatically
- Invalid signatures rejected
- Expired keys rejected

## Testing

### Generate a Test License
```powershell
powershell -ExecutionPolicy Bypass -File scripts/generate-license.ps1 `
  -Email "test@example.com" `
  -Tier "pro" `
  -ExpiresInDays 1
```

### Verify with curl
```bash
curl -X POST http://localhost:5000/api/license \
  -H "Content-Type: application/json" \
  -d '{"licenseKey": "eyJhbGc..."}'
```

## Webhook Integration (Future)

When Stripe webhooks are added:
1. Listen for `invoice.payment_succeeded`
2. Call `LicenseGenerator.GenerateLicense()`
3. Send key to customer email
4. Auto-activate in control panel (if user is logged in)

## Troubleshooting

### "License key signature is invalid"
- Verify the key was signed with the correct private key
- Check that public key in `LicenseValidator.cs` matches `keys/public.pem`

### "License key has expired"
- Generate a new key with extended expiration
- Check system clock on client machine

### "License key is empty"
- User must enter or paste the full JWT string
- No newlines or extra spaces

### "Failed to activate license"
- Check network connectivity to API
- Verify the license key is not malformed
- Check server logs for detailed error

## Migration Path

1. **Phase 1 (Current):** Manual key generation for testing
2. **Phase 2:** Stripe webhook integration for automatic generation
3. **Phase 3:** Customer self-service portal for license management
4. **Phase 4:** License analytics and usage tracking (via Stripe data)

## References

- JWT Standard: https://tools.ietf.org/html/rfc7519
- RSA Algorithm: https://en.wikipedia.org/wiki/RSA_(cryptosystem)
- System.IdentityModel.Tokens.Jwt: https://www.nuget.org/packages/System.IdentityModel.Tokens.Jwt/
