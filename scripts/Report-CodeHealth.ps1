# @implements CodeHealth
param(
    [string]$OutputPath,
    [switch]$SkipVerification
)

. (Join-Path $PSScriptRoot 'BuildEnvironment.ps1')
. (Join-Path $PSScriptRoot 'code-health/MetricsRunner.ps1')
if (-not $OutputPath) { $OutputPath = 'artifacts/code-health/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') }
$reportPath = [System.IO.Path]::GetFullPath($OutputPath, $projectRoot)
$python = Join-Path $projectRoot 'artifacts/lint-venv/Scripts/python.exe'
$collector = Join-Path $PSScriptRoot 'code-health/collect.py'
$previousRollForward = $env:DOTNET_ROLL_FORWARD
Push-Location $projectRoot
try {
    New-Item -ItemType Directory -Path $reportPath -Force | Out-Null
    if (-not (Test-Path -LiteralPath $python)) {
        python -m venv artifacts/lint-venv
        if ($LASTEXITCODE -ne 0) { throw 'The reporting environment could not be created.' }
        & $python -m pip install --disable-pip-version-check -r scripts/requirements-lint.txt
        if ($LASTEXITCODE -ne 0) { throw 'The pinned reporting dependencies could not be installed.' }
    }
    & $python -m unittest discover -s scripts/code-health -p 'test_*.py' *> (Join-Path $reportPath 'report-tests.log')
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath (Join-Path $reportPath 'report-tests.log') | Write-Output
        throw 'The report aggregation checks failed.'
    }
    $before = & $python $collector fingerprint --root $projectRoot
    if ($LASTEXITCODE -ne 0) { throw 'The verification fingerprint could not be computed.' }
    $verification = [ordered]@{
        status = 'skipped'
        startedAt = [DateTime]::UtcNow.ToString('o')
        sourceFingerprintBefore = $before
    }
    $verificationExit = 0
    if (-not $SkipVerification) {
        $logPath = Join-Path $reportPath 'verification.log'
        & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-Xcmd.ps1') -OutputPath (Join-Path $reportPath 'behavior') 2>&1 | Tee-Object -FilePath $logPath
        $verificationExit = $LASTEXITCODE
        $verification.status = if ($verificationExit -eq 0) { 'passed' } else { 'failed' }
        $verification.exitCode = $verificationExit
        $log = Get-Content -Raw -LiteralPath $logPath
        if ($log -match 'Architecture: (\d+) rules') { $verification.architectureRules = [int]$Matches[1] }
        if ($log -match 'PASS analyzer checks \((\d+) deliberate') { $verification.analyzerSelfChecks = [int]$Matches[1] }
        if (Test-Path -LiteralPath (Join-Path $reportPath 'behavior/results.json')) { $verification.behaviorResults = 'behavior/results.json' }
    }
    Write-Output 'CHECK Generate the code health report'
    $verification.finishedAt = [DateTime]::UtcNow.ToString('o')
    $verification.sourceFingerprintAfter = & $python $collector fingerprint --root $projectRoot
    if ($LASTEXITCODE -ne 0) { throw 'The final verification fingerprint could not be computed.' }
    $verification | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $reportPath 'verification.json') -Encoding utf8

    $runner = Get-CodeMetricsRunner $projectRoot
    $env:DOTNET_ROLL_FORWARD = 'LatestMajor'
    $projects = & $python $collector projects --root $projectRoot
    if ($LASTEXITCODE -ne 0) { throw 'The reporting project order could not be computed.' }
    $metricArguments = @($projects | ForEach-Object { '/project:' + $_ })
    $metricArguments += '/out:' + (Join-Path $reportPath 'roslyn.xml')
    & $runner @metricArguments *> (Join-Path $reportPath 'roslyn.log')
    if ($LASTEXITCODE -ne 0) {
        Get-Content -LiteralPath (Join-Path $reportPath 'roslyn.log') | Write-Output
        throw 'Microsoft code metrics could not be collected.'
    }
    & $python $collector generate --root $projectRoot --output $reportPath
    if ($LASTEXITCODE -ne 0) { throw 'The code health report could not be generated.' }
    Set-Content -LiteralPath (Join-Path $projectRoot 'artifacts/code-health/latest.txt') -Value $reportPath -Encoding utf8
    if ($verificationExit -ne 0) { exit $verificationExit }
}
finally {
    $env:DOTNET_ROLL_FORWARD = $previousRollForward
    Pop-Location
}
