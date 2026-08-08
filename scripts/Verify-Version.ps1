# Verify-Version.ps1 — fail-fast guard against version drift (header vs VERSION constant vs UI vs README vs DIARY).
# Root cause v1.57: header Version: 1.57 but VERSION = "1.56" -> repo looked 1.57, NT8 compiled 1.56.
# This script is the single source of truth for deploy pre-flight and CI gate.
# Usage:  pwsh scripts/Verify-Version.ps1            # check only, exit 1 on mismatch
#         pwsh scripts/Verify-Version.ps1 -Strict     # also fail if DIARY latest != repo version
param(
    [switch]$Strict
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$csPath     = Join-Path $repoRoot 'KatTradeManager.cs'
$uiPath     = Join-Path $repoRoot 'src\KatTradeManagerUI.cs'
$readmePath = Join-Path $repoRoot 'README.md'
$diaryPath  = Join-Path $repoRoot 'DIARY.md'

function Fail($msg) { Write-Host "VERSION MISMATCH: $msg" -ForegroundColor Red; exit 1 }
function Info($msg) { Write-Host $msg }

# --- Parse KatTradeManager.cs ---
$cs = Get-Content $csPath -Raw
if ($cs -notmatch 'Version:\s*(\d+\.\d+)') { Fail "header Version not found in KatTradeManager.cs" }
$headerVer = $matches[1]
if ($cs -notmatch 'Version:.*\((\d{4}-\d{2}-\d{2})\)') { Fail "header date not found in KatTradeManager.cs" }
$headerDate = $matches[1]
if ($cs -notmatch 'VERSION\s*=\s*"([^"]+)"') { Fail "VERSION constant not found in KatTradeManager.cs" }
$constVer = $matches[1]
if ($cs -notmatch 'RELEASE_DATE\s*=\s*"([^"]+)"') { Fail "RELEASE_DATE not found in KatTradeManager.cs" }
$constDate = $matches[1]

# --- Parse UI header ---
$uiVer = $null; $uiDate = $null
if (Test-Path $uiPath) {
    $ui = Get-Content $uiPath -Raw
    if ($ui -match 'v(\d+\.\d+)') { $uiVer = $matches[1] }
    if ($ui -match 'v\d+\.\d+\s*\((\d{4}-\d{2}-\d{2})\)') { $uiDate = $matches[1] }
}

# --- Parse README.md ---
$readmeVer = $null; $readmeDate = $null
if (Test-Path $readmePath) {
    $rm = Get-Content $readmePath -Raw
    if ($rm -match 'v(\d+\.\d+)') { $readmeVer = $matches[1] }
    # README line: `v1.57` (Released: `2026-08-08`) -> grab last date on that line
    if ($rm -match 'Current Version.*(\d{4}-\d{2}-\d{2})') { $readmeDate = $matches[1] }
}

# --- Parse DIARY.md latest entry ---
$diaryVer = $null; $diaryDate = $null
if (Test-Path $diaryPath) {
    $diary = Get-Content $diaryPath -Raw
    if ($diary -match '### \[v(\d+\.\d+)\]\s*[—\-]\s*(\d{4}-\d{2}-\d{2})') {
        $diaryVer = $matches[1]; $diaryDate = $matches[2]
    }
}

Info "  KatTradeManager.cs header : v$headerVer ($headerDate)"
Info "  KatTradeManager.cs VERSION: v$constVer ($constDate)"
if ($uiVer) { Info "  KatTradeManagerUI.cs header: v$uiVer $(if($uiDate){"($uiDate)"})" }
if ($readmeVer) { Info "  README.md               : v$readmeVer $(if($readmeDate){"($readmeDate)"})" }
if ($diaryVer) { Info "  DIARY.md latest         : v$diaryVer ($diaryDate)" }

# --- Assertions (hard fail) ---
if ($headerVer -ne $constVer) { Fail "header v$headerVer != VERSION v$constVer — run pwsh scripts/Bump-Version.ps1 or fix manually" }
if ($headerDate -ne $constDate) { Fail "header date $headerDate != RELEASE_DATE $constDate" }
if ($uiVer -and $uiVer -ne $constVer) { Fail "UI header v$uiVer != VERSION v$constVer" }
if ($uiDate -and $uiDate -ne $constDate) { Fail "UI date $uiDate != RELEASE_DATE $constDate" }
if ($readmeVer -and $readmeVer -ne $constVer) { Fail "README v$readmeVer != VERSION v$constVer" }
if ($readmeDate -and $readmeDate -ne $constDate) { Fail "README date $readmeDate != RELEASE_DATE $constDate" }

# DIARY strict only when requested (CI strict)
if ($Strict -and $diaryVer -and $diaryVer -ne $constVer) { Fail "DIARY latest v$diaryVer != VERSION v$constVer (Strict)" }
if ($Strict -and $diaryDate -and $diaryDate -ne $constDate) { Fail "DIARY date $diaryDate != RELEASE_DATE $constDate (Strict)" }

# Warning (non-blocking) for DIARY drift in normal mode
if (-not $Strict -and $diaryVer -and $diaryVer -ne $constVer) {
    Write-Host "WARNING: DIARY latest v$diaryVer != VERSION v$constVer — update DIARY.md" -ForegroundColor Yellow
}

Info "OK: version consistent v$constVer ($constDate)"
exit 0
