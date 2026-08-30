using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class RonAnimationUI
    {
        private CanvasGroup BuildSettlementPanel(RectTransform parent, RonSettlementInfo s,
            List<TextMeshProUGUI> hanTexts,
            out TextMeshProUGUI totalHanText, out TextMeshProUGUI multiplierText,
            out TextMeshProUGUI myBetText, out TextMeshProUGUI theirBetText,
            out TextMeshProUGUI myMultText, out TextMeshProUGUI theirMultText,
            out TextMeshProUGUI tankiMine, out TextMeshProUGUI tankiTheirs,
            out TextMeshProUGUI myDeltaText, out TextMeshProUGUI theirDeltaText,
            out TextMeshProUGUI myHpText, out TextMeshProUGUI theirHpText)
        {
            // 外枠（線の色）→ 内側（地の色）の2枚重ね。1ドット＝2UI単位なので枠は4
            GameObject root = new GameObject("SettlementPanel");
            root.transform.SetParent(parent, false);
            Image rootImg = root.AddComponent<Image>();
            rootImg.color = PanelLine;

            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            // **下端を固定して上へ伸ばす。** 役が増えても下（手牌）を押さない。
            // 基準 800x600 で手牌の上端は中心から -146 なので、その少し上に置く。
            // 上へ使えるのは中心から +300 まで。役5行でちょうど収まる寸法にしてある
            // （実機で -105 に置いたら見出しの帯が画面外に出た）。
            rootRt.pivot = new Vector2(0.5f, 0f);
            rootRt.anchoredPosition = new Vector2(0f, -140f);
            rootRt.sizeDelta = new Vector2(PanelWidth, 0f);

            var rootFit = root.AddComponent<ContentSizeFitter>();
            rootFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var rootLayout = root.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(4, 4, 4, 4);
            rootLayout.spacing = 0;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            // ---- 見出しの帯：左に「和了」、右にランク ----
            GameObject head = MakeBox(root.transform, PanelLine, 8, 4);
            MakeText(head.transform, "和了", 20f, PanelInk, TextAlignmentOptions.Left, true);
            MakeText(head.transform, s.RankName, 30f, AccentGold, TextAlignmentOptions.Right, false, 200f);

            // ---- 中身（地の色） ----
            GameObject body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<Image>().color = PanelBg;
            var bodyLayout = body.AddComponent<VerticalLayoutGroup>();
            bodyLayout.padding = new RectOffset(12, 12, 6, 6);
            bodyLayout.spacing = 1;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = true;
            bodyLayout.childForceExpandHeight = false;

            // ---- 役の行 ----
            foreach (var row in s.Rows)
            {
                GameObject r = MakeBox(body.transform, Color.clear, 0, 1);
                MakeText(r.transform, row.Name, 22f, PanelInk, TextAlignmentOptions.Left, true);
                // 強化ぶんは黄色で独立させる。`断幺九+1` と地の文に埋めない
                MakeText(r.transform, row.Boost > 0 ? "+" + row.Boost : "", 18f, AccentGold, TextAlignmentOptions.Right, false, 70f);
                hanTexts.Add(MakeText(r.transform, "", 22f, PanelInk, TextAlignmentOptions.Right, false, 90f));
            }

            // ---- 合計 → 倍率 ----
            MakeRule(body.transform);
            GameObject sum = MakeBox(body.transform, Color.clear, 0, 2);
            MakeText(sum.transform, "合計", 22f, PanelFaint, TextAlignmentOptions.Left, true);
            totalHanText = MakeText(sum.transform, "", 24f, AccentGold, TextAlignmentOptions.Right, false, 90f);
            multiplierText = MakeText(sum.transform, "", 24f, AccentGold, TextAlignmentOptions.Right, false, 70f);

            // ---- 自分と相手の内訳 ----
            MakeRule(body.transform);

            GameObject colHead = MakeBox(body.transform, Color.clear, 0, 1);
            MakeText(colHead.transform, "", 18f, PanelFaint, TextAlignmentOptions.Left, true);
            MakeText(colHead.transform, "自分", 18f, AccentMine, TextAlignmentOptions.Right, false, ValueColumn);
            MakeText(colHead.transform, "相手", 18f, AccentThem, TextAlignmentOptions.Right, false, ValueColumn);

            // 持ち越しがあると素点が膨らむ。**その理由が今までどこにも出ていなかった**
            string betLabel = s.CarryRounds > 1 ? $"素点（持ち越し{s.CarryRounds}局ぶん）" : "素点";
            GameObject betRow = MakeBox(body.transform, Color.clear, 0, 1);
            MakeText(betRow.transform, betLabel, 18f, PanelFaint, TextAlignmentOptions.Left, true);
            myBetText = MakeText(betRow.transform, "", 21f, PanelInk, TextAlignmentOptions.Right, false, ValueColumn);
            theirBetText = MakeText(betRow.transform, "", 21f, PanelInk, TextAlignmentOptions.Right, false, ValueColumn);

            GameObject multRow = MakeBox(body.transform, Color.clear, 0, 1);
            MakeText(multRow.transform, "倍率", 18f, PanelFaint, TextAlignmentOptions.Left, true);
            myMultText = MakeText(multRow.transform, "", 21f, PanelInk, TextAlignmentOptions.Right, false, ValueColumn);
            theirMultText = MakeText(multRow.transform, "", 21f, PanelInk, TextAlignmentOptions.Right, false, ValueColumn);

            tankiMine = null;
            tankiTheirs = null;
            if (s.IsTankiWait)
            {
                GameObject tankiRow = MakeBox(body.transform, Color.clear, 0, 1);
                MakeText(tankiRow.transform, "単騎待ち", 18f, PanelFaint, TextAlignmentOptions.Left, true);
                tankiMine = MakeText(tankiRow.transform, "", 21f, AccentGold, TextAlignmentOptions.Right, false, ValueColumn);
                tankiTheirs = MakeText(tankiRow.transform, "", 21f, AccentGold, TextAlignmentOptions.Right, false, ValueColumn);
            }

            // 強襲は「獲得が 0 に潰れ、その分が相手への追加ダメージへ回る」。式ではなく文で見せる
            if (s.AssaultApplied)
            {
                GameObject assaultRow = MakeBox(body.transform, Color.clear, 0, 1);
                MakeText(assaultRow.transform, "強襲", 18f, PanelFaint, TextAlignmentOptions.Left, true);
                MakeText(assaultRow.transform, s.LocalWon ? "獲得なし" : "", 20f, AccentGold, TextAlignmentOptions.Right, false, ValueColumn);
                MakeText(assaultRow.transform, s.LocalWon ? "+" + s.AssaultBonusDamage : "獲得なし", 20f, AccentGold, TextAlignmentOptions.Right, false, ValueColumn);
            }

            MakeRule(body.transform);

            GameObject deltaRow = MakeBox(body.transform, Color.clear, 0, 2);
            MakeText(deltaRow.transform, "血", 22f, PanelInk, TextAlignmentOptions.Left, true);
            myDeltaText = MakeText(deltaRow.transform, "", 30f, AccentMine, TextAlignmentOptions.Right, false, ValueColumn);
            theirDeltaText = MakeText(deltaRow.transform, "", 30f, AccentThem, TextAlignmentOptions.Right, false, ValueColumn);

            GameObject hpRow = MakeBox(body.transform, Color.clear, 0, 1);
            MakeText(hpRow.transform, "", 18f, PanelFaint, TextAlignmentOptions.Left, true);
            myHpText = MakeText(hpRow.transform, "", 18f, PanelFaint, TextAlignmentOptions.Right, false, ValueColumn);
            theirHpText = MakeText(hpRow.transform, "", 18f, PanelFaint, TextAlignmentOptions.Right, false, ValueColumn);

            // **血とHPの2行は伏せておく。**（2026-08-29）
            // 血の増減はこのあとの「飛ぶ数字」が答えとして出すもので、パネルに先に書くと山が消える。
            // 行そのものを消さずに非アクティブにしているのは、
            // **out で返す4本の参照を維持したまま**（＝リフレクション経由の検証手順を壊さずに）
            // 見せ方だけ戻せるようにするため。戻すなら SetActive(true) の1行でよい。
            deltaRow.SetActive(false);
            hpRow.SetActive(false);

            return group;
        }

        /// <summary>横1列の入れ物。中身は左から順に並ぶ。</summary>
        private GameObject MakeBox(Transform parent, Color bg, int padX, int padY)
        {
            GameObject go = new GameObject("Row");
            go.transform.SetParent(parent, false);

            if (bg.a > 0f) go.AddComponent<Image>().color = bg;

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(padX, padX, padY, padY);
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            return go;
        }

        /// <summary>区切りの細い線。1ドット＝2UI単位なので高さ2。</summary>
        private void MakeRule(Transform parent)
        {
            GameObject go = new GameObject("Rule");
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = PanelLine;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 2f;
            le.preferredHeight = 2f;
        }

        /// <param name="flexible">true なら残りの幅を全部取る（左の見出し用）</param>
        /// <param name="width">flexible が false のときの固定幅</param>
        private TextMeshProUGUI MakeText(Transform parent, string content, float size, Color color,
            TextAlignmentOptions align, bool flexible, float width = 0f)
        {
            GameObject go = new GameObject("Text");
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (customFont != null) tmp.font = customFont;
            tmp.text = content;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = size * 1.15f;
            if (flexible)
            {
                le.flexibleWidth = 1f;
                le.minWidth = 0f;
            }
            else
            {
                le.flexibleWidth = 0f;
                le.preferredWidth = width;
                le.minWidth = width;
            }

            return tmp;
        }
    }
}
