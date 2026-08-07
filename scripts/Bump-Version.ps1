# Bump-Version.ps1 — +0.01 version bump, date stamp, sync 4 locations.
# Usage: pwsh scripts/Bump-Version.ps1 -Description "Revert fix + XXE harden"
param(
    [string]$Description = "audit fixes",
    [string]$Date = (Get-Date -Format "yyyy-MM-dd")
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

$csPath = Join-Path $repoRoot "KatTradeManager.cs"
$uiPath = Join-Path $repoRoot "src/KatTradeManagerUI.cs"
$readmePath = Join-Path $repoRoot "README.md"
$diaryPath = Join-Path $repoRoot "DIARY.md"

$cs = Get-Content $csPath -Raw
if ($cs -match 'VERSION = "(\d+)\.(\d+)"') {
    $major = $matches[1]
    $minor = [int]$matches[2] + 1
    $newVer = "$major.$minor"
    if ($minor -lt 10) { $newVer = "$major.0$minor" } # keep 2-digit
    # normalize: v1.38 -> v1.39 (simple +0.01)
    $oldVer = "$($matches[1]).$($matches[2])"
    # Extract actual version string handling 1.38 format
    $oldFull = $matches[0] -replace '.*"(.*)".*', '$1'
    $parts = $oldFull.Split('.')
    $newMinor = ([int]$parts[1] + 1).ToString()
    if ($newMinor.Length -eq 1) { $newMinor = "0$newMinor" } # keep leading zero? actually 38->39 no zero
    # Simpler: parse as double +0.01
    $verDouble = [double]::Parse($oldFull) + 0.01
    $newVer = $verDouble.ToString("0.00")
} else { throw "VERSION not found in $csPath" }

Write-Host "Bumping $oldFull -> $newVer ($Date): $Description"

# KatTradeManager.cs — header + constants
(Get-Content $csPath -Raw) -replace 'Version: .*', "Version: $newVer ($Date)" -replace 'VERSION = ".*"', "VERSION = `"$newVer`"" -replace 'RELEASE_DATE = ".*"', "RELEASE_DATE = `"$Date`"" | Set-Content $csPath -NoNewline

# KatTradeManagerUI.cs — header
if (Test-Path $uiPath) {
    (Get-Content $uiPath -Raw) -replace 'v\d+\.\d+.*', "v$newVer ($Date) */" -replace 'v\d+\.\d+', "v$newVer" | Set-Content $uiPath -NoNewline
}

# README.md badge
(Get-Content $readmePath -Raw) -replace 'v\d+\.\d+', "v$newVer" -replace '\d{4}-\d{2}-\d{2}', $Date | Set-Content $readmePath -NoNewline

# DIARY.md — prepend entry (caller must fill details)
Write-Host "Bumped to v$newVer. Remember to add DIARY entry and run graphify update ."

Write-Host "Done. Next: update DIARY.md, run Run-AllChecks.ps1, then git commit."
