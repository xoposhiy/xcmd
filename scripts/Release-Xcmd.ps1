$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$releaseId = (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$releasePath = Join-Path $projectRoot ('releases/' + $releaseId)
$testOutput = Join-Path $projectRoot ('artifacts/probe/release-' + $releaseId)

Push-Location $projectRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'

    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-Xcmd.ps1') -OutputPath $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Verification failed. No release was published.' }
    $resultsPath = Join-Path $testOutput 'results.json'
    $results = Get-Content -Raw -LiteralPath $resultsPath | ConvertFrom-Json
    if ($results.total -le 0 -or $results.failures -ne 0 -or $results.nativeApplicationWindows -ne 0) {
        throw 'Verification did not produce a passing headless result.'
    }

    New-Item -ItemType Directory -Path $releasePath | Out-Null
    dotnet publish src/Xcmd.Desktop/Xcmd.Desktop.csproj -c Release -r win-x64 --no-self-contained -p:PublishReadyToRun=true -p:RestoreLockedMode=true --output $releasePath -m:1 -p:UseSharedCompilation=false -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Publishing failed. The current release was not changed.' }
    $executable = Join-Path $releasePath 'Xcmd.Desktop.exe'
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'The published executable is missing.' }
    Copy-Item -LiteralPath $resultsPath -Destination (Join-Path $releasePath 'verification.json')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/feedback-resolutions.json') -Destination (Join-Path $releasePath 'feedback-resolutions.json')
    @{ id = $releaseId; restartProtocol = 1 } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releasePath 'release.json')
    $startupOutput = Join-Path $projectRoot ('artifacts/startup/release-' + $releaseId)
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-Startup.ps1') -ApplicationPath $releasePath -OutputPath $startupOutput -Native
    if ($LASTEXITCODE -ne 0) { throw 'Published startup verification failed. The current release was not changed.' }
    Copy-Item -LiteralPath (Join-Path $startupOutput 'results.json') -Destination (Join-Path $releasePath 'startup-verification.json')
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Install-Launcher.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Launcher setup failed. The current release was not changed.' }
    $markerPath = Join-Path $projectRoot 'releases/latest.txt'
    $temporaryMarker = $markerPath + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    Set-Content -LiteralPath $temporaryMarker -Value $releaseId
    [System.IO.File]::Move($temporaryMarker, $markerPath, $true)
    Write-Output ('Ready application: ' + $executable)
}
finally { Pop-Location }
