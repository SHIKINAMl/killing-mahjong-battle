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

                // 音は台詞に合わせて動く（2026-09-11）。**文字を出す直前に当てる。**
                // 表に無い台詞では何も起きず、直前の音がそのまま続く。
                // 設計は km-docs/tutorial/04_演出と音_統合.md。
                Tutorial.TutorialAudioDirector.OnLine(line);

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
        /// <param name="highlight">
        /// 指定すると、矢印の代わりに**範囲そのもの**を塗る／囲む（<see cref="UI.TutorialHighlightUI"/>）。
        /// 点数計算表のように、点で指しても何を指しているのか伝わらない相手に使う。
        /// </param>
        private void GuideTo(RectTransform target, bool useMask = true, Vector2? arrowOffset = null,
            UI.TutorialHighlightUI.Style? highlight = null)
        {
            if (target == null) return;

            // **面で示すときは矢印を出さない。** 帯は表の文字の上に重なって読みづらくなるし、
            // 枠のほうも、表の上端のすぐ外＝立ち絵のあごの前に 80x80 の矢印が立つ。
            // どこを見ればいいかは枠と帯がすでに言っているので、二重に指さない。
            if (arrowUI != null)
            {
                if (highlight.HasValue) arrowUI.Hide();
                else arrowUI.ShowAt(target, arrowOffset ?? new Vector2(0, 50f));
            }

            if (highlight.HasValue) UI.TutorialHighlightUI.Show(target, highlight.Value);
            else UI.TutorialHighlightUI.HideCurrent();

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
            UI.TutorialHighlightUI.HideCurrent();
        }

    }
}
