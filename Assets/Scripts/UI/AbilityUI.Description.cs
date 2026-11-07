using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public partial class AbilityUI
    {
        private TMPro.TextMeshProUGUI EnsureDescriptionBox()
        {
            if (_descText != null) return _descText;
            if (abilityWindow == null) return null;

            var boxTr = abilityWindow.Find(DescBoxName) as RectTransform;
            if (boxTr == null)
            {
                var go = new GameObject(DescBoxName, typeof(RectTransform), typeof(Image));
                boxTr = (RectTransform)go.transform;
                boxTr.SetParent(abilityWindow, false);
            }

            // ベルルの上に置き、口元へ向かう尻尾を下に伸ばす。
            var boxImg = boxTr.GetComponent<Image>();
            boxImg.enabled = false;
            boxImg.raycastTarget = false;
            var legacyFill = boxTr.Find(DescFillName);
            if (legacyFill != null) legacyFill.gameObject.SetActive(false);
            boxTr.anchorMin = boxTr.anchorMax = new Vector2(.5f, .5f);
            boxTr.pivot = new Vector2(0f, 1f);
            boxTr.sizeDelta = new Vector2(DescPanelWidth, DescPanelHeight);
            boxTr.anchoredPosition = new Vector2(-18f, 271f);
            boxTr.localScale = Vector3.one;
            var art = boxTr.Find("SpeechOutline") as RectTransform;
            if (art == null)
            {
                art = new GameObject("SpeechOutline", typeof(RectTransform), typeof(Effects.PixelShapeGraphic))
                    .GetComponent<RectTransform>();
                art.SetParent(boxTr, false);
            }
            art.anchorMin = art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0f, 1f);
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = boxTr.sizeDelta;
            art.SetAsFirstSibling();
            var shape = art.GetComponent<Effects.PixelShapeGraphic>();
            shape.raycastTarget = false;
            DrawSkillSpeech(shape);
            var textTr = boxTr.Find(DescTextName) as RectTransform;
            TMPro.TextMeshProUGUI text;
            if (textTr == null)
            {
                var go = new GameObject(DescTextName, typeof(RectTransform));
                textTr = (RectTransform)go.transform;
                textTr.SetParent(boxTr, false);
                text = go.AddComponent<TMPro.TextMeshProUGUI>();
                if (tooltipText != null && tooltipText.font != null) text.font = tooltipText.font;
            }
            else
            {
                text = textTr.GetComponent<TMPro.TextMeshProUGUI>();
            }

            textTr.anchorMin = Vector2.zero;
            textTr.anchorMax = Vector2.one;
            textTr.offsetMin = new Vector2(12f, 12f);
            textTr.offsetMax = new Vector2(-12f, -12f);
            textTr.localScale = Vector3.one;

            // 文字は吹き出しの内側に収め、尻尾と縁には重ねない。
            ApplyDescriptionStyle(text);
            text.raycastTarget = false;

            _descText = text;
            return _descText;
        }

        /// <summary>
        /// 説明の字の大きさなどを当てる。**出すたびに当て直す**（2026-10-22）。
        ///
        /// ユーザーがチュートリアルを遊んでいる最中に見ると、自動縮小の幅が TextMeshPro の既定の 18〜72 に戻っていた。
        /// 18 より小さくならないので「字が大きすぎて文が入りきらない」（3行目で切れる）状態になっていた。
        /// 戻した経路は特定できていない（作り直しを非表示のまま行っても再現しなかった）ので、作るときだけでなく、
        /// 説明を出すたびに（表示した直後に）当て直す。
        /// </summary>
        private static void ApplyDescriptionStyle(TMPro.TextMeshProUGUI text)
        {
            text.margin = Vector4.zero;
            text.color = new Color32(0x21, 0x0B, 0x0B, 0xFF);
            text.alignment = TMPro.TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TMPro.TextWrappingModes.Normal;
            text.overflowMode = TMPro.TextOverflowModes.Truncate;
            text.enableAutoSizing = true;
            text.fontSizeMax = 14f;
            text.fontSizeMin = 13f;
        }
        // 既存UIと同じ2px単位。半透明や影を重ねず、輪郭を一重にする。
        private static void DrawSkillSpeech(Effects.PixelShapeGraphic shape)
        {
            const float tailX = 18f;
            var edge = new Color32(0x51, 0x19, 0x19, 0xFF);
            var fill = new Color32(0xFF, 0x93, 0x93, 0xFF);
            shape.Begin();
            // 中心をベルルへ向けた、段階的に細くなる尻尾。
            for (int row = 0; row < 10; row++)
            {
                float half = 12f - row;
                shape.Box(tailX - half, -DescPanelHeight - row * 2f - 2f,
                    tailX + half, -DescPanelHeight - row * 2f, edge);
                if (row < 8)
                    shape.Box(tailX + 2f - half, -DescPanelHeight - row * 2f - 2f,
                        tailX - 2f + half, -DescPanelHeight - row * 2f, fill);
            }
            // 左上を原点とするRectTransformなので、本文は下向きの座標。
            for (int row = 0; row < (int)DescPanelHeight / 2; row++)
            {
                float y = -row * 2f;
                int near = Mathf.Min(row, (int)DescPanelHeight / 2 - 1 - row);
                float inset = near == 0 ? 10f : near == 1 ? 6f : near == 2 ? 4f : 2f;
                shape.Box(inset, y - 2f, DescPanelWidth - inset, y, edge);
                if (near > 0)
                    shape.Box(inset + 2f, y - 2f, DescPanelWidth - inset - 2f, y, fill);
            }
            // 尻尾の付け根だけは本文とつなぐ。
            shape.Box(tailX - 10f, -DescPanelHeight, tailX + 10f, -DescPanelHeight + 2f, fill);
            shape.End();
        }
        private void HideLegacyTooltip()
        {
            if (tooltipText != null && !string.IsNullOrEmpty(tooltipText.text)) tooltipText.text = "";
            if (tooltipPanel != null && tooltipPanel.activeSelf) tooltipPanel.SetActive(false);
        }
        /// <summary>閉じるボタンの絵。`Assets/Resources/能力UIcloseButton.png`。</summary>
        private const string CloseButtonSpriteName = "能力UIcloseButton";

        /// <summary>
        /// 閉じるボタンを**一覧の下の空いた帯**へ置く（2026-08-31）。
        ///
        /// **シーンでは `役ListPanel` の子になっていて、巻物とは別の座標系にいた**ため、
        /// 右上に取り残されていた。巻物の子へ移して、内枠の下端から測って置き直す。
        /// 巻物の子にしておけば、開閉で巻物が動いても一緒に動く。
        /// </summary>
        private void LayoutCloseButton()
        {
            if (closeButton == null || abilityWindow == null) return;

            var rt = closeButton.transform as RectTransform;
            if (rt == null) return;

            if (rt.parent != abilityWindow) rt.SetParent(abilityWindow, false);
            rt.SetAsLastSibling(); // 巻物の絵より手前に出す

            float innerBottom = PanelInnerCenter.y - PanelInnerHeight * 0.5f;

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(CloseButtonSize, CloseButtonSize);
            rt.anchoredPosition = new Vector2(
                PanelInnerCenter.x,
                innerBottom + CloseButtonBottomMargin + CloseButtonSize * 0.5f);
            rt.localScale = Vector3.one;

            // 大きさが変わったので、絵の拡大率を計算し直す
            StyleCloseButton();
        }

        /// <summary>
        /// 説明欄の出し入れ。**何も選んでいないときは箱ごと消す**（2026-08-31）。
        /// 案内文を出しっぱなしにすると、対局開始時から巻物の外に箱が浮いて見える。
        /// </summary>
        private void SetDescriptionVisible(bool visible)
        {
            if (_descText == null) return;
            var box = _descText.transform.parent;
            if (box == null) return;
            if (box.gameObject.activeSelf != visible) box.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 閉じるボタンの見た目を作る。
        ///
        /// **Inspector で絵を差しても効かない。** この関数が毎回 `sprite` を触るので、
        /// 絵はここで入れないと、下の「色の四角」に上書きされてしまう。
        /// </summary>
        private void StyleCloseButton()
        {
            if (closeButton == null) return;
            var img = closeButton.GetComponent<Image>();
            if (img == null) return;

            // 絵があるならそれを出す。枠と塗りを重ねる下の処理は要らない。
            var sprite = Resources.Load<Sprite>(CloseButtonSpriteName);
            if (sprite != null)
            {
                // **素材は 101x157 だが、絵は (44,126)-(60,142) の 17x17 しかない。**
                // 残り約9割は透明な余白。そのまま貼るとボタンの中で豆粒になり、
                // しかも下寄りなので画面上でほぼ見えない（実際そうなっていた）。
                //
                // **素材は加工しない約束なので、コード側で吸収する。**
                // 絵だけを子に置いて拡大し、絵の中心がボタンの中心へ来るようずらす。
                // 素材を描き直したら、この4つの数値を測り直すこと。
                const float SpriteW = 101f, SpriteH = 157f;
                const float IconCenterX = 52f;   // (44+60)/2
                const float IconCenterYFromBottom = 22f; // 下から数えた中心。UVは下が原点
                const float IconSize = 17f;

                // ボタン本体は当たり判定だけ持たせて、絵は子に描かせる。
                // 本体を拡大すると、透明な余白まで押せる範囲になってしまう。
                img.sprite = null;
                img.color = new Color(1f, 1f, 1f, 0f);
                img.raycastTarget = true;

                var oldFill = img.rectTransform.Find(CloseFillName);
                if (oldFill != null) oldFill.gameObject.SetActive(false);

                var iconTr = img.rectTransform.Find(CloseIconName) as RectTransform;
                if (iconTr == null)
                {
                    var iconGo = new GameObject(CloseIconName, typeof(RectTransform), typeof(Image));
                    iconTr = (RectTransform)iconGo.transform;
                    iconTr.SetParent(img.rectTransform, false);
                }
                var iconImg = iconTr.GetComponent<Image>();
                iconImg.sprite = sprite;
                iconImg.color = Color.white;
                iconImg.type = Image.Type.Simple;
                iconImg.raycastTarget = false;

                // 絵の 17x17 がボタンの短辺いっぱいになる倍率
                var btnRect = img.rectTransform.rect;
                float k = Mathf.Min(btnRect.width, btnRect.height) / IconSize;

                iconTr.anchorMin = new Vector2(0.5f, 0.5f);
                iconTr.anchorMax = new Vector2(0.5f, 0.5f);
                iconTr.pivot = new Vector2(0.5f, 0.5f);
                iconTr.sizeDelta = new Vector2(SpriteW * k, SpriteH * k);
                // 素材の中心から見た絵の中心のずれを、逆向きに打ち消す
                iconTr.anchoredPosition = new Vector2(
                    -(IconCenterX - SpriteW * 0.5f) * k,
                    -(IconCenterYFromBottom - SpriteH * 0.5f) * k);
                iconTr.localScale = Vector3.one;
                return;
            }

            // 絵が見つからないときは、これまでどおり色の四角で描く
            img.sprite = null;
            img.color = CloseBorderColor;

            var rt = img.rectTransform;
            var fillTr = rt.Find(CloseFillName) as RectTransform;
            if (fillTr == null)
            {
                var go = new GameObject(CloseFillName, typeof(RectTransform), typeof(Image));
                fillTr = (RectTransform)go.transform;
                fillTr.SetParent(rt, false);
            }
            var fill = fillTr.GetComponent<Image>();
            fill.sprite = null;
            fill.color = new Color32(0x1B, 0x23, 0x38, 0xE6);
            fill.raycastTarget = false;
            fillTr.anchorMin = Vector2.zero;
            fillTr.anchorMax = Vector2.one;
            fillTr.offsetMin = new Vector2(2f, 2f);
            fillTr.offsetMax = new Vector2(-2f, -2f);
            fillTr.localScale = Vector3.one;
            fillTr.SetAsFirstSibling();
        }
    }
}
