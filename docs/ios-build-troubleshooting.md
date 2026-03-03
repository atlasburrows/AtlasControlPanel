# iOS Build Troubleshooting Guide

## Current Status

**Build #19**: Failed (1m 38s)  
**Android Build**: ✅ Working  
**iOS Build**: ❌ Failing at compile step

## Root Cause

### Primary Issue: Xcode Version Mismatch

The .NET 10 MAUI iOS workload expects **Xcode 26.x**, but GitHub Actions `macos-15` runners only provide **Xcode 16.x** (16.2 as of March 2025).

This mismatch causes:
- Asset catalog processing failures (`actool` not found)
- SDK location errors
- Build failures before code signing

## Fix History

### Fix 1: MudBlazor Icon Errors (COMPLETED)
- Changed `Icons.Material.Filled.Apple` → `Icons.Material.Filled.PhoneIphone`
- Changed `Icons.Material.Filled.Google` → `Icons.Material.Filled.Cloud`
- File: `src/Atlas.Shared/Pages/Security.razor`

### Fix 2: Workflow Trigger Paths (COMPLETED)
- Updated paths to include workflow file changes

### Fix 3: Added Verbose Logging (COMPLETED)
- Added `--verbosity normal` to build commands

### Fix 4: Simplified Build (COMPLETED)
- Removed iossimulator-arm64 runtime requirement (now added back with proper handling)
- Set `/p:BuildIpa=false` for compile-only builds

### Fix 5: Xcode Selection & Runtime Identifier (CURRENT)
- Added explicit Xcode 16.2 selection step
- Added `-r iossimulator-arm64` runtime identifier
- Added `/p:RunAOTCompilation=false` to skip AOT in debug builds
- Added conditional artifact upload for build logs on failure
- Removed `UIRequiredDeviceCapabilities` arm64 requirement from Info.plist

## Diagnostic Approaches (Given Log Limitations)

Since we can't access detailed GitHub Actions logs due to tooling limitations:

### Option 1: Local Build Verification
```powershell
# Run on a Mac with Xcode 16.x installed
dotnet workload restore
dotnet build src/Atlas.MAUI/Atlas.MAUI.csproj -f net10.0-ios -c Debug /p:BuildIpa=false
```

### Option 2: Enhanced Workflow Logging
The updated workflow now:
- Lists available Xcode versions before build
- Prints active Xcode path and version
- Captures obj/ and bin/ folders on failure
- Verifies workload list before building

### Option 3: TestFlight-Only Build (Skip Simulator)
If simulator builds continue to fail:
```yaml
# Skip the debug/simulator build entirely
# Only build Release + device when credentials are configured
```

### Option 4: Use macos-14 Runner
The macos-14 runner may have different Xcode compatibility:
```yaml
runs-on: macos-14  # Try this if macos-15 continues to fail
```

## Common iOS MAUI Build Failures & Fixes

| Error Pattern | Cause | Fix |
|--------------|-------|-----|
| `xcrun: unable to find utility "actool"` | Xcode/SDK mismatch | Pin Xcode version, ensure workload matches |
| `Failed to load actool log file` | Asset catalog processing failed | Check Images/Assets.xcassets integrity |
| `Root element is missing` (plist) | Corrupted plist during failed actool | Clean build, delete obj/ folder |
| `SDK cannot be located` | Xcode path not set | Use `xcode-select -s` explicitly |
| Build succeeds but no artifact | Wrong runtime identifier path | Use `-r iossimulator-arm64` |

## Next Steps

1. **Commit the current fixes**
2. **Trigger a new build** (push to master or workflow_dispatch)
3. **If still failing**:
   - Check the uploaded build logs artifact
   - Try switching to `macos-14` runner
   - Consider using `macos-13` with older Xcode if needed

## Alternative: Manual Certificate Setup

If automatic certificate creation via API is not feasible:

1. Generate certificates locally using fastlane match or Xcode manual signing
2. Upload `.p12` and provisioning profile as GitHub secrets
3. Update workflow to import certificates from secrets

See: `FASTLANE_SETUP.md` (if exists) or Apple documentation on manual signing.
