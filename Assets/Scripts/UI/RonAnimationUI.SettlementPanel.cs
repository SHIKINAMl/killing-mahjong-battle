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
            // 自分が上がったときは青、相手が上がったときは赤（2026-10-25 のユーザー指示）。
            // **枠は付けない。** 単色の地に、血飛沫の模様（AddSettlementSplatter）を重ねるだけ。
            // codex のドット絵の枠を3通り試したが、ユーザーから「表の枠が微妙」「端っこの枠みたいなもんはいらない」。
            // 色は codex の2回目の絵の地と帯の色（km-docs screenshots/20261025/settlement_pixel/README.md）
            bool blue = s.LocalWon;
            GameObject root = new GameObject("SettlementPanel");
            root.transform.SetParent(parent, false);
            Image rootImg = root.AddComponent<Image>();
            rootImg.color = blue ? GroundBlue : GroundRed;

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
            rootLayout.padding = new RectOffset(0, 0, 0, 0);
            rootLayout.spacing = 0;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            AddSettlementSplatter(root.transform, blue);

            // ---- 見出しの帯：左に「和了」、右にランク ----
            // 帯は地より一段明るい同じ系統の色。端まで塗り、縁や飾りは付けない。
            // 絵（デバッグ部屋の本と同じ作りのドット絵、280x20 ドット＝560x40 単位）があれば、それを等倍で貼る
            var headSprite = Resources.Load<Sprite>(blue ? HeadPixelBlue : HeadPixelRed);
            GameObject head = MakeBox(root.transform, headSprite != null ? Color.clear : (blue ? HeadBlue : HeadRed), 16, 6);
            if (headSprite != null)
            {
                var headImg = head.AddComponent<Image>();
                headImg.sprite = headSprite;
                headImg.color = Color.white;
                var headLe = head.AddComponent<LayoutElement>();
                headLe.minHeight = headLe.preferredHeight = headSprite.rect.height * 2f;
            }
            MakeText(head.transform, "和了", 20f, PanelInk, TextAlignmentOptions.Left, true);
            // 型の答え合わせ（2026-10-15）。相手に和了られたときに「相手は染めだった」と分かれば、
            // 次からその捨て牌の特徴を覚えていける。行を足すと役5行で画面からはみ出すので見出しに入れる
            var archetype = HandArchetype.Classify(RowNames(s));
            if (archetype != null)
            {
                MakeText(head.transform, s.LocalWon ? "自分の型" : "相手の型", 16f,
                    s.LocalWon ? AccentMine : AccentThem, TextAlignmentOptions.Right, false, 120f);
                // 型は角丸の文字の札で出す（役一覧・手牌選択と同じ札）
                HandTypeBadge.Create(head.transform, "TypeBadge", 18f, customFont, false).Show(archetype);
            }
            MakeText(head.transform, s.RankName, 30f, AccentGold, TextAlignmentOptions.Right, false, 200f);

            // ---- 中身（地の色） ----
            GameObject body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            // 地は表（root）の色をそのまま見せる
            var bodyLayout = body.AddComponent<VerticalLayoutGroup>();
            bodyLayout.padding = new RectOffset(16, 16, 8, 10);
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
                // 強化は翻ではなく精算の段階（満貫→跳満…）を上げるので「段」と書く（2026-10-25）
                MakeText(r.transform, row.Boost > 0 ? "+" + row.Boost + "段" : "", 18f, AccentGold, TextAlignmentOptions.Right, false, 70f);
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

        /// <summary>
        /// 表の地に血飛沫の模様を付ける（2026-10-25 のユーザー指示「能力発動のカットインの背景を作った時のような感じの模様のつけ方」）。
        ///
        /// カットインと同じ付け方で、既存の血飛沫の絵（Textures/UI/blood_splatter.png）を地より暗い色で数枚散らす。
        /// **絵は Image-to-Pixel（Tezumie, MIT）と同じ手順でドット絵にした物**（同日のユーザー指示「これの作り方で作ってみてほしい」）:
        /// 72x72 に縮め、形は Bayer 4x4、濃淡は地より暗い3色に Floyd–Steinberg で置き換えた（km-docs tools/pixelate_splatter.py）。
        /// ドットを崩さないため、回転させず等倍（1ドット＝2UI単位）で置き、左右・上下の反転だけで変化を付ける。
        /// ドット絵が読めないときは、元の絵を染めて回して置く（カットインと同じ）。
        /// **字の後ろを避けて角と端に寄せる**。表からはみ出した部分は切る（RectMask2D）。並び（VerticalLayoutGroup）には加わらない。
        /// </summary>
        private void AddSettlementSplatter(Transform root, bool blue)
        {
            var pixelSprite = Resources.Load<Sprite>(blue ? SplatterPixelBlue : SplatterPixelRed);
            if (pixelSprite == null && settlementSplatterSprite == null)
            {
                var phase = FindFirstObjectByType<PhaseTransitionUI>(FindObjectsInactive.Include);
                settlementSplatterSprite = phase != null ? phase.BloodSplatterSprite : null;
            }
            bool hasSplatter = pixelSprite != null || settlementSplatterSprite != null;

            var layer = new GameObject("SplatterPattern", typeof(RectTransform), typeof(RectMask2D), typeof(LayoutElement));
            layer.transform.SetParent(root, false);
            layer.GetComponent<LayoutElement>().ignoreLayout = true;
            var lr = (RectTransform)layer.transform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;

            // 地の模様（2026-10-25 のユーザー指示「血だけではなく背景も。デバッグ部屋にある本の模様を参考に」）。
            // 本と同じ作り（上→下の狭い濃淡を4色・bayer4 で減色。tools/CutinPixelArt/settlement.mjs）。
            // 表の高さは役の数で変わるので、一番高いとき（340）の大きさで作ってあり、表の上にそろえて下を切る。
            // 伸ばすとドットが崩れるので、いつも等倍（1ドット＝2UI単位）で置く
            var groundSprite = Resources.Load<Sprite>(blue ? GroundPixelBlue : GroundPixelRed);
            if (groundSprite != null)
            {
                var g = new GameObject("GroundPattern", typeof(RectTransform), typeof(Image));
                g.transform.SetParent(lr, false);
                var gr = (RectTransform)g.transform;
                gr.anchorMin = gr.anchorMax = gr.pivot = new Vector2(0.5f, 1f);
                gr.anchoredPosition = Vector2.zero;
                gr.sizeDelta = groundSprite.rect.size * 2f;
                var gi = g.GetComponent<Image>();
                gi.sprite = groundSprite;
                gi.color = Color.white;
                gi.raycastTarget = false;
            }

            // 置き場所は表の角と端（錨の位置、錨からのずれ）。役の名前（左）と数字（右）の列の外側に寄せる
            var spots = new[]
            {
                (anchor: new Vector2(1f, 1f), offset: new Vector2(-50f, -80f)),
                (anchor: new Vector2(0f, 0f), offset: new Vector2(40f, 30f)),
                (anchor: new Vector2(1f, 0f), offset: new Vector2(-150f, 20f)),
            };
            var rng = new System.Random();
            // 元の絵を使うときの色（地の色を一段暗くした色）。ドット絵は絵の色そのまま
            Color tint = blue ? new Color32(8, 16, 38, 150) : new Color32(30, 6, 12, 150);
            foreach (var spot in spots)
            {
                if (!hasSplatter) break;
                var go = new GameObject("Splatter", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(lr, false);
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = spot.anchor;
                r.anchoredPosition = spot.offset;
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                if (pixelSprite != null)
                {
                    img.sprite = pixelSprite;
                    img.color = Color.white;
                    r.sizeDelta = pixelSprite.rect.size * 2f; // 1ドット＝2UI単位
                    r.localScale = new Vector3(rng.Next(2) == 0 ? 1f : -1f, rng.Next(2) == 0 ? 1f : -1f, 1f);
                }
                else
                {
                    img.sprite = settlementSplatterSprite;
                    img.color = tint;
                    img.preserveAspect = true;
                    float size = 190f * (0.85f + 0.3f * (float)rng.NextDouble());
                    r.sizeDelta = new Vector2(size, size);
                    r.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 360f);
                }
            }
        }

        private Sprite settlementSplatterSprite;

        private static IEnumerable<string> RowNames(RonSettlementInfo s)
        {
            foreach (var row in s.Rows)
                if (row != null) yield return row.Name;
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
