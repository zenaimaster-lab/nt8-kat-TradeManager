# Bump-Version.ps1 — +0.01 version bump, date stamp, sync 4 locations + verify.
# SINGLE SOURCE OF TRUTH for version bumps — never edit Version/VERSION manually.
# Usage: pwsh scripts/Bump-Version.ps1 -Description "fix"
param(
    [string]$Description = "audit fixes",
    [string]$Date = (Get-Date -Format "yyyy-MM-dd")
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$csPath = Join-Path $repoRoot "KatTradeManager.cs"
$uiPath = Join-Path $repoRoot "src/KatTradeManagerUI.cs"
$readmePath = Join-Path $repoRoot "README.md"

$cs = Get-Content $csPath -Raw
if ($cs -notmatch 'VERSION = "([^"]+)"') { throw "VERSION not found in $csPath" }
$oldFull = $matches[1]
$newVer = ([double]::Parse($oldFull) + 0.01).ToString("0.00")
Write-Host "Bumping $oldFull -> $newVer ($Date): $Description"

(Get-Content $csPath -Raw) -replace 'Version:\s*\d+\.\d+\s*\(\d{4}-\d{2}-\d{2}\)', "Version: $newVer ($Date)" -replace 'VERSION = ".*"', "VERSION = `"$newVer`"" -replace 'RELEASE_DATE = ".*"', "RELEASE_DATE = `"$Date`"" | Set-Content $csPath -NoNewline
if (Test-Path $uiPath) {
    (Get-Content $uiPath -Raw) -replace 'v\d+\.\d+\s*\(\d{4}-\d{2}-\d{2}\)', "v$newVer ($Date)" | Set-Content $uiPath -NoNewline
}
(Get-Content $readmePath -Raw) -replace 'v\d+\.\d+', "v$newVer" -replace '\d{4}-\d{2}-\d{2}', $Date | Set-Content $readmePath -NoNewline
Write-Host "Bumped to v$newVer."

# Verify immediately — catches regex edge cases before commit
$verifyScript = Join-Path $PSScriptRoot 'Verify-Version.ps1'
if (Test-Path $verifyScript) {
    Write-Host "Verifying bump..."
    & $verifyScript
    if ($LASTEXITCODE -ne 0) { throw "Bump verification FAILED — check regex replacements above." }
}
Write-Host "Bumped to v$newVer. Remember to add DIARY entry and run graphify update ."
Write-Host "Done. Next: update DIARY.md, then pwsh scripts/Deploy-NT8.ps1"
