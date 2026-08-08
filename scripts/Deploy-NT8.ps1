# Deploy-NT8.ps1 — copy ALL KatTradeManager sources into NT8's Indicators\KAT subfolder with overwrite,
# remove stale flat-root copies, then verify NT8's file watcher recompiled NinjaTrader.Custom.dll.
# Folder must match the declared NinjaTrader.NinjaScript.Indicators.KAT namespace.
# Usage:  pwsh scripts/Deploy-NT8.ps1 [-TimeoutSeconds 60]
param(
    [int]$TimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
$repoRoot   = Split-Path -Parent $PSScriptRoot
$indicators = Join-Path $env:USERPROFILE 'Documents\NinjaTrader 8\bin\Custom\Indicators'
$katDir     = Join-Path $indicators 'KAT'
$customDll  = Join-Path $env:USERPROFILE 'Documents\NinjaTrader 8\bin\Custom\NinjaTrader.Custom.dll'

$files = @(
    'KatTradeManager.cs',
    'src\KatTradeManagerUI.cs',
    'src\KatTradeManager.HudDrag.cs',
    'src\KatTradeManager.OrderOps.cs',
    'src\KatTradeManager.Queue.cs',
    'src\KatTradeManager.AtmMerge.cs',
    'src\KatTradeManager.DailyRisk.cs',
    'src\KatTradeManager.Properties.cs',
    'src\KatTradeCalculator.cs',
    'src\KatAtmXmlParser.cs',
    'src\KatTradeManager.Discipline.cs'
)

# Removed sources that must not linger in NT8 (it compiles the folder recursively).
$stale = @('KatTradeManager.FreezeTrail.cs')

New-Item -ItemType Directory -Path $katDir -Force | Out-Null

foreach ($name in $stale) {
    foreach ($p in @((Join-Path $katDir $name), (Join-Path $indicators $name))) {
        if (Test-Path $p) { Remove-Item $p -Force; Write-Host "removed stale: $name" }
    }
}
# Generic orphan sweep: any .cs in KAT not in deploy list is stale (handles renames/splits)
$allowedLeaves = $files | ForEach-Object { Split-Path $_ -Leaf }
if (Test-Path $katDir) {
    Get-ChildItem -Path $katDir -Filter *.cs -ErrorAction SilentlyContinue | Where-Object { $allowedLeaves -notcontains $_.Name } | ForEach-Object {
        Remove-Item $_.FullName -Force
        Write-Host "removed orphan: KAT\$($_.Name)"
    }
}

$deployTime = Get-Date
foreach ($f in $files) {
    $src = Join-Path $repoRoot $f
    if (-not (Test-Path $src)) { throw "Missing source: $f" }
    $name = Split-Path $f -Leaf
    $dst = Join-Path $katDir $name
    Copy-Item $src $dst -Force
    $flat = Join-Path $indicators $name
    if (Test-Path $flat) { Remove-Item $flat -Force }
    Write-Host "deployed: KAT\$name"
}

# Final atomic nudge: the NT8 file-watcher can recompile MID-copy (new KatTradeManager.cs +
# stale UI file = mismatched dll). Nudging ALL files after the last copy guarantees one more
# recompile that sees the complete, consistent source set.
Start-Sleep -Seconds 1
$finalStamp = (Get-Date).AddSeconds(2)
foreach ($f in $files) {
    (Get-Item (Join-Path $katDir (Split-Path $f -Leaf))).LastWriteTime = $finalStamp
}

# NT8 recompiles automatically when NinjaTrader is running. A newer dll = accepted; older = rejected
# (open NinjaScript Editor for errors). Skip wait silently when NT8 is not running.
$ntRunning = Get-Process -Name 'NinjaTrader' -ErrorAction SilentlyContinue
if (-not $ntRunning) {
    Write-Host 'NinjaTrader not running — files deployed; recompile happens on next start.'
    exit 0
}

$deadline = $deployTime.AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    if ((Test-Path $customDll) -and (Get-Item $customDll).LastWriteTime -gt $deployTime) {
        Write-Host 'OK: NinjaTrader.Custom.dll recompiled — deploy accepted.'
        exit 0
    }
    Start-Sleep -Seconds 2
}

Write-Host 'WARNING: NinjaTrader.Custom.dll not recompiled within timeout — check NinjaScript Editor for compile errors.'
exit 1
