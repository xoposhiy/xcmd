param([string]$SourcePath = 'src')

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$python = Join-Path $projectRoot 'artifacts/lint-venv/Scripts/python.exe'
Push-Location $projectRoot
try {
    $source = Get-Item -LiteralPath $SourcePath
    $sourceFiles = if ($source.PSIsContainer) {
        @(Get-ChildItem -LiteralPath $source.FullName -Filter '*.cs' -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
    } else { @($source | Where-Object Extension -eq '.cs') }
    if ($sourceFiles.Count -eq 0) { throw 'Code metrics require at least one C# source file.' }
    if (-not (Test-Path -LiteralPath $python)) {
        python -m venv artifacts/lint-venv
        if ($LASTEXITCODE -ne 0) { throw 'Could not create the lint environment.' }
        & $python -m pip install --disable-pip-version-check -r scripts/requirements-lint.txt
        if ($LASTEXITCODE -ne 0) { throw 'Could not install the pinned lint tools.' }
    }
    & $python -c "from importlib.metadata import version; assert version('lizard') == '1.25.1', 'Recreate artifacts/lint-venv with the pinned requirements.'"
    if ($LASTEXITCODE -ne 0) { throw 'The Lizard version does not match the project policy.' }
    $arguments = @('-m', 'lizard', '-l', 'csharp', '-x', '*/obj/*', '-x', '*/bin/*', '-C', '25', '-T', 'nloc=150', '-a', '8', '-Ecognitive', '--CogC', '25', '-END', '--ND', '10', '-Eduplicate', '-w', $SourcePath)
    $report = & $python @arguments
    $status = $LASTEXITCODE
    if ($status -ne 0 -or ($report | Select-String -SimpleMatch 'Duplicate block:')) {
        $report | Write-Output
        throw 'Code metrics failed. Limits: CCN 25, cognitive complexity 25, nesting depth 10, 150 code lines, 8 parameters, no duplicate blocks of 70 tokens or more.'
    }
    Write-Output 'PASS code metrics and duplicate detection'
}
finally { Pop-Location }
