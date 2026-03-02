# iOS TestFlight Setup Guide

## Overview
This GitHub Actions workflow automatically builds and deploys the Vigil iOS app to TestFlight.

## Required GitHub Secrets

You need to add these secrets to your GitHub repository:

### 1. Code Signing Secrets

**APPLE_CERTIFICATE_P12**
- Your Apple Distribution certificate (.p12 file) base64 encoded
- Used to sign the iOS app

**APPLE_CERTIFICATE_PASSWORD**
- Password for the .p12 certificate file

**APPLE_PROVISIONING_PROFILE**
- Your App Store provisioning profile (.mobileprovision) base64 encoded
- Must match the App ID: `com.zenidolabs.vigil`

### 2. App Store Connect API Secrets

**APPLE_ISSUER_ID**
- Issuer ID from App Store Connect → Users and Access → Keys
- Looks like: `12345678-1234-1234-1234-123456789012`

**APPLE_KEY_ID**
- Key ID from your App Store Connect API key
- Looks like: `ABC123DEF4`

**APPLE_API_KEY**
- The actual private key (.p8 file) content
- Download from App Store Connect when creating the API key

## How to Generate Secrets

### Step 1: Create App Store Connect API Key
1. Go to https://appstoreconnect.apple.com
2. Users and Access → Keys
3. Click "+" to add new key
4. Name: "GitHub Actions CI"
5. Access: App Manager (or Admin)
6. Download the .p8 file (can only download once!)
7. Note the Issuer ID and Key ID

### Step 2: Create Signing Certificate
1. Go to https://developer.apple.com
2. Certificates, Identifiers & Profiles
3. Create "Apple Distribution" certificate
4. Download and export as .p12 with password

### Step 3: Create Provisioning Profile
1. Certificates, Identifiers & Profiles → Profiles
2. Create "App Store" profile
3. Select App ID: `com.zenidolabs.vigil`
4. Select your distribution certificate
5. Download the .mobileprovision file

### Step 4: Encode Files for GitHub Secrets

```bash
# Encode certificate
base64 -i certificate.p12 | pbcopy
# Paste into APPLE_CERTIFICATE_P12 secret

# Encode provisioning profile  
base64 -i profile.mobileprovision | pbcopy
# Paste into APPLE_PROVISIONING_PROFILE secret

# API key is just the text content of the .p8 file
# Paste into APPLE_API_KEY secret
```

## Adding Secrets to GitHub

1. Go to your GitHub repo → Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Add each secret from above

## Running the Workflow

### Manual Trigger
1. Go to Actions → Build & Deploy iOS to TestFlight
2. Click "Run workflow"
3. Select branch (master)
4. Choose whether to deploy to TestFlight
5. Click "Run workflow"

### Automatic Trigger
- Any push to `master` branch that changes MAUI code will trigger a build
- Debug build runs automatically
- For TestFlight deployment, use manual trigger

## TestFlight Testing

Once uploaded:
1. Go to https://appstoreconnect.apple.com → My Apps → Vigil → TestFlight
2. You'll see the build under "Internal Testing"
3. Add yourself as an internal tester
4. Download TestFlight app on your iOS device
5. Accept the invitation and install Vigil

## Troubleshooting

**Build fails with signing error**
- Check certificate and provisioning profile match
- Verify App ID in Info.plist matches provisioning profile
- Ensure certificate is not expired

**Upload fails to TestFlight**
- Verify App Store Connect API key has App Manager access
- Check that bundle identifier matches what's in App Store Connect
- Ensure you've completed App Store privacy questionnaire

**Build succeeds but not in TestFlight**
- Check email for processing errors from Apple
- Verify app passes automated validation
- Check for missing icons, permissions, or privacy manifest
