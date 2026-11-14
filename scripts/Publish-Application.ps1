param(
    [Parameter(Mandatory)][string]$ReleaseId,
    [Parameter(Mandatory)][psobject]$Verification,
    [string]$StartupOutputPath,
    [switch]$SkipPackageAudit
)

. (Join-Path $PSScriptRoot 'BuildEnvironment.ps1')
if ($ReleaseId -notmatch '^[a-zA-Z0-9-]+$') { throw 'Invalid release ID.' }
if ($Verification.total -le 0 -or $Verification.failures -ne 0 -or $Verification.nativeApplicationWindows -ne 0) {
    throw 'Verification did not produce a passing headless result.'
}
$releasePath = Join-Path $projectRoot ('releases/' + $ReleaseId)
if (Test-Path -LiteralPath $releasePath) { throw 'Use a new directory for each release.' }
[string[]]$auditArguments = if ($SkipPackageAudit) { '-p:NuGetAudit=false' } else { @() }

Push-Location $projectRoot
try {
    dotnet publish src/Xcmd.Desktop/Xcmd.Desktop.csproj -c Release -r win-x64 --no-self-contained -p:PublishReadyToRun=true -p:RestoreLockedMode=true --output $releasePath -m:1 -p:UseSharedCompilation=false -v minimal @auditArguments
    if ($LASTEXITCODE -ne 0) { throw 'Publishing failed. The current release was not changed.' }
    $executable = Join-Path $releasePath 'Xcmd.Desktop.exe'
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'The published executable is missing.' }
    $Verification | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $releasePath 'verification.json')
    $manifest = @{ id = $ReleaseId; restartProtocol = 1; issueProtocol = 1 }
    if ($Verification.verificationPhase) { $manifest.verificationPhase = $Verification.verificationPhase }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releasePath 'release.json')
    if ($StartupOutputPath) {
        & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-Startup.ps1') -ApplicationPath $releasePath -OutputPath $StartupOutputPath -Native
        if ($LASTEXITCODE -ne 0) { throw 'Published startup verification failed. The current release was not changed.' }
        Copy-Item -LiteralPath (Join-Path $StartupOutputPath 'results.json') -Destination (Join-Path $releasePath 'startup-verification.json')
    }
    & (Join-Path $PSScriptRoot 'Install-Launcher.ps1') -IconPath (Join-Path $releasePath 'app.ico')
    $marker = Join-Path $projectRoot 'releases/latest.txt'
    $temporary = $marker + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    Set-Content -LiteralPath $temporary -Value $ReleaseId
    [System.IO.File]::Move($temporary, $marker, $true)
    Write-Output ('Ready application: ' + $executable)
}
finally { Pop-Location }
