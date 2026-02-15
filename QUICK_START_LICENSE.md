# Quick Start: License Key System

## Setup

The license system is now fully integrated! Here's what was created:

### Core Files
✅ `src/Atlas.Application/Licensing/LicenseKey.cs` - Domain model  
✅ `src/Atlas.Application/Licensing/LicenseValidator.cs` - JWT validator with embedded public key  
✅ `src/Atlas.Application/Licensing/LicenseGenerator.cs` - Key signing (server-side)  
✅ `src/Atlas.Application/Licensing/LicenseValidationResult.cs` - Result model  
✅ `src/Atlas.Web/Controllers/LicenseController.cs` - REST API endpoints  
✅ `src/Atlas.Shared/Pages/Settings.razor` - License UI section  

### Keys
✅ `keys/private.pem` - RSA 2048 private key (generated)  
✅ `keys/public.pem` - RSA 2048 public key (generated)  

### Scripts
✅ `scripts/generate-keypair.ps1` - Generate RSA keypair  
✅ `scripts/generate-license.ps1` - Generate license keys  

### Documentation
✅ `LICENSING_GUIDE.md` - Complete reference  

## Quick Test

### Step 1: Build the project
```bash
cd src/Atlas.Web
dotnet build
```

This will restore the new NuGet packages:
- `System.IdentityModel.Tokens.Jwt` v8.2.2
- `Microsoft.IdentityModel.Tokens` v8.2.2

### Step 2: Start the application
```bash
dotnet run
```

### Step 3: Generate a test license key

**Simple way (using OpenSSL directly):**

```bash
# Create a test license JWT
Email="test@example.com"
Tier="pro"
LicenseId=$(uuidgen)  # macOS/Linux
# Or on Windows PowerShell: LicenseId = [guid]::NewGuid()

# Sign with OpenSSL (manual JWT creation)
# See LICENSING_GUIDE.md for detailed steps
```

**Or update your hosts/environment and use the PowerShell script:**

```powershell
# From Atlas root directory
cd scripts
pwsh -ExecutionPolicy Bypass -File generate-license.ps1 -Email "demo@example.com" -Tier "pro" -ExpiresInDays 365
```

### Step 4: Test the API

**Get current license:**
```bash
curl http://localhost:5000/api/license
# Returns: { "tier": "free", "modules": ["dashboard", "tasks", "activity"], ... }
```

**Activate a license:**
```bash
curl -X POST http://localhost:5000/api/license \
  -H "Content-Type: application/json" \
  -d '{"licenseKey": "YOUR_JWT_STRING_HERE"}'
```

**Clear the license:**
```bash
curl -X DELETE http://localhost:5000/api/license
```

### Step 5: Test the UI

1. Open http://localhost:5000/settings
2. Look for the new **License** section (purple card with license icon)
3. Click "Activate License"
4. Paste a valid license key JWT and click "Activate"
5. See the license status update immediately

## Module Availability by Tier

### Free (Default)
```
✓ Dashboard
✓ Tasks  
✓ Activity Log
```

### Pro
```
✓ All Free modules
✓ Analytics
✓ Security
✓ Monitoring
✓ Chat
✓ Cost Optimization
```

### Team
```
✓ All Pro modules
✓ Multi-user Management
✓ API Access
```

## Generate Your First License

### Sample: Pro Tier (1 year)
```powershell
# Adjust path to your project
cd C:\Users\mikal\source\repos\AtlasControlPanel\scripts

powershell -ExecutionPolicy Bypass { 
    & '.\generate-license.ps1' `
        -Email "customer@company.com" `
        -Tier "pro" `
        -ExpiresInDays 365
}
```

**Output:** A long JWT string that looks like:
```
eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJjdXN0b21lckBjb21wYW55LmNvbSIsImxpY2Vuc2VfaWQiOiI1NTBlODQwMC1lMjliLTQxZDQtYTcxNi00NDY2NTU0NDAwMDAiLCJ0aWVyIjoicHJvIiwibW9kdWxlcyI6WyJkYXNoYm9hcmQiLCJ0YXNrcyIsImFjdGl2aXR5IiwiYW5hbHl0aWNzIiwic2VjdXJpdHkiLCJtb25pdG9yaW5nIiwiY2hhdCIsImNvc3Qtb3B0aW1pemF0aW9uIl0sImlzcyI6ImF0bGFzLWNvbnRyb2wtcGFuZWwiLCJpYXQiOjE3MzkxNzA0MDAsImV4cCI6MTc3MDcwNjQwMCwibmJmIjoxNzM5MTcwNDAwfQ.SIGNATURE_HERE
```

Copy this entire string and paste into the Settings → License dialog.

## How It Works

1. **Key Generation** (Offline, Admin Only)
   - Admin runs `generate-license.ps1`
   - System signs JWT with private key (`keys/private.pem`)
   - Returns signed JWT string
   - Customer receives via email

2. **Key Activation** (Client-Side)
   - User pastes JWT into Settings dialog
   - Client sends to `/api/license` POST endpoint
   - Server validates signature using embedded public key
   - No phone home — validation is 100% offline
   - Key is stored locally on client
   - UI updates immediately

3. **Feature Gating** (Client-Side)
   - Components check `IsModuleUnlocked("module-name")`
   - Free tier has limited modules
   - Pro/Team unlock additional modules
   - No verification server needed

## Environment Configuration

**For Production:**

Set the private key via environment variable instead of file:

```bash
# Linux/Mac
export ATLAS_LICENSE_PRIVATE_KEY="$(cat /secure/path/to/private.pem)"

# Windows PowerShell
$env:ATLAS_LICENSE_PRIVATE_KEY = Get-Content "C:\secure\path\private.pem" -Raw

# Then use in code
var generator = LicenseGenerator.FromEnvironment("ATLAS_LICENSE_PRIVATE_KEY");
```

**For Development:**

The system reads from `keys/private.pem` by default.

## Next Steps

### Immediate
- [x] Build and test the license system
- [x] Generate test licenses
- [x] Verify API endpoints
- [x] Check UI displays correctly

### Short-term
- [ ] Set up Stripe webhook integration (Phase 2)
- [ ] Auto-generate keys on payment success
- [ ] Send keys to customer email

### Medium-term  
- [ ] Create customer portal for license management
- [ ] Add license analytics dashboard
- [ ] Implement license revocation

### Long-term
- [ ] Usage-based pricing
- [ ] Trial period system
- [ ] License enforcement policies

## Troubleshooting

**Q: PowerShell script won't run**  
A: Use full path with `-ExecutionPolicy Bypass`:
```powershell
powershell -ExecutionPolicy Bypass -File "C:\full\path\to\generate-license.ps1" -Email "test@test.com" -Tier "pro"
```

**Q: "License key signature is invalid"**  
A: Ensure you're using the JWT string exactly as generated, with no extra spaces or newlines.

**Q: Keys directory not found**  
A: Run `generate-keypair.ps1` first to create RSA keys:
```powershell
cd C:\Users\mikal\source\repos\AtlasControlPanel\scripts
powershell -ExecutionPolicy Bypass -File generate-keypair.ps1
```

## Support

See `LICENSING_GUIDE.md` for complete documentation and API reference.
