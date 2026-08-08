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
$agentsPath = Join-Path $repoRoot "AGENTS.md"

$cs = Get-Content $csPath -Raw
if ($cs -notmatch 'VERSION = "([^"]+)"') { throw "VERSION not found in $csPath" }
$oldFull = $matches[1]
$newVer = ([double]::Parse($oldFull) + 0.01).ToString("0.00")
Write-Host "Bumping $oldFull -> $newVer ($Date): $Description"

(Get-Content $csPath -Raw) -replace 'Version:\s*\d+\.\d+\s*\(\d{4}-\d{2}-\d{2}\)', "Version: $newVer ($Date)" -replace 'VERSION = ".*"', "VERSION = `"$newVer`"" -replace 'RELEASE_DATE = ".*"', "RELEASE_DATE = `"$Date`"" | Set-Content $csPath -NoNewline
if (Test-Path $uiPath) {
    (Get-Content $uiPath -Raw) -replace 'v\d+\.\d+\s*\(\d{4}-\d{2}-\d{2}\)', "v$newVer ($Date)" | Set-Content $uiPath -NoNewline
}
# also bump HudFactory and other src headers (drift-proof)
$hudFactoryPath = Join-Path $repoRoot "src\KatTradeManager.HudFactory.cs"
if (Test-Path $hudFactoryPath) {
    (Get-Content $hudFactoryPath -Raw) -replace 'v\d+\.\d+\s*\(\d{4}-\d{2}-\d{2}\)', "v$newVer ($Date)" | Set-Content $hudFactoryPath -NoNewline
}
$hudUpdatesPath = Join-Path $repoRoot "src\KatTradeManager.HudUpdates.cs"
if (Test-Path $hudUpdatesPath) {
    (Get-Content $hudUpdatesPath -Raw) -replace 'v\d+\.\d+\s*\(\d{4}-\d{2}-\d{2}\)', "v$newVer ($Date)" | Set-Content $hudUpdatesPath -NoNewline
}
$hudBuilderPath = Join-Path $repoRoot "src\KatTradeManager.HudBuilder.cs"
if (Test-Path $hudBuilderPath) {
    (Get-Content $hudBuilderPath -Raw) -replace 'v\d+\.\d+\s*\(\d{4}-\d{2}-\d{2}\)', "v$newVer ($Date)" | Set-Content $hudBuilderPath -NoNewline
}
$closeOpsPath = Join-Path $repoRoot "src\KatTradeManager.CloseOps.cs"
if (Test-Path $closeOpsPath) {
    (Get-Content $closeOpsPath -Raw) -replace 'v\d+\.\d+\s*\(\d{4}-\d{2}-\d{2}\)', "v$newVer ($Date)" | Set-Content $closeOpsPath -NoNewline
}
(Get-Content $readmePath -Raw) -replace 'v\d+\.\d+', "v$newVer" -replace '\d{4}-\d{2}-\d{2}', $Date | Set-Content $readmePath -NoNewline
if (Test-Path $agentsPath) {
    (Get-Content $agentsPath -Raw) -replace 'Current: v\d+\.\d+ \(\d{4}-\d{2}-\d{2}\)', "Current: v$newVer ($Date)" | Set-Content $agentsPath -NoNewline
}
Write-Host "Bumped to v$newVer."

# Auto DIARY entry (was manual → drift v1.57). Prepends version header if missing.
$diaryPath = Join-Path $repoRoot "DIARY.md"
if (Test-Path $diaryPath) {
    $diaryRaw = Get-Content $diaryPath -Raw
    if ($diaryRaw -notmatch "### \[v$newVer\]") {
        $mermaidHeader = "## 📊 Graphify System Architecture"
        $insertPos = $diaryRaw.IndexOf($mermaidHeader)
        if ($insertPos -ge 0) {
            # insert before Version History
            $vhMarker = "## 📜 Version History"
            $vhIdx = $diaryRaw.IndexOf($vhMarker)
            if ($vhIdx -ge 0) {
                $before = $diaryRaw.Substring(0, $vhIdx)
                $after = $diaryRaw.Substring($vhIdx)
                $newEntry = "### [v$newVer] — $Date`n- **Audit fixes — $Description**`n  - Auto-bumped via Bump-Version.ps1 ($oldFull -> $newVer)`n`n"
                $newDiary = $before + $newEntry + $after
                Set-Content $diaryPath $newDiary -NoNewline
                Write-Host "DIARY auto-inserted v$newVer entry."
            }
        } else {
            # fallback: prepend at top after first line
            $newEntry = "`n### [v$newVer] — $Date`n- **$Description**`n"
            Set-Content $diaryPath ($diaryRaw + $newEntry) -NoNewline
            Write-Host "DIARY appended v$newVer entry (fallback)."
        }
    } else {
        Write-Host "DIARY already contains v$newVer — skip auto-insert."
    }
}

# Verify immediately — catches regex edge cases before commit
$verifyScript = Join-Path $PSScriptRoot 'Verify-Version.ps1'
if (Test-Path $verifyScript) {
    Write-Host "Verifying bump..."
    & $verifyScript
    if ($LASTEXITCODE -ne 0) { throw "Bump verification FAILED — check regex replacements above." }
}
Write-Host "Bumped to v$newVer. Remember to run graphify update ."
Write-Host "Done. Next: pwsh scripts/Deploy-NT8.ps1"
