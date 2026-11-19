# @implements CodeHealth, TraceStoryFiles
param(
    [string]$Baseline = 'HEAD',
    [string]$Target,
    [string]$Title = 'Refactoring comparison',
    [string]$OutputPath
)

. (Join-Path $PSScriptRoot 'BuildEnvironment.ps1')
. (Join-Path $PSScriptRoot 'code-health/MetricsRunner.ps1')
if (-not $OutputPath) { $OutputPath = 'artifacts/refactoring/' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') }
$comparisonPath = [System.IO.Path]::GetFullPath($OutputPath, $projectRoot)
$runner = Get-CodeMetricsRunner $projectRoot
$previousRollForward = $env:DOTNET_ROLL_FORWARD
try {
    $env:DOTNET_ROLL_FORWARD = 'LatestMajor'
    $arguments = @((Join-Path $PSScriptRoot 'code-health/compare_refactoring.py'),
        '--root', $projectRoot, '--runner', $runner, '--baseline', $Baseline, '--title', $Title, '--output', $comparisonPath)
    if ($Target) { $arguments += @('--target', $Target) }
    python @arguments
    if ($LASTEXITCODE -ne 0) { throw 'The refactoring comparison could not be generated.' }
}
finally { $env:DOTNET_ROLL_FORWARD = $previousRollForward }
