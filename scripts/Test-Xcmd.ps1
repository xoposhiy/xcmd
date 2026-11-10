param(
    [string]$Filter,
    [string]$OutputPath,
    [switch]$KeepFixtures
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-Architecture.ps1')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet restore tests/Xcmd.Probe/Xcmd.Probe.csproj --configfile NuGet.Config --locked-mode --disable-parallel -m:1 -p:BuildInParallel=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet build tests/Xcmd.Probe/Xcmd.Probe.csproj -c Release --no-restore -m:1 -p:UseSharedCompilation=false -v minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $probeArguments = @('tests/Xcmd.Probe/bin/Release/net10.0/Xcmd.Probe.dll')
    if ($Filter) { $probeArguments += @('--filter', $Filter) }
    if ($OutputPath) { $probeArguments += @('--output', $OutputPath) }
    if ($KeepFixtures) { $probeArguments += '--keep-fixtures' }
    dotnet @probeArguments
    exit $LASTEXITCODE
}
finally { Pop-Location }
