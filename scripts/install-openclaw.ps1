#!/usr/bin/env pwsh
<#
.SYNOPSIS
Installs and patches OpenClaw with the latest fixes.

.DESCRIPTION
Complete setup script that:
1. Verifies Node.js 18+ is installed
2. Installs OpenClaw globally via npm if not present
3. Applies the splitToolExecuteArgs patch (PR #14982)
4. Returns the OpenClaw installation path

.PARAMETER SkipPatch
Skip applying the patch after installation.

.PARAMETER Force
Force reinstall of OpenClaw even if already installed.

.EXAMPLE
.\install-openclaw.ps1
.\install-openclaw.ps1 -SkipPatch
.\install-openclaw.ps1 -Force

.NOTES
Production-quality installation script with comprehensive error handling.
#>

param(
    [switch]$SkipPatch,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Color output functions
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = 'White'
    )
    Write-Host $Message -ForegroundColor $Color
}

function Write-Success {
    param([string]$Message)
    Write-ColorOutput "✓ $Message" 'Green'
}

function Write-Error-Custom {
    param([string]$Message)
    Write-ColorOutput "✗ $Message" 'Red'
}

function Write-Warning-Custom {
    param([string]$Message)
    Write-ColorOutput "⚠ $Message" 'Yellow'
}

function Write-Info {
    param([string]$Message)
    Write-ColorOutput "ℹ $Message" 'Cyan'
}

# Check if Node.js is installed and meets version requirement
function Test-NodeJS {
    try {
        $versionOutput = cmd /c node --version 2>$null
        if (-not $versionOutput) {
            return @{ Installed = $false; Version = $null }
        }

        # Parse version string (e.g., "v22.18.0" -> 22)
        $version = $versionOutput -replace '^v(\d+).*', '$1'
        [int]$majorVersion = $version

        if ($majorVersion -lt 18) {
            return @{ Installed = $true; Version = $versionOutput.Trim(); MeetsRequirement = $false }
        }

        return @{ Installed = $true; Version = $versionOutput.Trim(); MeetsRequirement = $true }
    }
    catch {
        return @{ Installed = $false; Version = $null }
    }
}

# Get npm installation path
function Get-NpmPath {
    try {
        $npmPath = cmd /c where npm 2>$null
        if ($npmPath) {
            return $npmPath.Trim()
        }
    }
    catch {
        # Continue with fallback
    }

    # Fallback paths
    $fallbacks = @(
        "C:\Program Files\nodejs\npm.cmd",
        "C:\Program Files (x86)\nodejs\npm.cmd",
        "$env:ProgramFiles\nodejs\npm.cmd"
    )

    foreach ($path in $fallbacks) {
        if (Test-Path $path) {
            return $path
        }
    }

    return $null
}

# Get npm global prefix
function Get-NpmGlobalPrefix {
    try {
        $prefix = cmd /c npm config get prefix 2>$null
        if ($prefix) {
            return $prefix.Trim()
        }
    }
    catch {
        # Use default
    }
    
    return "$env:APPDATA\npm"
}

# Check if OpenClaw is already installed
function Test-OpenClawInstalled {
    param([string]$NpmPrefix)

    $paths = @(
        "$NpmPrefix\node_modules\openclaw\dist\gateway\gateway-server.cjs",
        "$NpmPrefix\..\npm\node_modules\openclaw\dist\gateway\gateway-server.cjs"
    )

    foreach ($path in $paths) {
        if (Test-Path $path) {
            return @{ 
                Installed = $true; 
                Path = (Split-Path (Split-Path (Split-Path $path -Parent) -Parent) -Parent)
            }
        }
    }

    return @{ Installed = $false; Path = $null }
}

# Install OpenClaw via npm
function Install-OpenClaw {
    param([string]$NpmPath)

    Write-Info "Installing OpenClaw..."
    
    try {
        & cmd /c "$NpmPath install -g openclaw" 2>&1 | ForEach-Object {
            if ($_ -match 'error' -or $_ -match 'ERR') {
                Write-Warning-Custom $_
            }
            else {
                Write-Info $_
            }
        }

        if ($LASTEXITCODE -ne 0) {
            Write-Error-Custom "npm install failed with exit code: $LASTEXITCODE"
            return $false
        }

        Write-Success "OpenClaw installed successfully"
        return $true
    }
    catch {
        Write-Error-Custom "Failed to install OpenClaw: $_"
        return $false
    }
}

