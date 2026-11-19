# @implements CodeHealth
function Get-CodeMetricsRunner([string]$projectRoot) {
    $toolsProject = Join-Path $projectRoot 'scripts/code-health/CodeHealth.Tools.csproj'
    $nugetConfig = Join-Path $projectRoot 'NuGet.Config'
    dotnet restore $toolsProject -r win-x64 -p:Configuration=Release --configfile $nugetConfig --packages (Join-Path $projectRoot '.packages') --locked-mode --disable-parallel -m:1 | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'The pinned reporting tools could not be restored.' }
    $runnerRoot = Join-Path $projectRoot 'artifacts/code-health-tools/5.6.0'
    $runner = Join-Path $runnerRoot 'Metrics/Metrics.exe'
    if (-not (Test-Path -LiteralPath $runner)) {
        New-Item -ItemType Directory -Path $runnerRoot -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $projectRoot '.packages/microsoft.codeanalysis.metrics/5.6.0/Metrics') -Destination $runnerRoot -Recurse
    }
    $hostPath = Join-Path $runnerRoot 'Metrics/BuildHost-netcore'
    if (-not (Test-Path -LiteralPath $hostPath)) {
        # The Metrics package omits the host required to load SDK projects.
        Copy-Item -LiteralPath (Join-Path $projectRoot '.packages/microsoft.codeanalysis.workspaces.msbuild/4.12.0/contentFiles/any/any/BuildHost-netcore') -Destination (Split-Path $runner -Parent) -Recurse
    }
    return $runner
}
