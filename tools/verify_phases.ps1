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
$taskCovered = Read-Block $taskSource 'internal void RunCoveredBoardUpdate('
$taskQueue = Read-Block $taskSource 'public void DeferUntilIdle('
$taskQueueMethods = foreach ($signature in @('private void ResetDeferredWait(', 'internal void ResetDeferredActions(', 'private void ProcessDeferredActions(')) { Read-Block $taskSource $signature }
$taskQueueFieldsStart = $taskSource.IndexOf('private readonly List<KeyValuePair<string, Action>> deferredActions')
$taskQueueFieldsEnd = $taskSource.IndexOf('/// <summary>', $taskQueueFieldsStart)
$taskQueueFields = $taskSource.Substring($taskQueueFieldsStart, $taskQueueFieldsEnd - $taskQueueFieldsStart)
$taskQueueTimeout = $taskSource.Substring($taskSource.IndexOf('private const float DeferredActionTimeoutSeconds =')).Split(';')[0] + ';'
$taskPropertiesStart = $taskSource.IndexOf('private int coveredBoardUpdateDepth;')
$taskPropertiesEnd = $taskSource.IndexOf('/// <summary>', $taskPropertiesStart)
$taskProperties = $taskSource.Substring($taskPropertiesStart, $taskPropertiesEnd - $taskPropertiesStart)
[IO.File]::WriteAllText((Join-Path $taskGenerated 'Manager.cs'), "using System; using System.Collections.Generic; using UnityEngine; namespace KillingMahjong.UI { public partial class GameUIManager { $taskProperties $taskCovered $taskQueueFields $taskQueueTimeout $taskQueue $($taskQueueMethods -join "`n") } }")
$taskFlow = Read-Source 'Assets/Scripts/UI/GameUIPhaseController.RoundFlow.cs'
$taskReady = Read-Block $taskFlow 'public void HandleNextRoundWaitingReceived('
$taskSend = Read-Block $taskFlow 'private void SendNextRoundAction('
$taskRon = Read-Source 'Assets/Scripts/UI/GameUIPhaseController.Ron.cs'
$taskRonFinish = Read-Block $taskRon 'private void CompleteRonPresentation('
$taskRonComplete = Read-Block $taskRon 'private void OnRonAnimationComplete('
$taskRoundStartCancel = Read-Block (Read-Source 'Assets/Scripts/UI/GameUIPhaseController.RoundStart.cs') 'private void CancelRoundStartTransition('
[IO.File]::WriteAllText((Join-Path $taskGenerated 'NextRoundMessages.cs'), "using KillingMahjong.Network; using KillingMahjong.EngineData; using KillingMahjong.Managers; using UnityEngine; namespace KillingMahjong.UI { public partial class GameUIPhaseController { $taskReady $taskSend $taskRonFinish $taskRonComplete $taskRoundStartCancel } }")
$taskSkill = Read-Source 'Assets/Scripts/UI/GameUISkillController.Transitions.cs'
$taskSkillRequest = Read-Block $taskSkill 'public void CancelPendingSkillRequest('
$taskSkillCancel = Read-Block $taskSkill 'internal void CancelActiveTransitions('
$taskSkillRoutine = Read-Block $taskSkill 'private System.Collections.IEnumerator HandleSkillCastedRoutine('
[IO.File]::WriteAllText((Join-Path $taskGenerated 'SkillLifetime.cs'), "using KillingMahjong.Network; using KillingMahjong.EngineData; using System.Collections.Generic; namespace KillingMahjong.UI { public partial class GameUISkillController { $taskSkillRequest $taskSkillCancel $taskSkillRoutine } }")
$taskNetwork = Read-Source 'Assets/Scripts/UI/GameUINetworkHandler.cs'
$taskError = Read-Block $taskNetwork 'private void HandleError('
[IO.File]::WriteAllText((Join-Path $taskGenerated 'NetworkError.cs'), "namespace KillingMahjong.UI { public partial class GameUINetworkHandler { $taskError } }")
$taskResult = Read-Source 'Assets/Scripts/UI/GameUIManager.GameResult.cs'
$taskResultMethods = foreach ($signature in @('private void HandleGameEnded(', 'public void ShowGameResult(', 'private void TryStartGameResult(', 'internal void ResetGameResultState(', 'private System.Collections.IEnumerator ShowGameResultRoutine(', 'public void RecordHpHistory(')) { Read-Block $taskResult $signature }
$taskResultFieldsStart = $taskResult.IndexOf('private bool gameResultShown')
$taskResultFieldsEnd = $taskResult.IndexOf('public void ShowGameResult(', $taskResultFieldsStart)
$taskResultFields = $taskResult.Substring($taskResultFieldsStart, $taskResultFieldsEnd - $taskResultFieldsStart)
$taskUpdate = Read-Block (Read-Source 'Assets/Scripts/UI/GameUIManager.Transitions.cs') 'private void Update('
[IO.File]::WriteAllText((Join-Path $taskGenerated 'GameResult.cs'), "using UnityEngine; using KillingMahjong.EngineData; using KillingMahjong.Managers; namespace KillingMahjong.UI { public partial class GameUIManager { $taskResultFields $($taskResultMethods -join "`n") $taskUpdate } }")
$taskDtos = Read-Source 'Assets/Scripts/EngineData/ServerMessages.cs'
$taskNames = @('LiquidationData','BettingCompletedInfo','DiscardCompletedMessage','DiscardCompletedData','DiscardAcceptedMessage','DiscardAcceptedData','RoundEndMessage','RoundEndData','AgariPendingMessage','AgariPendingData','GameEndInfo','DrawPlayerData')
$taskBlocks = foreach ($taskName in $taskNames) { Read-Block $taskDtos "public class $taskName" }
[IO.File]::WriteAllText((Join-Path $taskGenerated 'Messages.cs'), "using System; using System.Collections.Generic; namespace KillingMahjong.EngineData { $($taskBlocks -join "`n") }")
& dotnet run --project (Join-Path $PSScriptRoot 'phase-regression/PhaseRegression.csproj') -v:q "-p:GeneratedSourceDirectory=$taskGenerated" "-p:BaseIntermediateOutputPath=$OutputDirectory/obj/" "-p:BaseOutputPath=$OutputDirectory/bin/"
if ($LASTEXITCODE -ne 0) { throw "Phase regression failed ($LASTEXITCODE)" }
Write-Output "Regression artifacts: $OutputDirectory"
