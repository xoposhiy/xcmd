$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.dotnet'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Build-Launcher.ps1')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet restore tests/Xcmd.Architecture/Xcmd.Architecture.csproj -r win-x64 -p:Configuration=Release --configfile NuGet.Config --locked-mode --disable-parallel -m:1 -p:BuildInParallel=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet build tests/Xcmd.Architecture/Xcmd.Architecture.csproj -c Release -r win-x64 --no-restore -m:1 -p:UseSharedCompilation=false -v minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet tests/Xcmd.Architecture/bin/Release/net10.0/win-x64/Xcmd.Architecture.dll $projectRoot
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet tools/UiChecks/bin/Release/net10.0/win-x64/UiChecks.dll --self-test
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet format whitespace Xcmd.slnx --no-restore --verify-no-changes
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output 'PASS C# formatting'
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-Metrics.ps1')
    exit $LASTEXITCODE
}
finally { Pop-Location }
