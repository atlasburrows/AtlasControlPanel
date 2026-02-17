# Vigil Watchdog Script
# Runs as a scheduled task independently of OpenClaw/Atlas.
# Monitors both the Vigil server (Atlas.Web) and OpenClaw gateway.
# If either is down for >5 minutes, auto-restarts with known-good config.
#
# Install as scheduled task:
#   schtasks /create /tn "Vigil Watchdog" /tr "powershell -ExecutionPolicy Bypass -File C:\Users\mikal\source\repos\AtlasControlPanel\watchdog.ps1" /sc minute /mo 5 /ru "%USERNAME%"

$LogFile = "$env:USERPROFILE\.openclaw\workspace\memory\watchdog.log"
$MaxLogSize = 1MB

function Write-Log($msg) {
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$ts | $msg" | Out-File -Append -FilePath $LogFile
    # Rotate log if too big
    if ((Test-Path $LogFile) -and (Get-Item $LogFile).Length -gt $MaxLogSize) {
        $backup = $LogFile -replace '\.log$', '.old.log'
        Move-Item $LogFile $backup -Force -ErrorAction SilentlyContinue
    }
}

# --- Check Vigil Server ---
$vigilRunning = Get-Process -Name "Atlas.Web" -ErrorAction SilentlyContinue
if (-not $vigilRunning) {
    Write-Log "WARNING: Vigil server (Atlas.Web) is NOT running. Attempting restart..."
    try {
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        Start-Process -FilePath "C:\Users\mikal\source\repos\AtlasControlPanel\src\Atlas.Web\bin\Debug\net10.0\Atlas.Web.exe" `
            -ArgumentList "--urls", "http://0.0.0.0:5263;https://0.0.0.0:5264" `
            -WorkingDirectory "C:\Users\mikal\source\repos\AtlasControlPanel\src\Atlas.Web"
        Start-Sleep -Seconds 5
        $check = Get-Process -Name "Atlas.Web" -ErrorAction SilentlyContinue
        if ($check) {
            Write-Log "SUCCESS: Vigil server restarted (PID $($check.Id))"
        } else {
            Write-Log "FAILED: Vigil server did not start"
        }
    } catch {
        Write-Log "ERROR: Failed to restart Vigil: $_"
    }
} else {
    # Verify it's actually responding
    try {
        $response = Invoke-RestMethod -Uri "http://127.0.0.1:5263/api/notifications/approval-lockout/status" -TimeoutSec 5 -ErrorAction Stop
        # Server is healthy
    } catch {
        Write-Log "WARNING: Vigil process running but not responding. Restarting..."
        Stop-Process -Name "Atlas.Web" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
        $env:ASPNETCORE_ENVIRONMENT = "Development"
        Start-Process -FilePath "C:\Users\mikal\source\repos\AtlasControlPanel\src\Atlas.Web\bin\Debug\net10.0\Atlas.Web.exe" `
            -ArgumentList "--urls", "http://0.0.0.0:5263;https://0.0.0.0:5264" `
            -WorkingDirectory "C:\Users\mikal\source\repos\AtlasControlPanel\src\Atlas.Web"
        Write-Log "Vigil server restarted after unresponsive state"
    }
}

# --- Check OpenClaw Gateway ---
$gatewayRunning = Get-Process -Name "node" -ErrorAction SilentlyContinue | Where-Object {
    try {
        $cmd = (Get-CimInstance Win32_Process -Filter "ProcessId = $($_.Id)" -ErrorAction SilentlyContinue).CommandLine
        $cmd -and $cmd -match "openclaw"
    } catch { $false }
}

if (-not $gatewayRunning) {
    Write-Log "WARNING: OpenClaw gateway is NOT running. Attempting restart..."
    try {
        # Use npx to start gateway (avoids execution policy issues with .ps1)
        Start-Process -FilePath "node" `
            -ArgumentList "$env:APPDATA\npm\node_modules\openclaw\dist\index.mjs", "gateway", "start" `
            -WorkingDirectory "$env:USERPROFILE\.openclaw"
        Start-Sleep -Seconds 10
        Write-Log "OpenClaw gateway restart attempted"
    } catch {
        Write-Log "ERROR: Failed to restart OpenClaw gateway: $_"
    }
}

# --- Active Hours Check ---
$hour = (Get-Date).Hour
if ($hour -lt 8 -or $hour -ge 24) {
    # Outside active hours — just ensure processes exist, don't be noisy
    exit 0
}
