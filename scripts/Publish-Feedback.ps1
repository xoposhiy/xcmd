param(
    [Parameter(Mandatory)][string]$Filter,
    [switch]$SkipPackageAudit
)

. (Join-Path $PSScriptRoot 'BuildEnvironment.ps1')
$releaseId = (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$outputPath = Join-Path $projectRoot ('artifacts/probe/feedback-' + $releaseId)

Push-Location $projectRoot
try {
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $auditArguments = @()
    if ($SkipPackageAudit) {
        Write-Output 'Package vulnerability audit explicitly skipped for this feedback build. Locked dependency restore and smoke checks still run.'
        $auditArguments = @('-p:NuGetAudit=false')
    }
    dotnet build tests/Xcmd.Probe/Xcmd.Probe.csproj -c Release -r win-x64 -p:RestoreLockedMode=true -m:1 -p:UseSharedCompilation=false -v minimal @auditArguments
    if ($LASTEXITCODE -ne 0) { throw 'The feedback build failed. The current release was not changed.' }
    & tests/Xcmd.Probe/bin/Release/net10.0/win-x64/Xcmd.Probe.exe --filter $Filter --output $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'The feedback smoke check failed. The current release was not changed.' }
    $resultsPath = Join-Path $outputPath 'results.json'
    $results = Get-Content -Raw -LiteralPath $resultsPath | ConvertFrom-Json
    $timer.Stop()
    $results | Add-Member -NotePropertyName verificationPhase -NotePropertyValue 'feedback-smoke'
    $results | Add-Member -NotePropertyName fullVerification -NotePropertyValue $false
    $results | Add-Member -NotePropertyName packageAudit -NotePropertyValue $(if ($SkipPackageAudit) { 'skipped-explicitly' } else { 'passed' })
    $results | Add-Member -NotePropertyName verificationElapsedSeconds -NotePropertyValue $timer.Elapsed.TotalSeconds
    if ($timer.Elapsed.TotalSeconds -gt 60) { Write-Output ('Feedback verification exceeded 60 seconds: ' + $timer.Elapsed.TotalSeconds) }

    & (Join-Path $PSScriptRoot 'Publish-Application.ps1') -ReleaseId $releaseId -Verification $results -SkipPackageAudit:$SkipPackageAudit
    Write-Output ('Smoke results: ' + $resultsPath)
}
finally { Pop-Location }
