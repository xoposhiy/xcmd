# @implements QualityChecks
. (Join-Path $PSScriptRoot 'BuildEnvironment.ps1')
Push-Location $projectRoot
try {
    Write-Output 'CHECK Build the launcher'
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Build-Launcher.ps1')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output 'CHECK Restore locked packages'
    dotnet restore tests/Xcmd.Architecture/Xcmd.Architecture.csproj -r win-x64 -p:Configuration=Release --configfile NuGet.Config --locked-mode --disable-parallel -m:1 -p:BuildInParallel=false
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output 'CHECK Build and run compiler analyzers'
    dotnet build tests/Xcmd.Architecture/Xcmd.Architecture.csproj -c Release -r win-x64 --no-restore -m:1 -p:UseSharedCompilation=false -v minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output 'CHECK Architecture rules and analyzer fixtures'
    dotnet tests/Xcmd.Architecture/bin/Release/net10.0/win-x64/Xcmd.Architecture.dll $projectRoot
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output 'CHECK UI feedback identity analyzer'
    dotnet tools/UiChecks/bin/Release/net10.0/win-x64/UiChecks.dll --self-test
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output 'CHECK C# whitespace formatting'
    dotnet format whitespace Xcmd.slnx --no-restore --verify-no-changes
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output 'PASS C# formatting'
    Write-Output 'CHECK Code metrics and duplicate detection'
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-Metrics.ps1')
    exit $LASTEXITCODE
}
finally { Pop-Location }
