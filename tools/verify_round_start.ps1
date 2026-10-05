param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskRepo = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $env:TEMP ('km-round-start-regression-' + [Guid]::NewGuid().ToString('N'))
}
$taskGenerated = Join-Path $OutputDirectory 'generated'
New-Item -ItemType Directory -Force -Path $taskGenerated | Out-Null
function Read-Source([string]$relativePath) {
    [IO.File]::ReadAllText((Join-Path $taskRepo $relativePath))
}
function Read-Method([string]$source, [string]$signature) {
    $taskStart = $source.IndexOf($signature)
    if ($taskStart -lt 0) { throw "Method missing: $signature" }
    $taskOpen = $source.IndexOf('{', $taskStart)
    $taskDepth = 1
    $taskEnd = $taskOpen + 1
    while ($taskDepth -gt 0) {
        if ($source[$taskEnd] -eq '{') { $taskDepth++ }
        if ($source[$taskEnd] -eq '}') { $taskDepth-- }
        $taskEnd++
    }
    $source.Substring($taskStart, $taskEnd - $taskStart)
}
$taskTransitions = Read-Source 'Assets/Scripts/UI/GameUIManager.Transitions.cs'
$taskGuard = Read-Method $taskTransitions 'public bool DeferRoundStartBoardUpdate('
$taskQueue = Read-Method $taskTransitions 'public void DeferUntilIdle('
$taskQueueMethods = foreach ($signature in @('private void ResetDeferredWait(', 'private void ProcessDeferredActions(')) { Read-Method $taskTransitions $signature }
$taskQueueFieldsStart = $taskTransitions.IndexOf('private readonly List<KeyValuePair<string, Action>> deferredActions')
$taskQueueFieldsEnd = $taskTransitions.IndexOf('/// <summary>', $taskQueueFieldsStart)
$taskQueueFields = $taskTransitions.Substring($taskQueueFieldsStart, $taskQueueFieldsEnd - $taskQueueFieldsStart)
$taskQueueTimeout = $taskTransitions.Substring($taskTransitions.IndexOf('private const float DeferredActionTimeoutSeconds =')).Split(';')[0] + ';'
[IO.File]::WriteAllText((Join-Path $taskGenerated 'ManagerGuards.cs'), "using System; using System.Collections.Generic; using UnityEngine; namespace KillingMahjong.UI { public partial class GameUIManager { $taskGuard $taskQueueFields $taskQueueTimeout $taskQueue $($taskQueueMethods -join "`n") } }")
$taskDeal = Read-Source 'Assets/Scripts/Network/Handlers/DealingMessageHandler.cs'
$taskDealEnd = $taskDeal.IndexOf('private static void ApplyDealingCompleted(')
if ($taskDealEnd -lt 0) { throw 'Dealing application marker missing' }
$taskDeal = $taskDeal.Substring(0, $taskDealEnd) + @'
private static void ApplyDealingCompleted(string json, NetworkMessageHandler network) {
    Probe.Apply("deal:" + json, KillingMahjong.UI.RoundStartCoordinator.BoardUpdateKind.DealingCompleted);
    network.RaiseDealingCompleted();
}
}}
'@
[IO.File]::WriteAllText((Join-Path $taskGenerated 'DealingMessageHandler.cs'), $taskDeal)
$taskStatus = Read-Source 'Assets/Scripts/Network/Handlers/StatusMessageHandler.cs'
$taskStatusEnd = $taskStatus.IndexOf('StatusMessage statusMsg =')
if ($taskStatusEnd -lt 0) { throw 'Status application marker missing' }
$taskStatus = $taskStatus.Substring(0, $taskStatusEnd) + @'
Probe.Apply("status:" + jsonString, KillingMahjong.UI.RoundStartCoordinator.BoardUpdateKind.Status);
}}}
'@
[IO.File]::WriteAllText((Join-Path $taskGenerated 'StatusMessageHandler.cs'), $taskStatus)
$taskDarken = Read-Source 'Assets/Scripts/UI/PhaseTransitionUI.Darken.cs'
$taskFinishStart = $taskDarken.IndexOf('if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 1f);')
$taskFinishMarker = 'additionalReadyCallbacks?.Invoke();'
$taskFinishEnd = $taskDarken.IndexOf($taskFinishMarker, $taskFinishStart)
if ($taskFinishStart -lt 0 -or $taskFinishEnd -lt 0) { throw 'Darken completion markers missing' }
$taskCompletion = $taskDarken.Substring($taskFinishStart, $taskFinishEnd + $taskFinishMarker.Length - $taskFinishStart)
$taskCompletionSource = @"
using System;
namespace CompletionRegression {
public class CompletionProbe {
    public Material checkerMaterial = new Material();
    public bool IsDarkenTransitioning = true;
    private Action _additionalRoundStartDarkenedCallbacks = null, _additionalRoundStartReadyCallbacks = null;
    public Text centerText;
    private void PlayTransitionStinger(string name) {}
    public void Finish(Action onDarkened, Action onReady) { string text="round"; $taskCompletion }
}
public class Material { public float Progress; public void SetFloat(string name, float value) { Progress=value; } }
public class Text { public string text; public Color color; public GameObject gameObject=new GameObject(); }
public class Color { public static Color white=new Color(); }
public class GameObject { public void SetActive(bool value) {} }
}
"@
[IO.File]::WriteAllText((Join-Path $taskGenerated 'DarkenCompletion.cs'), $taskCompletionSource)
$taskProject = Join-Path $PSScriptRoot 'round-start-regression/RoundStartRegression.csproj'
& dotnet run --project $taskProject -v:q "-p:GeneratedSourceDirectory=$taskGenerated" "-p:BaseIntermediateOutputPath=$OutputDirectory/obj/" "-p:BaseOutputPath=$OutputDirectory/bin/"
if ($LASTEXITCODE -ne 0) { throw "Round-start regression failed ($LASTEXITCODE)" }
Write-Output "Regression artifacts: $OutputDirectory"
