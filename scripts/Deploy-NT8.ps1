# Deploy-NT8.ps1 — copy ALL KatTradeManager sources into NT8's Indicators\KAT subfolder with overwrite,
# remove stale flat-root copies, then verify NT8's file watcher recompiled NinjaTrader.Custom.dll.
# Pre-flight: runs Verify-Version.ps1 to abort on header/VERSION/README/UI drift (v1.57 root cause).
# Post-deploy: verifies deployed KAT\KatTradeManager.cs VERSION matches repo and file hashes match.
# Folder must match the declared NinjaTrader.NinjaScript.Indicators.KAT namespace.
# Usage:  pwsh scripts/Deploy-NT8.ps1 [-TimeoutSeconds 60] [-SkipVerify]
param(
    [int]$TimeoutSeconds = 60,
    [switch]$SkipVerify
)

$ErrorActionPreference = 'Stop'
$repoRoot   = Split-Path -Parent $PSScriptRoot
$indicators = Join-Path $env:USERPROFILE 'Documents\NinjaTrader 8\bin\Custom\Indicators'
$katDir     = Join-Path $indicators 'KAT'
$customDll  = Join-Path $env:USERPROFILE 'Documents\NinjaTrader 8\bin\Custom\NinjaTrader.Custom.dll'

# --- Pre-flight version guard (never deploy drifted version) ---
if (-not $SkipVerify) {
    $verifyScript = Join-Path $PSScriptRoot 'Verify-Version.ps1'
    if (Test-Path $verifyScript) {
        Write-Host 'Pre-flight: verifying version consistency...'
        & $verifyScript
        if ($LASTEXITCODE -ne 0) {
            throw "Deploy ABORTED: version drift detected. Fix with pwsh scripts/Bump-Version.ps1 or sync files manually, then re-run deploy."
        }
    } else {
        Write-Host 'WARNING: Verify-Version.ps1 not found — skipping pre-flight check' -ForegroundColor Yellow
    }
    # Record repo VERSION for post-deploy comparison
    $repoCs = Get-Content (Join-Path $repoRoot 'KatTradeManager.cs') -Raw
    if ($repoCs -match 'VERSION\s*=\s*"([^"]+)"') { $repoVer = $matches[1] } else { $repoVer = 'unknown' }
    if ($repoCs -match 'RELEASE_DATE\s*=\s*"([^"]+)"') { $repoDate = $matches[1] } else { $repoDate = 'unknown' }
    Write-Host "Pre-flight OK: repo v$repoVer ($repoDate) consistent."
}

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
    'src\KatTradeManager.Discipline.cs',
    'src\KatTradeManager.ProfileOps.cs',
    'src\KatAtmTemplateService.cs',
    'src\KatTradeManager.SwingOps.cs',
    'src\KatTradeManager.AccountInfo.cs',
    'src\KatTradeManager.HudFactory.cs',
    'src\KatTradeManager.HudUpdates.cs',
    'src\KatTradeManager.HudBuilder.cs',
    'src\KatTradeManager.CloseOps.cs'
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

# --- Post-deploy verification: deployed VERSION + file hash must match repo ---
if (-not $SkipVerify) {
    $deployedCs = Join-Path $katDir 'KatTradeManager.cs'
    if (Test-Path $deployedCs) {
        $depRaw = Get-Content $deployedCs -Raw
        if ($depRaw -match 'VERSION\s*=\s*"([^"]+)"') { $depVer = $matches[1] } else { $depVer = 'unknown' }
        if ($depRaw -match 'RELEASE_DATE\s*=\s*"([^"]+)"') { $depDate = $matches[1] } else { $depDate = 'unknown' }
        if ($depVer -ne $repoVer) {
            throw "Post-deploy FAIL: deployed KAT\KatTradeManager.cs v$depVer != repo v$repoVer — copy incomplete or stale."
        }
        if ($depDate -ne $repoDate) {
            throw "Post-deploy FAIL: deployed RELEASE_DATE $depDate != repo $repoDate."
        }
        Write-Host "Post-deploy verify: KAT\KatTradeManager.cs v$depVer ($depDate) matches repo."
        # Hash check for every deployed file (catches partial copy / ACL issues)
        $mismatch = @()
        foreach ($f in $files) {
            $src = Join-Path $repoRoot $f
            $leaf = Split-Path $f -Leaf
            $dst = Join-Path $katDir $leaf
            if ((Get-FileHash $src -Algorithm SHA256).Hash -ne (Get-FileHash $dst -Algorithm SHA256).Hash) {
                $mismatch += $leaf
            }
        }
        if ($mismatch.Count -gt 0) {
            throw "Post-deploy FAIL: hash mismatch for: $($mismatch -join ', ') — copy corrupted."
        }
        Write-Host "Post-deploy verify: all $($files.Count) file hashes match repo."
    }
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
