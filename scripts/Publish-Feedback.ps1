# @implements TryLocalRelease, PackageInputs, HeadlessVerification
param(
    [Parameter(Mandatory)][string]$Filter,
    [string]$OutputPath,
    [switch]$KeepFixtures,
    [switch]$SkipPackageAudit
)

. (Join-Path $PSScriptRoot 'BuildEnvironment.ps1')
$releaseId = (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
if (-not $OutputPath) { $OutputPath = Join-Path $projectRoot ('artifacts/probe/feedback-' + $releaseId) }
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath, (Get-Location).Path)
$buildPath = Join-Path $projectRoot 'artifacts/feedback-build'
$probe = Join-Path $buildPath 'bin/Xcmd.Probe/release_win-x64/Xcmd.Probe.exe'
$application = Join-Path $buildPath 'bin/Xcmd.Desktop/release_win-x64'

$lock = & (Join-Path $PSScriptRoot 'Enter-XcmdLock.ps1') -Path $buildPath
Push-Location $projectRoot
try {
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $auditArguments = @()
    if ($SkipPackageAudit) {
        Write-Output 'Package vulnerability audit explicitly skipped for this feedback build. Locked dependency restore and smoke checks still run.'
        $auditArguments = @('-p:NuGetAudit=false')
    }
    if (-not (Test-Path -LiteralPath 'artifacts/launcher/publish/Xcmd.exe' -PathType Leaf)) {
        & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Build-Launcher.ps1')
        if ($LASTEXITCODE -ne 0) { throw 'The launcher build failed.' }
    }
    dotnet build tests/Xcmd.Probe/Xcmd.Probe.csproj -c Release -r win-x64 --artifacts-path $buildPath -p:RestoreLockedMode=true -m:1 -p:UseSharedCompilation=false -v minimal @auditArguments
    if ($LASTEXITCODE -ne 0) { throw 'The feedback build failed. The current release was not changed.' }
    $probeArguments = @('--filter', $Filter, '--output', $OutputPath)
    if ($KeepFixtures) { $probeArguments += '--keep-fixtures' }
    & $probe @probeArguments
    if ($LASTEXITCODE -ne 0) { throw 'The feedback smoke check failed. The current release was not changed.' }
    $resultsPath = Join-Path $OutputPath 'results.json'
    $results = Get-Content -Raw -LiteralPath $resultsPath | ConvertFrom-Json
    $timer.Stop()
    $results | Add-Member -NotePropertyName verificationPhase -NotePropertyValue 'feedback-smoke'
    $results | Add-Member -NotePropertyName fullVerification -NotePropertyValue $false
    $results | Add-Member -NotePropertyName packageAudit -NotePropertyValue $(if ($SkipPackageAudit) { 'skipped-explicitly' } else { 'passed' })
    $results | Add-Member -NotePropertyName verificationElapsedSeconds -NotePropertyValue $timer.Elapsed.TotalSeconds
    if ($timer.Elapsed.TotalSeconds -gt 60) { Write-Output ('Feedback verification exceeded 60 seconds: ' + $timer.Elapsed.TotalSeconds) }

    & (Join-Path $PSScriptRoot 'Publish-Application.ps1') -ReleaseId $releaseId -Verification $results -BuildOutputPath $application
    Write-Output ('Smoke results: ' + $resultsPath)
}
finally {
    Pop-Location
    $lock.ReleaseMutex()
    $lock.Dispose()
}
