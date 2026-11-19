# @implements StableShortcut
param(
    [string]$Destination,
    [string]$SourcePath = 'artifacts/launcher/publish/Xcmd.exe',
    [string]$IconPath = 'src/Xcmd.Desktop/Assets/app.ico'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $Destination) { $Destination = $projectRoot }
$destinationRoot = [System.IO.Path]::GetFullPath($Destination, $projectRoot)
$source = [System.IO.Path]::GetFullPath($SourcePath, $projectRoot)
$launcher = Join-Path $destinationRoot 'Xcmd.exe'
$shortcut = Join-Path $destinationRoot 'Xcmd.lnk'
$iconSource = [System.IO.Path]::GetFullPath($IconPath, $projectRoot)
$iconHash = (Get-FileHash -LiteralPath $iconSource -Algorithm SHA256).Hash.ToLowerInvariant()
$iconDirectory = Join-Path $destinationRoot 'releases/icons'
$icon = Join-Path $iconDirectory ($iconHash + '.ico')
New-Item -ItemType Directory -Path $destinationRoot -Force | Out-Null
New-Item -ItemType Directory -Path $iconDirectory -Force | Out-Null
if (-not (Test-Path -LiteralPath $icon -PathType Leaf)) { Copy-Item -LiteralPath $iconSource -Destination $icon }
if (-not (Test-Path -LiteralPath $launcher) -or (Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $launcher).Hash) {
    $temporary = $launcher + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        Copy-Item -LiteralPath $source -Destination $temporary
        try { [System.IO.File]::Move($temporary, $launcher, $true) }
        catch [System.IO.IOException], [System.UnauthorizedAccessException] {
            if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) { throw }
            Write-Output 'The existing launcher could not be replaced. Its update is deferred; release publishing can continue.'
        }
    }
    finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
}
$process = Start-Process -FilePath $source -ArgumentList @('--create-shortcut', ('"' + $shortcut + '"'), ('"' + $launcher + '"'), ('"' + $icon + '"')) -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $shortcut -PathType Leaf)) { throw 'The launcher shortcut could not be updated.' }
Write-Output ('Ready launcher: ' + $launcher)
Write-Output ('Taskbar shortcut: ' + $shortcut)
