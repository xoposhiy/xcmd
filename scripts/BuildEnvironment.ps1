# @implements PackageInputs
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$sdkVersion = (Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'global.json') | ConvertFrom-Json).sdk.version
if ($env:LOCALAPPDATA) {
    $userDotnet = Join-Path $env:LOCALAPPDATA 'Microsoft/dotnet'
    if ($env:PATH.Split([IO.Path]::PathSeparator)[0] -ne $userDotnet -and
        (Test-Path -LiteralPath (Join-Path $userDotnet 'dotnet.exe') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $userDotnet "sdk/$sdkVersion") -PathType Container)) {
        $env:PATH = $userDotnet + [IO.Path]::PathSeparator + $env:PATH
    }
}
# The NativeAOT linker lookup runs VsDevCmd.bat, which calls vswhere.exe by bare name
# from its own folder. That lookup fails when NoDefaultCurrentDirectoryInExePath is set,
# as agent sandboxes do, so the folder goes on PATH explicitly.
$programFilesX86 = ${env:ProgramFiles(x86)}
if ($programFilesX86) {
    $vsInstaller = Join-Path $programFilesX86 'Microsoft Visual Studio/Installer'
    if ((Test-Path -LiteralPath (Join-Path $vsInstaller 'vswhere.exe') -PathType Leaf) -and
        -not ($env:PATH.Split([IO.Path]::PathSeparator) -contains $vsInstaller)) {
        $env:PATH = $env:PATH + [IO.Path]::PathSeparator + $vsInstaller
    }
}
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
