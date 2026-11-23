# Parse recorded PowerShell as text. Do not run the script or expand variables.
param([Parameter(Mandatory)][string]$InputPath)
$ErrorActionPreference = 'Stop'
$rows = Get-Content -LiteralPath $InputPath -Raw -Encoding utf8 | ConvertFrom-Json
$result = @{}
foreach ($row in $rows) {
    $tokens = $null
    $problems = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseInput($row.command, [ref]$tokens, [ref]$problems)
    $commands = @($ast.FindAll({param($node) $node -is [System.Management.Automation.Language.CommandAst]}, $true))
    $nodes = foreach ($command in $commands | Select-Object -First 28) {
        $name = $command.GetCommandName()
        if (-not $name) { $name = '[dynamic command]' }
        $literals = foreach ($element in $command.CommandElements | Select-Object -Skip 1 -First 30) {
            if ($element -is [System.Management.Automation.Language.StringConstantExpressionAst]) {
                if ($element.Value.Length -le 180) { $element.Value }
            } elseif ($element -is [System.Management.Automation.Language.ArrayLiteralAst]) {
                foreach ($part in $element.Elements) {
                    if ($part -is [System.Management.Automation.Language.StringConstantExpressionAst]) { $part.Value }
                }
            }
        }
        $fileArgs = @()
        $hasPaths = $name -in @('Get-Content','Get-ChildItem','cat','ls','dir')
        $skip = $false
        if ($hasPaths) {
            foreach ($part in $command.CommandElements | Select-Object -Skip 1) {
                if ($skip) { $skip = $false; continue }
                if ($part -is [System.Management.Automation.Language.CommandParameterAst]) {
                    if ($part.ParameterName -in @('TotalCount','First','Last','Tail','Encoding','Depth','Filter','Include','Exclude')) { $skip = $true }
                    elseif ($part.ParameterName -notin @('Path','LiteralPath','Raw','Force','Recurse','File','Directory','Name','Hidden','ReadOnly')) { $hasPaths=$false; break }
                    if ($part.Argument) { $hasPaths=$false; break }
                } elseif ($part -is [System.Management.Automation.Language.StringConstantExpressionAst]) {
                    $fileArgs += $part.Value
                } elseif ($part -is [System.Management.Automation.Language.ArrayLiteralAst]) {
                    foreach ($entry in $part.Elements) {
                        if ($entry -isnot [System.Management.Automation.Language.StringConstantExpressionAst]) { $hasPaths=$false; break }
                        $fileArgs += $entry.Value
                    }
                } else { $hasPaths=$false; break }
            }
        }
        [pscustomobject]@{name=$name; literals=@($literals); redirects=[bool]$command.Redirections.Count;
            text=$command.Extent.Text; hasPaths=$hasPaths; fileArgs=$fileArgs}
    }
    $flat = @()
    if ($problems.Count -eq 0 -and $ast.EndBlock.Statements.Count -le 12) {
        foreach ($statement in $ast.EndBlock.Statements) {
            if ($statement -isnot [System.Management.Automation.Language.PipelineAst]) { $flat = @(); break }
            $flat += $statement.Extent.Text
        }
    }
    $result[$row.id] = [pscustomobject]@{parsed=($problems.Count -eq 0); commands=@($nodes); statements=$flat}
}
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
ConvertTo-Json -InputObject $result -Depth 7 -Compress
