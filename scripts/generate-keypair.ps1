# Generate RSA 2048 keypair for license signing
# Uses OpenSSL and outputs C# const for embedding public key

$ErrorActionPreference = "Stop"

# Create keys directory if it doesn't exist
$keysDir = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) "keys"
New-Item -ItemType Directory -Path $keysDir -Force | Out-Null

# Paths
$privateKeyPath = Join-Path $keysDir "private.pem"
$publicKeyPath = Join-Path $keysDir "public.pem"

Write-Host "Generating RSA 2048 keypair using OpenSSL..." -ForegroundColor Cyan

# Generate private key
openssl genrsa -out $privateKeyPath 2048 2>$null

# Extract public key
openssl rsa -in $privateKeyPath -pubout -out $publicKeyPath 2>$null

Write-Host "✓ Private key saved to: $privateKeyPath" -ForegroundColor Green
Write-Host "✓ Public key saved to: $publicKeyPath" -ForegroundColor Green

# Read the public key
$publicKeyPem = Get-Content -Path $publicKeyPath -Raw

# Output C# const string for embedding in LicenseValidator
Write-Host ""
Write-Host "C# Const for LicenseValidator.cs:" -ForegroundColor Yellow
Write-Host "=================================" -ForegroundColor Yellow
Write-Host ""

# Create the C# const with proper escaping
$csharpConst = 'private const string PUBLIC_KEY = @"'
$csharpConst += "`r`n"
$csharpConst += $publicKeyPem.TrimEnd()
$csharpConst += "`r`n"
$csharpConst += '";'

Write-Host $csharpConst
Write-Host ""
Write-Host "Copy this const into LicenseValidator.cs" -ForegroundColor Yellow
