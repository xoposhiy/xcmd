# @implements BrowseStories, TraceStoryFiles
param([int]$Port = 8765, [switch]$Check)

$ErrorActionPreference = 'Stop'
$entryPoint = Join-Path $PSScriptRoot '../tools/story-explorer/server.py'
$arguments = @($entryPoint, '--port', $Port)
if ($Check) { $arguments += '--check' }
& python @arguments
if ($LASTEXITCODE -ne 0) { throw 'The story explorer could not load the catalogue or source map.' }
