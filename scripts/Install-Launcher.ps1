param(
    [string]$Destination,
    [string]$SourcePath = 'artifacts/launcher/publish/Xcmd.exe'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $Destination) { $Destination = $projectRoot }
$destinationRoot = [System.IO.Path]::GetFullPath($Destination, $projectRoot)
$source = [System.IO.Path]::GetFullPath($SourcePath, $projectRoot)
$launcher = Join-Path $destinationRoot 'Xcmd.exe'
$shortcut = Join-Path $destinationRoot 'Xcmd.lnk'
New-Item -ItemType Directory -Path $destinationRoot -Force | Out-Null
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
if (-not (Test-Path -LiteralPath $shortcut -PathType Leaf)) {
    $process = Start-Process -FilePath $launcher -ArgumentList @('--create-shortcut', ('"' + $shortcut + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $shortcut -PathType Leaf)) { throw 'The launcher shortcut could not be created.' }
}
Write-Output ('Ready launcher: ' + $launcher)
Write-Output ('Taskbar shortcut: ' + $shortcut)
