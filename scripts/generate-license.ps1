# Generate a signed license key for Atlas Control Panel
# Usage: powershell -ExecutionPolicy Bypass -File generate-license.ps1 -Email user@example.com -Tier pro

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$Email,

    [Parameter(Mandatory=$true)]
    [ValidateSet("free", "pro", "team")]
    [string]$Tier,

    [string]$Modules = "",
    [int]$ExpiresInDays = 365,
    [string]$LicenseId = ""
)

$ErrorActionPreference = "Stop"
$VerbosePreference = "Continue"

# Resolve paths
$ScriptDir = Split-Path -Path $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Path $ScriptDir
$PrivateKeyPath = Join-Path $ProjectRoot "keys" "private.pem"

Write-Verbose "Script dir: $ScriptDir"
Write-Verbose "Project root: $ProjectRoot"
Write-Verbose "Private key path: $PrivateKeyPath"

if (-not (Test-Path $PrivateKeyPath)) {
    Write-Error "Private key not found at: $PrivateKeyPath"
    exit 1
}

Write-Host "Generating license key..." -ForegroundColor Cyan
Write-Host "  Email: $Email"
Write-Host "  Tier: $Tier"
Write-Host "  Expires in: $ExpiresInDays days"

# Default modules per tier
$defaultModules = @{
    "free" = @("dashboard", "tasks", "activity")
    "pro" = @("dashboard", "tasks", "activity", "analytics", "security", "monitoring", "chat", "cost-optimization")
    "team" = @("dashboard", "tasks", "activity", "analytics", "security", "monitoring", "chat", "cost-optimization", "multi-user", "api")
}

# Use provided modules or defaults
if ([string]::IsNullOrWhiteSpace($Modules)) {
    $moduleList = $defaultModules[$Tier]
} else {
    $moduleList = @($Modules -split "," | ForEach-Object { $_.Trim() })
}

Write-Host "  Modules: $($moduleList -join ', ')"
Write-Host ""

# Generate license ID if not provided
if ([string]::IsNullOrWhiteSpace($LicenseId)) {
    $LicenseId = [guid]::NewGuid().ToString()
}

# Calculate expiration
$issuedAt = [DateTime]::UtcNow
$expiresAt = $issuedAt.AddDays($ExpiresInDays)

# Create JWT header and payload
$header = @{
    "alg" = "RS256"
    "typ" = "JWT"
} | ConvertTo-Json -Compress

$payload = @{
    "sub" = $Email
    "license_id" = $LicenseId
    "tier" = $Tier
    "modules" = $moduleList
    "iss" = "atlas-control-panel"
    "iat" = [int]([DateTimeOffset]::UtcNow).ToUnixTimeSeconds()
    "exp" = [int]([DateTimeOffset]$expiresAt).ToUnixTimeSeconds()
    "nbf" = [int]([DateTimeOffset]$issuedAt).ToUnixTimeSeconds()
} | ConvertTo-Json -Compress

# Base64url encode header and payload
$headerB64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($header)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
$payloadB64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
$unsignedToken = "$headerB64.$payloadB64"

# Sign with OpenSSL
$tempUnsignedFile = New-TemporaryFile
$tempSignatureFile = New-TemporaryFile

try {
    $unsignedToken | Out-File -FilePath $tempUnsignedFile -NoNewline -Encoding ASCII
    
    # Create signature
    $signOutput = & openssl dgst -sha256 -sign $PrivateKeyPath -out $tempSignatureFile $tempUnsignedFile 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "OpenSSL signing failed: $signOutput"
        exit 1
    }
    
    # Read and encode signature
    $signatureBytes = [System.IO.File]::ReadAllBytes($tempSignatureFile)
    $signatureB64 = [Convert]::ToBase64String($signatureBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    
    # Construct final JWT
    $jwt = "$unsignedToken.$signatureB64"
    
    Write-Host "License key generated successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "License Key:" -ForegroundColor Yellow
    Write-Host $jwt
    Write-Host ""
    Write-Host "License ID: $LicenseId" -ForegroundColor Cyan
    Write-Host "Expires: $expiresAt UTC" -ForegroundColor Cyan
}
finally {
    if (Test-Path $tempUnsignedFile) { Remove-Item -Path $tempUnsignedFile -Force }
    if (Test-Path $tempSignatureFile) { Remove-Item -Path $tempSignatureFile -Force }
}
