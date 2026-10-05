param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $env:TEMP ('km-phase-regression-' + [Guid]::NewGuid().ToString('N')) }
$taskGenerated = Join-Path $OutputDirectory 'generated'
New-Item -ItemType Directory -Force -Path $taskGenerated | Out-Null
function Read-Source([string]$path) { [IO.File]::ReadAllText((Join-Path $taskRepo $path)) }
function Read-Block([string]$source, [string]$signature) {
    $taskStart = $source.IndexOf($signature)
    if ($taskStart -lt 0) { throw "Missing source block: $signature" }
    $taskOpen = $source.IndexOf('{', $taskStart)
    $taskDepth = 1; $taskEnd = $taskOpen + 1
    while ($taskDepth -gt 0) {
        if ($source[$taskEnd] -eq '{') { $taskDepth++ }
        if ($source[$taskEnd] -eq '}') { $taskDepth-- }
        $taskEnd++
    }
    $source.Substring($taskStart, $taskEnd - $taskStart)
}
$taskSource = Read-Source 'Assets/Scripts/UI/GameUIManager.Transitions.cs'
$taskLock = Read-Block $taskSource 'public void SetIsTransitioning('
$taskCovered = Read-Block $taskSource 'internal void RunCoveredBoardUpdate('
$taskQueue = Read-Block $taskSource 'public void DeferUntilIdle('
$taskPropertiesStart = $taskSource.IndexOf('private int coveredBoardUpdateDepth;')
$taskPropertiesEnd = $taskSource.IndexOf('/// <summary>', $taskPropertiesStart)
$taskProperties = $taskSource.Substring($taskPropertiesStart, $taskPropertiesEnd - $taskPropertiesStart)
[IO.File]::WriteAllText((Join-Path $taskGenerated 'Manager.cs'), "using System; using System.Collections.Generic; using UnityEngine; namespace KillingMahjong.UI { public partial class GameUIManager { $taskProperties $taskLock $taskCovered $taskQueue } }")
$taskDtos = Read-Source 'Assets/Scripts/EngineData/ServerMessages.cs'
$taskNames = @('LiquidationData','BettingCompletedInfo','DiscardCompletedMessage','DiscardCompletedData','DiscardAcceptedMessage','DiscardAcceptedData','RoundEndMessage','RoundEndData','AgariPendingMessage','AgariPendingData','GameEndInfo','DrawPlayerData')
$taskBlocks = foreach ($taskName in $taskNames) { Read-Block $taskDtos "public class $taskName" }
[IO.File]::WriteAllText((Join-Path $taskGenerated 'Messages.cs'), "using System; using System.Collections.Generic; namespace KillingMahjong.EngineData { $($taskBlocks -join "`n") }")
& dotnet run --project (Join-Path $PSScriptRoot 'phase-regression/PhaseRegression.csproj') -v:q "-p:GeneratedSourceDirectory=$taskGenerated" "-p:BaseIntermediateOutputPath=$OutputDirectory/obj/" "-p:BaseOutputPath=$OutputDirectory/bin/"
if ($LASTEXITCODE -ne 0) { throw "Phase regression failed ($LASTEXITCODE)" }
Write-Output "Regression artifacts: $OutputDirectory"
