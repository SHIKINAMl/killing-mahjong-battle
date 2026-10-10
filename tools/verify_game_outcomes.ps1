param([string]$OutputDirectory, [string]$FixturesPath)
$ErrorActionPreference = 'Stop'
$taskPreviousFixtures = [Environment]::GetEnvironmentVariable('KM_OUTCOME_FIXTURES')
try {
    if (-not $FixturesPath) { $FixturesPath = Join-Path $PSScriptRoot 'outcome-fixtures/server-current-20261006.json' }
    $env:KM_OUTCOME_FIXTURES = (Resolve-Path -LiteralPath $FixturesPath).Path
    & (Join-Path $PSScriptRoot 'verify_phases.ps1') -OutputDirectory $OutputDirectory
}
finally {
    [Environment]::SetEnvironmentVariable('KM_OUTCOME_FIXTURES', $taskPreviousFixtures)
}
