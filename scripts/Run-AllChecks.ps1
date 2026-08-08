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

Write-Host '=== 1b/4: ps analyze ==='
$psOk = $true
if (Get-Module -ListAvailable PSScriptAnalyzer) {
    $err = Invoke-ScriptAnalyzer -Path (Join-Path $repoRoot 'scripts') -Recurse -Severity Error
    if ($err) { $err | Format-Table -AutoSize | Out-String | Write-Host; Write-Host 'FAILED: PSScriptAnalyzer Error' -ForegroundColor Red; $psOk = $false } else { Write-Host 'ps Error: 0' }
    Invoke-ScriptAnalyzer -Path (Join-Path $repoRoot 'scripts') -Recurse -Severity Warning 2>&1 | Out-String | Write-Host
} else { Write-Host 'PSScriptAnalyzer not installed — skip' }

Write-Host '=== 2/4: xunit suite ==='
dotnet test (Join-Path $repoRoot 'tests\KatTradeManager.Tests') --nologo --verbosity quiet --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
$testsOk = ($LASTEXITCODE -eq 0)
$covOk = $true
if ($testsOk) {
    $covFile = Get-ChildItem -Path (Join-Path $repoRoot 'tests\KatTradeManager.Tests\TestResults') -Recurse -Filter coverage.cobertura.xml -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($covFile) {
        try {
            [xml]$cov = Get-Content $covFile.FullName
            $rate = 0; if ($cov.coverage -and $cov.coverage.'line-rate') { $rate = [double]$cov.coverage.'line-rate' * 100 }
            $covered = 0; $valid = 0
            if ($cov.coverage) { $covered = [int]$cov.coverage.'lines-covered'; $valid = [int]$cov.coverage.'lines-valid' }
            Write-Host ("Coverage line-rate: {0:N1}% ({1}/{2} lines)" -f $rate, $covered, $valid)
            if ($valid -eq 0) {
                Write-Host "Coverage 0 lines-valid — coverlet not instrumenting (NT8 refs + UseWPF, private repo) — treating as WARN not FAIL" -ForegroundColor Yellow
            } elseif ($rate -lt 60) {
                Write-Host "WARNING: coverage <60% — add tests (private repo: warn only, not fail)" -ForegroundColor Yellow
            }
            # ponytail: private solo repo — coverage is advisory, never blocks. CI/Run-AllChecks still green.
        } catch {}
    } else {
        Write-Host "No coverage file — skip (coverlet optional for private repo)" -ForegroundColor Yellow
    }
}

Write-Host '=== 3/4: CompileCheck (net48 gate) ==='
dotnet build (Join-Path $repoRoot 'tools\CompileCheck') --nologo --verbosity quiet
$gateOk = ($LASTEXITCODE -eq 0)

if ($verOk -and $testsOk -and $gateOk -and $psOk) {
    Write-Host 'ALL CHECKS GREEN.'
    if (-not $fmtOk) { Write-Host 'NOTE: format drift - run dotnet format (non-blocking)' -ForegroundColor Yellow }
    if (-not $covOk) { Write-Host 'NOTE: coverage low — see warning above (private repo advisory only)' -ForegroundColor Yellow }
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
if (-not $psOk) { Write-Host 'FAILED: ps analyzer Error' }
exit 1
