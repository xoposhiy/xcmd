# @implements TryLocalRelease, PackageInputs, ReleaseHandoff
param(
    [Parameter(Mandatory)][string]$ReleaseId,
    [Parameter(Mandatory)][psobject]$Verification,
    [string]$BuildOutputPath,
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
    if ($BuildOutputPath) {
        New-Item -ItemType Directory -Path (Split-Path $releasePath -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $BuildOutputPath -Destination $releasePath -Recurse
    }
    else {
        dotnet publish src/Xcmd.Desktop/Xcmd.Desktop.csproj -c Release -r win-x64 --no-self-contained -p:PublishReadyToRun=true -p:RestoreLockedMode=true --output $releasePath -m:1 -p:UseSharedCompilation=false -v minimal @auditArguments
        if ($LASTEXITCODE -ne 0) { throw 'Publishing failed. The current release was not changed.' }
    }
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
    $marker = Join-Path $projectRoot 'releases/latest.txt'
    $lock = & (Join-Path $PSScriptRoot 'Enter-XcmdLock.ps1') -Path $marker
    try {
        $current = if (Test-Path -LiteralPath $marker) { (Get-Content -Raw -LiteralPath $marker).Trim() }
        if ($current -match '^\d{8}-\d{6}-\d{3}-[a-f0-9]{8}$' -and [StringComparer]::Ordinal.Compare($ReleaseId, $current) -le 0) {
            Write-Output 'A newer release is already available.'
            return
        }
        & (Join-Path $PSScriptRoot 'Install-Launcher.ps1') -IconPath (Join-Path $releasePath 'app.ico')
        $temporary = $marker + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
        try {
            Set-Content -LiteralPath $temporary -Value $ReleaseId
            [System.IO.File]::Move($temporary, $marker, $true)
        }
        finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
        Write-Output ('Ready application: ' + $executable)
    }
    finally {
        $lock.ReleaseMutex()
        $lock.Dispose()
    }
}
finally { Pop-Location }
