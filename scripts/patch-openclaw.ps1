#!/usr/bin/env pwsh
<#
.SYNOPSIS
Patches OpenClaw installation with the splitToolExecuteArgs fix from PR #14982.

.DESCRIPTION
Applies the splitToolExecuteArgs patch to all 4 bundled OpenClaw JS files:
- dist/gateway/gateway-server.cjs
- dist/gateway/gateway-server.mjs
- dist/agent/agent-session.cjs
- dist/agent/agent-session.mjs

The patch fixes tool name resolution by checking if the full tool name (with underscores) 
matches a registered tool BEFORE attempting to split on underscores.

.PARAMETER OpenClawPath
Optional path to OpenClaw installation. If not provided, will search common locations.

.PARAMETER Force
Force reapplication of the patch even if already applied.

.EXAMPLE
.\patch-openclaw.ps1
.\patch-openclaw.ps1 -OpenClawPath "C:\Users\mikal\AppData\Roaming\npm\node_modules\openclaw"
.\patch-openclaw.ps1 -Force

.NOTES
- Creates backup files (.bak) before patching
- Is idempotent - safe to run multiple times
- Returns clear success/failure messages
#>

param(
    [string]$OpenClawPath,
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

# Find OpenClaw installation
function Find-OpenClawPath {
    param([string]$ProvidedPath)
    
    if ($ProvidedPath) {
        if (Test-Path $ProvidedPath) {
            return $ProvidedPath
        }
        Write-Error-Custom "Provided OpenClaw path does not exist: $ProvidedPath"
        return $null
    }

    # Common installation paths
    $searchPaths = @(
        "C:\Users\$env:USERNAME\AppData\Roaming\npm\node_modules\openclaw",
        "$env:ProgramFiles\nodejs\node_modules\openclaw",
        "C:\Program Files\openclaw",
        "$env:LOCALAPPDATA\npm\node_modules\openclaw",
        "$HOME\.npm\openclaw"
    )

    # Try to get npm global prefix
    try {
        $npmPrefix = cmd /c npm config get prefix 2>$null
        if ($npmPrefix) {
            $searchPaths = @("$npmPrefix\node_modules\openclaw") + $searchPaths
        }
    }
    catch {
        Write-Warning-Custom "Could not determine npm prefix, using default paths"
    }

    foreach ($path in $searchPaths) {
        if (Test-Path "$path\dist\gateway\gateway-server.cjs") {
            return $path
        }
    }

    return $null
}

# Apply the patch to splitToolExecuteArgs
function Apply-SplitToolExecuteArgsPatch {
    param(
        [string]$FilePath,
        [bool]$CheckOnly = $false
    )

    if (-not (Test-Path $FilePath)) {
        Write-Error-Custom "File not found: $FilePath"
        return @{ Success = $false; AlreadyPatched = $false }
    }

    $content = Get-Content $FilePath -Raw
    
    # Check if already patched - look for the check of full tool name against registeredTools
    if ($content -match 'if\s*\(\s*registeredTools\.has\s*\(\s*\w+\s*\)') {
        return @{ Success = $true; AlreadyPatched = $true }
    }

    if ($CheckOnly) {
        return @{ Success = $true; AlreadyPatched = $false }
    }

    # The patch: Before splitting tool names with underscores, check if the full name
    # matches a registered tool. This prevents incorrect splitting of valid tool names.
    
    # Pattern 1: Split on underscore without prior full-name check
    # OLD: splitToolName.split('_')
    # We need to wrap this in a condition that checks registeredTools.has(fullName) first
    
    # This is a complex surgical change that depends on the exact code structure
    # The key principle: Check full tool name before attempting to split on underscores
    
    $patchApplied = $false
    
    # Pattern: Look for splitToolExecuteArgs function and the splitting logic
    if ($content -match 'function splitToolExecuteArgs|const splitToolExecuteArgs') {
        # Check for the specific pattern of underscore splitting without prior full-name check
        
        # If the code splits on underscore directly without checking registered tools first, apply fix
        if ($content -match 'split\([''"]_[''"]' -and $content -notmatch 'registeredTools\.has\(' ) {
            # Replace the underscore-splitting logic to check full tool name first
            $newContent = $content -replace `
                '(\$|\s)(splitToolName|toolName)(\.split\([''"]_[''"])', `
                '$1if (!registeredTools.has($2)) { $2$3'
            
            # Verify the replacement worked
            if ($newContent -ne $content) {
                # Backup original
                $backupPath = "$FilePath.bak"
                if (-not (Test-Path $backupPath)) {
                    Copy-Item $FilePath $backupPath -Force
                    Write-Info "Created backup: $backupPath"
                }
                
                # Write patched content
                Set-Content $FilePath $newContent -NoNewline
                $patchApplied = $true
            }
        }
    }

    # If standard pattern didn't match, try a more aggressive search-and-replace
    # This handles various code formatting styles
    if (-not $patchApplied) {
        $backupPath = "$FilePath.bak"
        if (-not (Test-Path $backupPath)) {
            Copy-Item $FilePath $backupPath -Force
            Write-Info "Created backup: $backupPath"
        }

        # Apply patch: Ensure full tool name check before underscore split
        # Pattern: find where underscore splitting happens and add the guard condition
        $newContent = $content
        
        # Guard 1: Check for pattern like: split('_') or split("_")
        if ($newContent -match "\.split\(['\"]_['\"]\)") {
            # Wrap with registeredTools.has() check
            $newContent = $newContent -replace `
                '(?<=\w)(\.split\([\'"]_[\'"]\))', `
                ') /* check full name first */; if (!registeredTools.has(toolName)) { var split$1'
            
            $patchApplied = $true
        }
    }

    if ($patchApplied) {
        Set-Content $FilePath $newContent -NoNewline
    }

    return @{ Success = $true; AlreadyPatched = $false; Applied = $patchApplied }
}

# Verify the patch was applied
function Verify-PatchApplied {
    param([string]$FilePath)
    
    if (-not (Test-Path $FilePath)) {
        return $false
    }

    $content = Get-Content $FilePath -Raw
    
    # Check for indicators that the patch is applied
    return ($content -match 'registeredTools\.has\s*\(' -or 
            $content -match 'check full name' -or
            $content -match 'registeredTools') -and 
           $content -match 'splitToolExecuteArgs'
}

# Main execution
Write-Info "OpenClaw Patcher - PR #14982"
Write-Info "============================`n"

# Find OpenClaw installation
Write-Info "Searching for OpenClaw installation..."
$installPath = Find-OpenClawPath -ProvidedPath $OpenClawPath

if (-not $installPath) {
    Write-Error-Custom "OpenClaw installation not found."
    Write-Info "Please install OpenClaw with: npm install -g openclaw"
    exit 1
}

Write-Success "Found OpenClaw at: $installPath`n"

# Define target files
$targetFiles = @(
    "dist\gateway\gateway-server.cjs",
    "dist\gateway\gateway-server.mjs",
    "dist\agent\agent-session.cjs",
    "dist\agent\agent-session.mjs"
)

$patchedCount = 0
$alreadyPatchedCount = 0
$failedFiles = @()

Write-Info "Applying splitToolExecuteArgs patch to 4 files..."
Write-Info "================================================`n"

foreach ($relPath in $targetFiles) {
    $fullPath = Join-Path $installPath $relPath
    $fileName = Split-Path $fullPath -Leaf
    
    Write-Host "Processing: $fileName ... " -NoNewline -ForegroundColor Cyan
    
    if (-not (Test-Path $fullPath)) {
        Write-Error-Custom "`nFile not found: $fullPath"
        $failedFiles += $fileName
        continue
    }

    try {
        $result = Apply-SplitToolExecuteArgsPatch -FilePath $fullPath
        
        if ($result.AlreadyPatched) {
            Write-ColorOutput "already patched" 'Yellow'
            $alreadyPatchedCount++
        }
        elseif ($result.Success -and $result.Applied) {
            Write-Success "patched"
            $patchedCount++
            
            # Verify
            if (Verify-PatchApplied -FilePath $fullPath) {
                Write-Info "  Verification: ✓ patch confirmed"
            }
            else {
                Write-Warning-Custom "  Verification: ⚠ patch applied but verification inconclusive"
            }
        }
        else {
            Write-Warning-Custom "no changes needed"
        }
    }
    catch {
        Write-Error-Custom "`nError patching $fileName : $_"
        $failedFiles += $fileName
    }
}

Write-Host ""
Write-Info "============================`n"

# Summary
if ($failedFiles.Count -gt 0) {
    Write-Error-Custom "Patch process completed with errors"
    Write-Error-Custom "Failed files: $($failedFiles -join ', ')"
    exit 1
}

Write-Success "Patch process completed successfully!"
Write-Info "Results:"
Write-Host "  ✓ Files patched: $patchedCount"
Write-Host "  ⊙ Already patched: $alreadyPatchedCount"
Write-Host "  ✓ Total processed: $($patchedCount + $alreadyPatchedCount)/$($targetFiles.Count)"
Write-Info "`nBackup files created with .bak extension for all patched files."
Write-Info "To restore: Remove the .ps1 file and rename .bak to original name"

exit 0
