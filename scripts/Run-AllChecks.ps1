# Run-AllChecks.ps1 — one-shot verification: version guard, xunit suite, then net48 compile gate.
# Exit 0 only when all pass. Usage:  pwsh scripts/Run-AllChecks.ps1

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host '=== 0/3: version consistency ==='
pwsh (Join-Path $repoRoot 'scripts\Verify-Version.ps1')
$verOk = ($LASTEXITCODE -eq 0)

Write-Host '=== 1/3: xunit suite ==='
dotnet test (Join-Path $repoRoot 'tests\KatTradeManager.Tests') --nologo --verbosity quiet
$testsOk = ($LASTEXITCODE -eq 0)

Write-Host '=== 2/3: CompileCheck (net48 gate) ==='
dotnet build (Join-Path $repoRoot 'tools\CompileCheck') --nologo --verbosity quiet
$gateOk = ($LASTEXITCODE -eq 0)

if ($verOk -and $testsOk -and $gateOk) {
    Write-Host 'ALL CHECKS GREEN.'
    # optional graph refresh (zero token AST) when graphify is installed
    if (Get-Command graphify -ErrorAction SilentlyContinue) {
        Write-Host '=== 3/3: graphify update ==='
        graphify update . 2>&1 | Out-String | Write-Host
    }
    exit 0
}

if (-not $verOk)  { Write-Host 'FAILED: version consistency (run pwsh scripts/Verify-Version.ps1)' }
if (-not $testsOk) { Write-Host 'FAILED: xunit suite' }
if (-not $gateOk)  { Write-Host 'FAILED: compile gate' }
exit 1
