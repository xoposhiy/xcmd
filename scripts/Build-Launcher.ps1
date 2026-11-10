param([string]$OutputPath = 'artifacts/launcher/publish')

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet'
    $output = [System.IO.Path]::GetFullPath($OutputPath, $projectRoot)
    $intermediate = Join-Path $projectRoot 'artifacts/launcher/obj/'
    $binary = Join-Path $projectRoot 'artifacts/launcher/bin/'
    $lockFile = Join-Path $projectRoot 'src/Xcmd.Infrastructure/packages.launcher.lock.json'
    dotnet publish src/Xcmd.Infrastructure/Xcmd.Infrastructure.csproj -c Release -r win-x64 --self-contained false --output $output `
        -p:XcmdLauncher=true -p:RestoreLockedMode=true "-p:NuGetLockFilePath=$lockFile" `
        "-p:BaseIntermediateOutputPath=$intermediate" "-p:BaseOutputPath=$binary" `
        -p:UseSharedCompilation=false -m:1 -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'The launcher build failed.' }
    if (-not (Test-Path -LiteralPath (Join-Path $output 'Xcmd.exe') -PathType Leaf)) { throw 'The launcher executable is missing.' }
}
finally { Pop-Location }
