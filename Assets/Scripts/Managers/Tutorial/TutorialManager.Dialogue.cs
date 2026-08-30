using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.UI;
using KillingMahjong.EngineData;
using KillingMahjong.Common;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        // TutorialManager: セリフ表示・誘導（矢印＋マスク）

        // ==================== セリフ表示 ====================

        /// <summary>
        /// 台本のセリフを1行ずつ送る。シナリオ用コルーチンからのみ呼ばれるため、
        /// 同時に複数走ることはない（旧実装の多重起動によるコルーチン残留を防いでいる）。
        /// </summary>
        private IEnumerator PlayLines(List<TutorialLine> lines)
        {
            if (lines == null) yield break;

            foreach (var line in lines)
            {
                if (line == null || string.IsNullOrEmpty(line.text)) continue;

                bool clicked = false;
                if (dialogueUI != null)
                {
                    dialogueUI.gameObject.SetActive(true);
                    dialogueUI.ShowText(Decorate(line));
                    // 画面のどこをクリックしても進む（要望15）。小さなOKボタンは出さない
                    dialogueUI.ShowAdvanceOnAnyClick(() => clicked = true);
                }
                else
                {
                    clicked = true;
                }

                // 送り待ちの間は牌を触らせない（OnTryMoveTile で弾く）
                _isWaitingForLine = true;
                yield return new WaitUntil(() => clicked);
                _isWaitingForLine = false;

                if (dialogueUI != null) dialogueUI.HideAdvanceOnAnyClick();
            }
        }

        /// <summary>
        /// 操作を弾いたときの一言。送りボタンを使わないので PlayLines と競合しない。
        /// 連打されても直前のものを止めるだけで、コルーチンは残らない。
        /// </summary>
        private void ShowInterruptMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (_interruptRoutine != null) StopCoroutine(_interruptRoutine);
            _interruptRoutine = StartCoroutine(InterruptRoutine(text));
        }

        private IEnumerator InterruptRoutine(string text)
        {
            if (dialogueUI != null)
            {
                dialogueUI.gameObject.SetActive(true);
                dialogueUI.ShowText($"「{text}」");
            }
            yield return new WaitForSeconds(interruptMessageDuration);
            _interruptRoutine = null;
        }

        private static string Decorate(TutorialLine line)
        {
            switch (line.speaker)
            {
                case TutorialSpeaker.System:
                    return line.text;
                default:
                    return line.text.Contains("「") ? line.text : $"「{line.text}」";
            }
        }

        // ==================== 誘導（矢印＋マスク） ====================

        /// <param name="useMask">
        /// false にすると矢印だけで指し示す。マスクは穴の外側のクリックを全て食べるので、
        /// セリフ送りと併用したい場面（説明しながら指す）では必ず false にすること。
        /// </param>
        private void GuideTo(RectTransform target, bool useMask = true)
        {
            if (target == null) return;

            if (arrowUI != null) arrowUI.ShowAt(target, new Vector2(0, 50f));

            if (useMask)
            {
                if (maskUI != null) maskUI.Show(target);
            }
            else if (maskUI != null)
            {
                maskUI.Hide();
            }
        }

        private void ClearGuide()
        {
            if (arrowUI != null) arrowUI.Hide();
            if (maskUI != null) maskUI.Hide();
        }

    }
}
