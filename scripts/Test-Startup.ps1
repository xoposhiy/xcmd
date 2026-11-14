param(
    [Parameter(Mandatory)][string]$ApplicationPath,
    [string]$OutputPath,
    [ValidateRange(3, 30)][int]$Runs = 7,
    [switch]$Native
)

. (Join-Path $PSScriptRoot 'BuildEnvironment.ps1')
$application = [System.IO.Path]::GetFullPath($ApplicationPath, $projectRoot)
if (-not $OutputPath) { $OutputPath = 'artifacts/startup/' + [Guid]::NewGuid().ToString('N') }
$output = [System.IO.Path]::GetFullPath($OutputPath, $projectRoot)
if (Test-Path -LiteralPath $output) { throw 'Use a new output directory for each startup measurement.' }
$fixture = Join-Path $output 'installation'
$id = '20261110-000000-000-00000001'
$release = Join-Path $fixture ('releases/' + $id)
New-Item -ItemType Directory -Path $release -Force | Out-Null
$probe = Join-Path $output 'driver'
dotnet publish (Join-Path $projectRoot 'tests/Xcmd.Probe/Xcmd.Probe.csproj') -c Release -r win-x64 --no-self-contained -p:PublishReadyToRun=true -p:RestoreLockedMode=true --output $probe -m:1 -p:UseSharedCompilation=false -v minimal
if ($LASTEXITCODE -ne 0) { throw 'The precompiled startup driver could not be built.' }
Copy-Item -Path (Join-Path $probe '*') -Destination $release -Recurse
Copy-Item -Path (Join-Path $application '*') -Destination $release -Recurse -Force
Copy-Item -LiteralPath (Join-Path $probe 'Xcmd.Probe.exe') -Destination (Join-Path $release 'Xcmd.Desktop.exe') -Force
$launcher = Join-Path $fixture 'Xcmd.exe'
Copy-Item -LiteralPath (Join-Path $projectRoot 'artifacts/launcher/publish/Xcmd.exe') -Destination $launcher
@{ id = $id; restartProtocol = 1 } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $release 'release.json')
'{"total":1,"failures":0}' | Set-Content -LiteralPath (Join-Path $release 'verification.json')
$id | Set-Content -LiteralPath (Join-Path $fixture 'releases/latest.txt')
$left = Join-Path $fixture 'left'
$right = Join-Path $fixture 'right'
foreach ($directory in @($left, $right)) {
    New-Item -ItemType Directory -Path $directory | Out-Null
    1..250 | ForEach-Object { [System.IO.File]::WriteAllText((Join-Path $directory ('file-{0:D3}.txt' -f $_)), 'Startup fixture') }
}
function New-PanelState([string]$directory) {
    return @{ DirectoryPath = $directory; Sort = @{ Field = 0; Descending = $false }; Cursor = 'file-020.txt'; Selected = @('file-020.txt');
        History = @($directory); Cursors = @{ $directory = 'file-020.txt' }; Columns = @(); ScrollX = 0; ScrollY = 0; Sizes = @() }
}
$session = Join-Path $fixture 'session.json'
@{ Version = 1; Left = (New-PanelState $left); Right = (New-PanelState $right); ActivePanel = 'right';
    Window = @{ X = 50; Y = 50; Width = 1280; Height = 760; Maximized = $false; Active = $true }; Notes = @();
    UsageVersion = 2; Usage = @(); Favorites = @(); CommandLine = @{ Draft = 'tool unfinished'; History = @('tool previous'); Caret = 4; SelectionStart = 4; SelectionEnd = 4; Focused = $false }
} | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $session
$samples = @()
for ($run = 1; $run -le $Runs; $run++) {
    $reportPath = Join-Path $output ('run-' + $run + '.json')
    $start = [System.Diagnostics.ProcessStartInfo]::new($launcher)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WorkingDirectory = $fixture
    foreach ($argument in @('--startup-host', $reportPath, $session)) { $start.ArgumentList.Add($argument) }
    if ($Native) { $start.ArgumentList.Add('--native') }
    $started = [System.Diagnostics.Stopwatch]::GetTimestamp()
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $process = [System.Diagnostics.Process]::Start($start)
    try {
        if (-not $process.WaitForExit(30000)) { $process.Kill(); throw 'The startup launcher timed out.' }
        $launcherExitMs = $timer.Elapsed.TotalMilliseconds
        if ($process.ExitCode -ne 0) { throw 'The startup launcher failed.' }
        while (-not (Test-Path -LiteralPath $reportPath)) {
            if ($timer.Elapsed.TotalSeconds -ge 35) { throw 'The application startup timed out.' }
            Start-Sleep -Milliseconds 10
        }
        $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
        if ($report.visibleApplicationWindows -ne 0) { throw 'Startup measurement opened a visible window.' }
        if ($report.consoleAttached -ne $false) { throw 'The startup host created a console. Background test processes must not allocate consoles.' }
        if ($report.state.Left.DirectoryPath -ne $left -or $report.state.Right.DirectoryPath -ne $right -or
            $report.state.ActivePanel -ne 'right' -or $report.state.Right.Cursor -ne 'file-020.txt' -or $report.state.CommandLine.Draft -ne 'tool unfinished') {
            throw 'Startup did not restore the saved workspace.'
        }
        $readyMs = ($report.readyToShowTimestamp - $started) * 1000.0 / [System.Diagnostics.Stopwatch]::Frequency
        $frameMs = if ($Native) { $null } else { ($report.frameReadyTimestamp - $started) * 1000.0 / [System.Diagnostics.Stopwatch]::Frequency }
        $samples += @{ run = $run; launcherExitMs = $launcherExitMs; launchToReadyToShowMs = $readyMs; launchToOffscreenFrameMs = $frameMs; consoleAttached = $report.consoleAttached; phases = $report.phases }
        Write-Output ('Startup {0}: launcher {1:F0} ms; ready to show {2:F0} ms' -f $run, $launcherExitMs, $readyMs)
    }
    finally {
        $process.Dispose()
        $pidPath = $reportPath + '.pid'
        if (Test-Path -LiteralPath $pidPath) {
            $hostProcess = Get-Process -Id ([int](Get-Content -Raw -LiteralPath $pidPath)) -ErrorAction SilentlyContinue
            if ($hostProcess) {
                try {
                    if (-not $hostProcess.WaitForExit(10000)) { $hostProcess.Kill(); throw 'The startup host did not close.' }
                    if ($hostProcess.ExitCode -ne 0) { throw 'The startup host failed.' }
                }
                finally { $hostProcess.Dispose() }
            }
        }
    }
}
$ordered = @($samples.launchToReadyToShowMs | Sort-Object)
$middle = [int][Math]::Floor($ordered.Count / 2)
$median = if ($ordered.Count % 2) { $ordered[$middle] } else { ($ordered[$middle - 1] + $ordered[$middle]) / 2 }
$result = @{ backend = $(if ($Native) { 'Windows native platform, hidden window' } else { 'Avalonia.Headless, offscreen frame' });
    scope = 'Real launcher and App startup to the Show call with saved workspace; excludes Explorer click handling, native Show, and desktop presentation';
    application = $application; runs = $Runs; firstMs = $samples[0].launchToReadyToShowMs; medianMs = $median; maximumMs = $ordered[-1]; samples = $samples }
$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'results.json')
Write-Output ('Startup results: ' + (Join-Path $output 'results.json'))
