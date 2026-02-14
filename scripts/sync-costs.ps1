# Sync OpenClaw session costs to AtlasControlPanel DB
# Scans all session transcripts, aggregates daily costs, and upserts into DailyCosts table

param(
    [int]$Days = 30,
    [string]$Server = "localhost",
    [string]$Database = "AtlasControlPanel"
)

$sessionsPath = "$env:USERPROFILE\.openclaw\agents\main\sessions"
$startDate = (Get-Date).AddDays(-$Days).ToString("yyyy-MM-dd")

# Aggregate costs by date
$dailyCosts = @{}
Get-ChildItem $sessionsPath -Filter "*.jsonl" -ErrorAction SilentlyContinue | ForEach-Object {
    Get-Content $_.FullName | ForEach-Object {
        try {
            $o = $_ | ConvertFrom-Json
            if ($o.message.usage.cost.total -and $o.timestamp) {
                $date = $o.timestamp.Substring(0, 10)
                if ($date -ge $startDate) {
                    if (-not $dailyCosts.ContainsKey($date)) { $dailyCosts[$date] = 0.0 }
                    $dailyCosts[$date] += [double]$o.message.usage.cost.total
                }
            }
        } catch {}
    }
}

# Build SQL to upsert daily costs
$sql = ""
foreach ($kv in $dailyCosts.GetEnumerator()) {
    $date = $kv.Key
    $cost = [Math]::Round($kv.Value, 4)
    $sql += "MERGE DailyCosts AS t USING (VALUES ('$date', $cost)) AS s(Date, Cost) ON t.Date = s.Date WHEN MATCHED THEN UPDATE SET Cost = s.Cost WHEN NOT MATCHED THEN INSERT (Date, Cost) VALUES (s.Date, s.Cost);`n"
}

if ($sql) {
    sqlcmd -S $Server -d $Database -E -Q $sql 2>&1 | Out-Null
}

# Also update today's cost in a format the dashboard can read
$today = (Get-Date).ToString("yyyy-MM-dd")
$todayCost = if ($dailyCosts.ContainsKey($today)) { [Math]::Round($dailyCosts[$today], 2) } else { 0 }
Write-Host "Synced $($dailyCosts.Count) days. Today: `$$todayCost"
