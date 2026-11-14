. (Join-Path $PSScriptRoot 'BuildEnvironment.ps1')
$releaseId = (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$testOutput = Join-Path $projectRoot ('artifacts/probe/release-' + $releaseId)

Push-Location $projectRoot
try {
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-Xcmd.ps1') -OutputPath $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Verification failed. No release was published.' }
    $resultsPath = Join-Path $testOutput 'results.json'
    $results = Get-Content -Raw -LiteralPath $resultsPath | ConvertFrom-Json
    & (Join-Path $PSScriptRoot 'Publish-Application.ps1') -ReleaseId $releaseId -Verification $results `
        -StartupOutputPath (Join-Path $projectRoot ('artifacts/startup/release-' + $releaseId))
}
finally { Pop-Location }