# Run the patcher
function Invoke-Patcher {
    param([string]$OpenClawPath)

    $scriptPath = Join-Path (Split-Path $MyInvocation.MyCommand.Path) "patch-openclaw.ps1"
    
    if (-not (Test-Path $scriptPath)) {
        Write-Warning-Custom "Patcher script not found at: $scriptPath"
        Write-Warning-Custom "Skipping patch application"
        return $false
    }

    Write-Info "Applying OpenClaw patches..."
    
    try {
        & $scriptPath -OpenClawPath $OpenClawPath
        return $?
    }
    catch {
        Write-Error-Custom "Patcher failed: $_"
        return $false
    }
}

# Main execution
Write-ColorOutput @"
╔════════════════════════════════════════════════════════════════╗
║           OpenClaw Installation & Patcher Script               ║
║                   (Version 1.0)                                ║
╚════════════════════════════════════════════════════════════════╝
"@ 'Cyan'

Write-Info ""

# Step 1: Check Node.js
Write-Info "Step 1: Checking Node.js installation..."
Write-Info "==========================================`n"

$nodeStatus = Test-NodeJS

if (-not $nodeStatus.Installed) {
    Write-Error-Custom "Node.js is not installed."
    Write-Info "Please install Node.js 18+ from https://nodejs.org/"
    exit 1
}

Write-Success "Node.js found: $($nodeStatus.Version)"

if (-not $nodeStatus.MeetsRequirement) {
    Write-Error-Custom "Node.js version must be 18 or higher"
    Write-Info "Current version: $($nodeStatus.Version)"
    Write-Info "Please upgrade from https://nodejs.org/"
    exit 1
}

Write-Success "Version requirement met (18+)`n"

# Step 2: Find npm
Write-Info "Step 2: Finding npm installation..."
Write-Info "====================================`n"

$npmPath = Get-NpmPath

if (-not $npmPath) {
    Write-Error-Custom "npm could not be found"
    exit 1
}

Write-Success "npm found: $npmPath"
$npmPrefix = Get-NpmGlobalPrefix
Write-Info "Global prefix: $npmPrefix`n"

# Step 3: Check if OpenClaw is installed
Write-Info "Step 3: Checking OpenClaw installation..."
Write-Info "==========================================`n"

$openclawStatus = Test-OpenClawInstalled -NpmPrefix $npmPrefix

if ($openclawStatus.Installed -and -not $Force) {
    Write-Success "OpenClaw is already installed"
    Write-Info "Location: $($openclawStatus.Path)`n"
}
else {
    if ($Force -and $openclawStatus.Installed) {
        Write-Warning-Custom "Force reinstall requested"
    }
    
    Write-Info "Installing OpenClaw..."
    
    if (-not (Install-OpenClaw -NpmPath $npmPath)) {
        Write-Error-Custom "Failed to install OpenClaw"
        exit 1
    }

    # Re-check installation
    $openclawStatus = Test-OpenClawInstalled -NpmPrefix $npmPrefix
    
    if (-not $openclawStatus.Installed) {
        Write-Error-Custom "OpenClaw installation verification failed"
        exit 1
    }

    Write-Success "OpenClaw installation verified`n"
}

# Step 4: Apply patcher
if (-not $SkipPatch) {
    Write-Info "Step 4: Applying OpenClaw patches..."
    Write-Info "====================================`n"
    
    if (-not (Invoke-Patcher -OpenClawPath $openclawStatus.Path)) {
        Write-Warning-Custom "Patch application had issues, but installation is complete"
        Write-Info "You may need to run patch-openclaw.ps1 manually later"
    }
}

# Final summary
Write-Host ""
Write-ColorOutput "╔════════════════════════════════════════════════════════════════╗" 'Cyan'
Write-ColorOutput "║                    Installation Complete!                      ║" 'Cyan'
Write-ColorOutput "╚════════════════════════════════════════════════════════════════╝" 'Cyan'

Write-Success "OpenClaw is ready to use"
Write-Info "Installation Path: $($openclawStatus.Path)"
Write-Host ""

Write-Info "Next steps:"
Write-Host "  1. Add to PATH or use: openclaw --version"
Write-Host "  2. Start the gateway: openclaw gateway start"
Write-Host "  3. View status: openclaw gateway status"
Write-Host ""

exit 0
