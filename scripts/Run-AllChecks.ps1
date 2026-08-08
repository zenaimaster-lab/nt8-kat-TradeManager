# Run-AllChecks.ps1 — one-shot verification: version guard, xunit suite, then net48 compile gate.
# Exit 0 only when all pass. Usage:  pwsh scripts/Run-AllChecks.ps1

$ErrorActionPreference = 'Continue'
$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host '=== 0/4: version consistency ==='
pwsh (Join-Path $repoRoot 'scripts\Verify-Version.ps1')
$verOk = ($LASTEXITCODE -eq 0)

Write-Host '=== 1/4: format check ==='
dotnet format tests/KatTradeManager.Tests/KatTradeManager.Tests.csproj --verify-no-changes --verbosity diagnostic 2>&1 | Out-String | Write-Host
$fmtOk = ($LASTEXITCODE -eq 0)
if (-not $fmtOk) { Write-Host 'format check failed — run dotnet format tests/KatTradeManager.Tests' -ForegroundColor Yellow }

Write-Host '=== 2/4: xunit suite ==='
dotnet test (Join-Path $repoRoot 'tests\KatTradeManager.Tests') --nologo --verbosity quiet --collect:"XPlat Code Coverage"
$testsOk = ($LASTEXITCODE -eq 0)

Write-Host '=== 3/4: CompileCheck (net48 gate) ==='
dotnet build (Join-Path $repoRoot 'tools\CompileCheck') --nologo --verbosity quiet
$gateOk = ($LASTEXITCODE -eq 0)

if ($verOk -and $testsOk -and $gateOk) {
    Write-Host 'ALL CHECKS GREEN.'
    if (-not $fmtOk) { Write-Host 'NOTE: format drift - run dotnet format (non-blocking)' -ForegroundColor Yellow }
    # optional graph refresh (zero token AST) when graphify is installed
    if (Get-Command graphify -ErrorAction SilentlyContinue) {
        Write-Host '=== 4/4: graphify update ==='
        graphify update . 2>&1 | Out-String | Write-Host
    }
    exit 0
}

if (-not $verOk)  { Write-Host 'FAILED: version consistency (run pwsh scripts/Verify-Version.ps1)' }
if (-not $testsOk) { Write-Host 'FAILED: xunit suite' }
if (-not $gateOk)  { Write-Host 'FAILED: compile gate' }
exit 1
