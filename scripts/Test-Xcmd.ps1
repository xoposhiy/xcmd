# @implements HeadlessVerification, IntegrationVerification
param(
    [string]$Filter,
    [string]$OutputPath,
    [switch]$KeepFixtures
)

. (Join-Path $PSScriptRoot 'BuildEnvironment.ps1')
Push-Location $projectRoot
try {
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-Architecture.ps1')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $probeArguments = @('tests/Xcmd.Probe/bin/Release/net10.0/win-x64/Xcmd.Probe.dll')
    if ($Filter) { $probeArguments += @('--filter', $Filter) }
    if ($OutputPath) { $probeArguments += @('--output', $OutputPath) }
    if ($KeepFixtures) { $probeArguments += '--keep-fixtures' }
    dotnet @probeArguments
    exit $LASTEXITCODE
}
finally { Pop-Location }
