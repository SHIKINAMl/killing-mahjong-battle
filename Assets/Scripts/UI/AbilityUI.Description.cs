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

            // 縁は箱そのものの地の色。内側に面を1枚敷く（行のタイルと同じ作り）
            var boxImg = boxTr.GetComponent<Image>();
            boxImg.sprite = null;
            boxImg.color = DescBorderColor;
            boxImg.raycastTarget = false;

            var fillTr = boxTr.Find(DescFillName) as RectTransform;
            if (fillTr == null)
            {
                var fillGo = new GameObject(DescFillName, typeof(RectTransform), typeof(Image));
                fillTr = (RectTransform)fillGo.transform;
                fillTr.SetParent(boxTr, false);
            }
            var fillImg = fillTr.GetComponent<Image>();
            fillImg.sprite = null;
            fillImg.color = DescBoxColor;
            fillImg.raycastTarget = false;
            fillTr.anchorMin = Vector2.zero;
            fillTr.anchorMax = Vector2.one;
            fillTr.offsetMin = new Vector2(DescBorder, DescBorder);
            fillTr.offsetMax = new Vector2(-DescBorder, -DescBorder);
            fillTr.localScale = Vector3.one;
            fillTr.SetAsFirstSibling();

            // **巻物の外（右側）に出す**（2026-08-31）。
            // 内枠の下に置いていたときは、閉じるボタンと場所を取り合っていた。
            // 左上を基準にして、巻物の右端から DescPanelGap だけ離し、内枠の上端に頭を揃える。
            // 巻物の幅はシーンの値なので、定数に焼かず実物から取る。
            float windowHalfWidth = abilityWindow.rect.width * 0.5f;

            boxTr.anchorMin = new Vector2(0.5f, 0.5f);
            boxTr.anchorMax = new Vector2(0.5f, 0.5f);
            boxTr.pivot = new Vector2(0f, 1f);
            boxTr.sizeDelta = new Vector2(DescPanelWidth, DescPanelHeight);
            boxTr.anchoredPosition = new Vector2(
                windowHalfWidth + DescPanelGap,
                PanelInnerCenter.y + PanelInnerHeight * 0.5f);
            boxTr.localScale = Vector3.one;

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
            textTr.offsetMin = new Vector2(4f, 3f);
            textTr.offsetMax = new Vector2(-4f, -3f);
            textTr.localScale = Vector3.one;

            // **折り返して全文を出す。** 46文字の「強襲」が最長で、
            // 128 幅・44 高に 9px で 4 行なら収まる。収まらない分は自動で縮む。
            //
            // **`Overflow` にしないこと。** 自動縮小の下限(7px)でも入らない文言を
            // 足したとき、はみ出した行が巻物の枠を突き抜けて盤面に出てしまう。
            // `Truncate` なら最悪でも欄の中で切れて止まる。
            text.margin = Vector4.zero;
            text.color = Color.white;
            text.alignment = TMPro.TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TMPro.TextWrappingModes.Normal;
            text.overflowMode = TMPro.TextOverflowModes.Truncate;
            text.enableAutoSizing = true;
            text.fontSizeMax = 10f;
            text.fontSizeMin = 7f;
            text.raycastTarget = false;

            _descText = text;
            return _descText;
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
            fill.color = DescBoxColor;
            fill.raycastTarget = false;
            fillTr.anchorMin = Vector2.zero;
            fillTr.anchorMax = Vector2.one;
            fillTr.offsetMin = new Vector2(DescBorder, DescBorder);
            fillTr.offsetMax = new Vector2(-DescBorder, -DescBorder);
            fillTr.localScale = Vector3.one;
            fillTr.SetAsFirstSibling();
        }
    }
}
