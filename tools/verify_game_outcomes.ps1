param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskPreviousFixtures = [Environment]::GetEnvironmentVariable('KM_OUTCOME_FIXTURES')
try {
    $env:KM_OUTCOME_FIXTURES = Join-Path $PSScriptRoot 'outcome-fixtures/server-current-20261006.json'
    & (Join-Path $PSScriptRoot 'verify_phases.ps1') -OutputDirectory $OutputDirectory
}
finally {
    [Environment]::SetEnvironmentVariable('KM_OUTCOME_FIXTURES', $taskPreviousFixtures)
}
