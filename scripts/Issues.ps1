param(
    [Parameter(Position = 0)][ValidateSet('list', 'start', 'close', 'release', 'reopen', 'import-legacy', 'help')][string]$Command = 'list',
    [Parameter(Position = 1, ValueFromRemainingArguments)][string[]]$Id
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$releaseId = (Get-Content -LiteralPath (Join-Path $projectRoot 'releases/latest.txt') -Raw).Trim()
if ($releaseId -notmatch '^[a-zA-Z0-9-]+$') { throw 'Invalid release marker.' }
$application = Join-Path $projectRoot "releases/$releaseId/Xcmd.Desktop.dll"
if (-not (Test-Path -LiteralPath $application -PathType Leaf)) { throw 'Publish a feedback build before using the issue CLI.' }
$metadata = Get-Content -LiteralPath (Join-Path $projectRoot "releases/$releaseId/release.json") -Raw | ConvertFrom-Json
if ($metadata.issueProtocol -ne 1) { throw 'The installed release does not support the issue CLI. Publish the updated application first.' }
& dotnet $application issues $Command @Id
exit $LASTEXITCODE
