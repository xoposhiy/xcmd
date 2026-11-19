# @implements HeadlessVerification, IntegrationVerification, TryLocalRelease
param(
    [string]$Filter,
    [string]$OutputPath,
    [switch]$KeepFixtures,
    [switch]$CheckOnly
)

. (Join-Path $PSScriptRoot 'BuildEnvironment.ps1')
if ($Filter -and -not $CheckOnly -and -not $env:CI) {
    & (Join-Path $PSScriptRoot 'Publish-Feedback.ps1') -Filter $Filter -OutputPath $OutputPath -KeepFixtures:$KeepFixtures
    exit 0
}

Push-Location $projectRoot
try {
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-Architecture.ps1')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $probeArguments = @('tests/Xcmd.Probe/bin/Release/net10.0/win-x64/Xcmd.Probe.dll')
    if ($Filter) { $probeArguments += @('--filter', $Filter) }
    if ($Filter -and $CheckOnly -and -not $env:CI) {
        $recent = @(python (Join-Path $PSScriptRoot 'code-health/recent_failures.py') --ids)
        if ($LASTEXITCODE -ne 0) { Write-Warning 'Checking affected scenarios with no CI history available.' }
        if ($recent.Count -gt 0) {
            Write-Output ("Recent CI scenarios: " + ($recent -join ', '))
            $probeArguments += @('--include-ids', ($recent -join ','))
        }
    }
    if ($OutputPath) { $probeArguments += @('--output', $OutputPath) }
    if ($KeepFixtures) { $probeArguments += '--keep-fixtures' }
    Write-Output 'CHECK Behavioral scenarios'
    dotnet @probeArguments
    exit $LASTEXITCODE
}
finally { Pop-Location }
