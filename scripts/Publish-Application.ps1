# @implements TryLocalRelease, PackageInputs, ReleaseHandoff
param(
    [Parameter(Mandatory)][string]$ReleaseId,
    [Parameter(Mandatory)][psobject]$Verification,
    [string]$BuildOutputPath,
    [string]$StartupOutputPath,
    [switch]$SkipPackageAudit
)

. (Join-Path $PSScriptRoot 'BuildEnvironment.ps1')
if ($ReleaseId -cnotmatch '^\d{8}-\d{6}-\d{3}-[a-f0-9]{8}$') { throw 'Invalid release ID.' }
if ($Verification.total -le 0 -or $Verification.failures -ne 0 -or $Verification.nativeApplicationWindows -ne 0) {
    throw 'Verification did not produce a passing headless result.'
}
$releaseRoot = Join-Path $projectRoot 'releases'
$releasePath = Join-Path $releaseRoot $ReleaseId
$stagingPath = $releasePath + '.publishing'
if (Test-Path -LiteralPath $releasePath) { throw 'Use a new directory for each release.' }
[string[]]$auditArguments = if ($SkipPackageAudit) { '-p:NuGetAudit=false' } else { @() }
$stagingCreated = $null

Push-Location $projectRoot
try {
    $storage = New-Item -ItemType Directory -Path $releaseRoot -Force
    if ($storage.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'The release root must be a local directory.' }
    $stagingCreated = New-Item -ItemType Directory -Path $stagingPath
    if ($BuildOutputPath) {
        Get-ChildItem -LiteralPath $BuildOutputPath -Force | Copy-Item -Destination $stagingPath -Recurse
    }
    else {
        dotnet publish src/Xcmd.Desktop/Xcmd.Desktop.csproj -c Release -r win-x64 --no-self-contained -p:PublishReadyToRun=true -p:RestoreLockedMode=true --output $stagingPath -m:1 -p:UseSharedCompilation=false -v minimal @auditArguments
        if ($LASTEXITCODE -ne 0) { throw 'Publishing failed. The current release was not changed.' }
    }
    $executable = Join-Path $stagingPath 'Xcmd.Desktop.exe'
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'The published executable is missing.' }
    $Verification | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $stagingPath 'verification.json')
    $manifest = @{ id = $ReleaseId; restartProtocol = 1; issueProtocol = 1 }
    if ($Verification.verificationPhase) { $manifest.verificationPhase = $Verification.verificationPhase }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stagingPath 'release.json')
    if ($StartupOutputPath) {
        & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-Startup.ps1') -ApplicationPath $stagingPath -OutputPath $StartupOutputPath -Native
        if ($LASTEXITCODE -ne 0) { throw 'Published startup verification failed. The current release was not changed.' }
        Copy-Item -LiteralPath (Join-Path $StartupOutputPath 'results.json') -Destination (Join-Path $stagingPath 'startup-verification.json')
    }
    $marker = Join-Path $releaseRoot 'latest.txt'
    $lock = & (Join-Path $PSScriptRoot 'Enter-XcmdLock.ps1') -Path $marker
    try {
        $current = if (Test-Path -LiteralPath $marker) { (Get-Content -Raw -LiteralPath $marker).Trim() }
        if ($current -match '^\d{8}-\d{6}-\d{3}-[a-f0-9]{8}$' -and [StringComparer]::Ordinal.Compare($ReleaseId, $current) -le 0) {
            Write-Output 'A newer release is already available.'
            return
        }
        & (Join-Path $PSScriptRoot 'Install-Launcher.ps1') -IconPath (Join-Path $stagingPath 'app.ico')
        [IO.Directory]::Move($stagingPath, $releasePath)
        $temporary = $marker + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
        try {
            Set-Content -LiteralPath $temporary -Value $ReleaseId
            [System.IO.File]::Move($temporary, $marker, $true)
        }
        finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
        Write-Output ('Ready application: ' + (Join-Path $releasePath 'Xcmd.Desktop.exe'))
    }
    finally {
        $lock.ReleaseMutex()
        $lock.Dispose()
    }
    try { & (Join-Path $PSScriptRoot 'Remove-OldReleases.ps1') }
    catch { Write-Warning "Release cleanup skipped. $($_.Exception.Message)" }
}
finally {
    if ($null -ne $stagingCreated -and (Test-Path -LiteralPath $stagingPath)) {
        try { [IO.Directory]::Delete($stagingCreated.FullName, $true) }
        catch { Write-Warning "Could not remove the temporary release at '$stagingPath'. $($_.Exception.Message)" }
    }
    Pop-Location
}
