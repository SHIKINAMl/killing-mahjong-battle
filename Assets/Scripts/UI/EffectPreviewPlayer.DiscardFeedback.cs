using System.Collections;
using UnityEngine;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    public sealed partial class EffectPreviewPlayer
    {
        private IEnumerator PreviewDiscardFeedback()
        {
            var audio = AudioManager.Instance;
            if (audio != null && !audio.IsBgmPlaying && !audio.IsPairBgmRunning
                && !audio.AreLayersRunning && !audio.IsProposal3Running)
                audio.SetPhaseBgm(EngineData.RoundStatus.Discard);
            var holder = scope.Own(new GameObject("DiscardFeedbackPreview"));
            holder.transform.SetParent(rig.transform, false);
            var director = holder.AddComponent<DiscardFeedbackDirector>();
            director.ConfigurePreview(rig.enemy);
            scope.AddCleanup(director.ResetPresentation);
            var voltage = VoltageUI.CreatePresentation(rig.surface);
            scope.Own(voltage.gameObject);
            var board = rig.surface.GetComponentInChildren<TutorialReplayBoard>(true);
            board.localRiver.gameObject.SetActive(true);
            if (board.localRiver.TurnTextRect != null) board.localRiver.TurnTextRect.gameObject.SetActive(false);
            int tileIndex = 0;
            // 固定局面を表示するだけで、VoltageSystem・対局状態・戦績は更新しない。
            foreach (int count in new[] { 1, 8, 14, 16 })
            {
                var tile = Instantiate(board.tilePrefab, rig.surface, false).GetComponent<RectTransform>();
                board.localRiver.AddPresentationTile(tile, Tile(tileIndex++));
                AudioManager.Instance?.PlayDiscardSE();
                director.Present(false, count);
                bool late = count >= 14;
                voltage.PresentFeedback(late ? 2 : 1, false, late ? 6 : 2, late ? 5 : 2, late ? 9 : 5, late ? 1.3f : 1.1f);
                AudioManager.Instance?.PlayVoltageHeartbeat(count != 8, late ? 2 : 1);
                yield return new WaitForSecondsRealtime(1.5f);
            }
            var repeated = Instantiate(board.tilePrefab, rig.surface, false).GetComponent<RectTransform>();
            board.localRiver.AddPresentationTile(repeated, Tile(tileIndex - 1));
            AudioManager.Instance?.PlayDiscardSE();
            director.Present(false, 17);
            voltage.PresentFeedback(0, true, 0, 0, 2, 1f);
            AudioManager.Instance?.PlayVoltageBreakSE();
            yield return new WaitForSecondsRealtime(1.2f);
        }
    }
}
