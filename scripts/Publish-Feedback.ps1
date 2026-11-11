param(
    [Parameter(Mandatory)][string]$Filter
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$releaseId = (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$releasePath = Join-Path $projectRoot ('releases/' + $releaseId)
$outputPath = Join-Path $projectRoot ('artifacts/probe/feedback-' + $releaseId)

Push-Location $projectRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    dotnet build tests/Xcmd.Probe/Xcmd.Probe.csproj -c Release -r win-x64 -p:RestoreLockedMode=true -m:1 -p:UseSharedCompilation=false -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'The feedback build failed. The current release was not changed.' }
    & tests/Xcmd.Probe/bin/Release/net10.0/win-x64/Xcmd.Probe.exe --filter $Filter --output $outputPath
    if ($LASTEXITCODE -ne 0) { throw 'The feedback smoke check failed. The current release was not changed.' }
    $resultsPath = Join-Path $outputPath 'results.json'
    $results = Get-Content -Raw -LiteralPath $resultsPath | ConvertFrom-Json
    if ($results.total -le 0 -or $results.failures -ne 0 -or $results.nativeApplicationWindows -ne 0) { throw 'The smoke check did not produce a passing headless result.' }
    $timer.Stop()
    $results | Add-Member -NotePropertyName verificationPhase -NotePropertyValue 'feedback-smoke'
    $results | Add-Member -NotePropertyName fullVerification -NotePropertyValue $false
    $results | Add-Member -NotePropertyName verificationElapsedSeconds -NotePropertyValue $timer.Elapsed.TotalSeconds
    if ($timer.Elapsed.TotalSeconds -gt 60) { Write-Output ('Feedback verification exceeded 60 seconds: ' + $timer.Elapsed.TotalSeconds) }

    dotnet publish src/Xcmd.Desktop/Xcmd.Desktop.csproj -c Release -r win-x64 --no-self-contained -p:PublishReadyToRun=true -p:RestoreLockedMode=true --output $releasePath -m:1 -p:UseSharedCompilation=false -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Publishing failed. The current release was not changed.' }
    $executable = Join-Path $releasePath 'Xcmd.Desktop.exe'
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'The published executable is missing.' }
    $results | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath (Join-Path $releasePath 'verification.json')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/feedback-resolutions.json') -Destination (Join-Path $releasePath 'feedback-resolutions.json')
    @{ id = $releaseId; restartProtocol = 1; verificationPhase = 'feedback-smoke' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releasePath 'release.json')
    & (Join-Path $PSScriptRoot 'Install-Launcher.ps1') -IconPath (Join-Path $releasePath 'app.ico')
    if ($LASTEXITCODE -ne 0) { throw 'Launcher setup failed. The current release was not changed.' }
    $marker = Join-Path $projectRoot 'releases/latest.txt'
    $temporary = $marker + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    Set-Content -LiteralPath $temporary -Value $releaseId
    [System.IO.File]::Move($temporary, $marker, $true)
    Write-Output ('Smoke-checked feedback application: ' + $executable)
    Write-Output ('Smoke results: ' + $resultsPath)
}
finally { Pop-Location }
