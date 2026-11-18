# @implements TryLocalRelease, ReleaseHandoff
[CmdletBinding(SupportsShouldProcess)]
param([string]$ReleaseRoot = (Join-Path (Split-Path $PSScriptRoot -Parent) 'releases'))

$ErrorActionPreference = 'Stop'
$releaseIdPattern = '^\d{8}-\d{6}-\d{3}-[a-f0-9]{8}$'
if (-not (Test-Path -LiteralPath $ReleaseRoot -PathType Container)) { return }
$root = Get-Item -LiteralPath $ReleaseRoot
if ($root.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'The release root must be a local directory.' }
$marker = Join-Path $root.FullName 'latest.txt'
$lock = & (Join-Path $PSScriptRoot 'Enter-XcmdLock.ps1') -Path $marker
try {
    if (-not [IO.File]::Exists($marker)) { return }
    $current = [IO.File]::ReadAllText($marker).Trim()
    if ($current -cnotmatch $releaseIdPattern) { throw 'The current release ID is invalid.' }

    $releases = @(foreach ($directory in Get-ChildItem -LiteralPath $root.FullName -Directory) {
        if ($directory.Name -cnotmatch $releaseIdPattern -or
            [StringComparer]::Ordinal.Compare($directory.Name, $current) -gt 0 -or
            ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) { continue }
        if (-not [IO.File]::Exists((Join-Path $directory.FullName 'Xcmd.Desktop.exe'))) { continue }
        try {
            $manifest = [IO.File]::ReadAllText((Join-Path $directory.FullName 'release.json')) | ConvertFrom-Json
            $verification = [IO.File]::ReadAllText((Join-Path $directory.FullName 'verification.json')) | ConvertFrom-Json
        }
        catch { continue }
        if ($manifest.id -cne $directory.Name -or $manifest.restartProtocol -ne 1 -or
            $verification.total -le 0 -or $verification.failures -ne 0) { continue }
        $phase = if ($verification.fullVerification -eq $true -or
            (-not $manifest.verificationPhase -and [IO.File]::Exists((Join-Path $directory.FullName 'startup-verification.json')))) { 'full' }
        else { $manifest.verificationPhase }
        [pscustomobject]@{ Directory = $directory; Id = $directory.Name; Phase = $phase }
    })
    if ($current -cnotin $releases.Id) { throw 'The current release could not be verified.' }

    $keep = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $ordered = $releases | Sort-Object Id -Descending
    foreach ($release in $ordered | Select-Object -First 3) { $null = $keep.Add($release.Id) }
    foreach ($phase in 'targeted', 'full') {
        $release = $ordered | Where-Object Phase -eq $phase | Select-Object -First 1
        if ($release) { $null = $keep.Add($release.Id) }
    }
    $pins = Join-Path $root.FullName 'keep.txt'
    if ([IO.File]::Exists($pins)) {
        foreach ($line in [IO.File]::ReadAllLines($pins)) {
            $id = $line.Trim()
            if ($id -cmatch $releaseIdPattern) { $null = $keep.Add($id) }
        }
    }
    foreach ($process in Get-CimInstance Win32_Process -Filter "Name = 'Xcmd.Desktop.exe'") {
        if (-not $process.ExecutablePath) { throw 'Could not determine the path of a running Xcmd process.' }
        $processDirectory = [IO.Path]::GetDirectoryName($process.ExecutablePath)
        if ([IO.Path]::GetDirectoryName($processDirectory) -eq $root.FullName) {
            $null = $keep.Add([IO.Path]::GetFileName($processDirectory))
        }
    }

    foreach ($release in $ordered) {
        if ($keep.Contains($release.Id) -or $release.Directory.Parent.FullName -ne $root.FullName) { continue }
        $path = $release.Directory.FullName
        if (Get-ChildItem -LiteralPath $path -Recurse -Force -Attributes ReparsePoint | Select-Object -First 1) {
            Write-Warning "Skipped a release with a reparse point at '$path'."
            continue
        }
        if ($PSCmdlet.ShouldProcess($path, 'Remove old Xcmd release')) {
            try {
                Remove-Item -LiteralPath $path -Recurse -Force
                Write-Output "Removed old release: $($release.Id)"
            }
            catch { Write-Warning "Could not remove the release at '$path'. $($_.Exception.Message)" }
        }
    }
}
finally {
    $lock.ReleaseMutex()
    $lock.Dispose()
}
