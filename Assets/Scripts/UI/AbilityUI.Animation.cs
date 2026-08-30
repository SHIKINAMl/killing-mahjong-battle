using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public partial class AbilityUI
    {
        private void OnTriggerClicked()
        {
            ToggleAbilityWindow();
            CancelOpponentReady();
            
            if (buttonPressCoroutine != null) StopCoroutine(buttonPressCoroutine);
            buttonPressCoroutine = StartCoroutine(ButtonPressRoutine());
        }

        private System.Collections.IEnumerator ButtonPressRoutine()
        {
            if (triggerButtonImage != null && pressedSprite != null)
            {
                triggerButtonImage.sprite = pressedSprite;
            }
            
            yield return new WaitForSeconds(buttonPressDuration);
            
            if (triggerButtonImage != null && normalSprite != null)
            {
                triggerButtonImage.sprite = normalSprite;
            }
        }
        private System.Collections.IEnumerator AnimateWindow(Vector2 targetPos, bool isOpening)
        {
            if (abilityWindow == null) yield break;

            // 巻物のコマが入っていればコマ送り、無ければ従来のスライド
            if (HasFrames)
            {
                yield return FrameAnimation(isOpening);
                yield break;
            }

            if (isOpening) abilityWindow.gameObject.SetActive(true);

            Vector2 startPos = abilityWindow.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                t = t * t * (3f - 2f * t);

                abilityWindow.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            abilityWindow.anchoredPosition = targetPos;

            if (!isOpening) abilityWindow.gameObject.SetActive(false);
        }

        private bool HasFrames => openFrames != null && openFrames.Length > 0;

        /// <summary>
        /// 巻物が開く／巻き取られるコマ送り。
        ///
        /// **スライドはしない。** 巻物はその場で開く絵なので、位置まで動かすと
        /// 「飛んできながら開く」ことになって二重の動きになる。
        /// 開いた位置（showPosition）に置いたままコマだけ送る。
        ///
        /// **中身は開き切ってから出す。** 小さく丸まったコマの上に能力の行が
        /// 浮いていると、何が起きているのか読めない。
        /// 閉じるときは逆で、先に中身を消してから巻き取る。
        /// </summary>
        private System.Collections.IEnumerator FrameAnimation(bool isOpening)
        {
            var img = ResolveFrameImage();

            if (isOpening)
            {
                abilityWindow.anchoredPosition = showPosition;
                abilityWindow.gameObject.SetActive(true);
            }
            SetContentsVisible(false);

            int last = openFrames.Length - 1;
            float wait = Mathf.Max(0f, frameSeconds);

            for (int step = 0; step <= last; step++)
            {
                // 開くときは 0→last、閉じるときは last→0
                int i = isOpening ? step : last - step;
                if (img != null && openFrames[i] != null) img.sprite = openFrames[i];

                // **最後のコマだけは待たない。** 開いた瞬間に操作できてほしいし、
                // 閉じるときも最後のコマを見せてから消す必要がない
                if (step < last) yield return new WaitForSeconds(wait);
            }

            if (isOpening)
            {
                SetContentsVisible(true);
            }
            else
            {
                abilityWindow.gameObject.SetActive(false);
                abilityWindow.anchoredPosition = hiddenPosition;

                // 次に開くときは最初のコマから始める
                if (img != null && openFrames[0] != null) img.sprite = openFrames[0];
            }
        }

        /// <summary>コマを描く Image。未設定なら abilityWindow の Image を使う。</summary>
        private Image ResolveFrameImage()
        {
            if (windowFrameImage != null) return windowFrameImage;
            if (abilityWindow == null) return null;

            windowFrameImage = abilityWindow.GetComponent<Image>();
            return windowFrameImage;
        }

        private void SetContentsVisible(bool visible)
        {
            // 説明欄は一覧と一緒に出し入れする
            var box = abilityWindow != null ? abilityWindow.Find(DescBoxName) : null;
            if (box != null && box.gameObject.activeSelf != visible) box.gameObject.SetActive(visible);

            if (contentsShownWhenOpen == null) return;
            for (int i = 0; i < contentsShownWhenOpen.Length; i++)
            {
                var go = contentsShownWhenOpen[i];
                if (go == null) continue;

                // **旧ツールチップはここで出さない。**
                // シーンの `contentsShownWhenOpen` に `TooltipPanel` が入っているため、
                // 巻物が開き切るたびにダミー文字列の箱が盤面の上に出ていた（2026-08-24 に判明）。
                if (tooltipPanel != null && go == tooltipPanel) continue;

                if (go.activeSelf != visible) go.SetActive(visible);
            }
        }

        private void CancelOpponentReady()
        {
            Debug.Log("Ability Triggered: Opponent's Ready State Cancelled!");
        }
    }
}
