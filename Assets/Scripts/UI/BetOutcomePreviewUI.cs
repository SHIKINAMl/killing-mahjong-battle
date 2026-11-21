using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 賭け金を選んでいる間、**その額で自分の血がどう動くか**を管の絵で見せる札（2026-10-17）。
    ///
    /// ユーザーの言葉:「掛け金の適当さを直していきたい」「正直、今1200と1400の差がわからない」。
    /// 数字だけだと額の重さが伝わらないので、持っている血の量に対してどれだけ増える／減るかを、
    /// 同じ縮尺の管に重ねて見せる。**縮尺は血の総量に合わせる**（差を大きく見せるために拡大しない）。
    /// 小さい額は管の上でもほとんど動かない ── それが実際の重さなので、そのまま見せる。
    ///
    /// 見せるのは3つ:
    ///   勝ち … いまの血 + 予想報酬（サーバーの倍率・役強化込み。ボルテージは 1 倍）
    ///   負け … いまの血 − 賭け金（相手が満貫で上がったとき。相手の役が大きいと倍率ぶん増える）
    ///   単騎で負け … いまの血 − 賭け金×2
    ///
    /// 部品はシーンに置く（<see cref="SceneFirst"/>）。BettingPanel の子なので、パネルと一緒に出入りする。
    /// </summary>
    public sealed class BetOutcomePreviewUI : MonoBehaviour
    {
        public const string RootName = "BetOutcomePreview";

        // ---- 見た目（シーンに無かったときに作るための控え。置いた後はシーンが正）----
        // BettingPanel は 2 倍に拡大されているので、ここの 1 は画面の 2 ドット
        private const float CardW = 96f, CardH = 132f;
        private const float TubeX = 8f, TubeY = 22f, TubeW = 16f, TubeH = 102f;
        private static readonly Color Ink = new Color32(37, 27, 35, 238);
        private static readonly Color Cream = new Color32(239, 238, 216, 255);
        private static readonly Color TubeBack = new Color32(64, 30, 38, 255);
        private static readonly Color Blood = new Color32(196, 24, 44, 255);
        private static readonly Color Drain = new Color32(92, 14, 26, 255);
        private static readonly Color Gain = new Color32(255, 208, 86, 255);
        private static readonly Color TankiInk = new Color32(255, 150, 170, 255);
        private static readonly Color LoseInk = new Color32(255, 96, 110, 255);

        private RectTransform bloodFill, gainBand, drainBand, tankiTick;
        private TextMeshProUGUI winValue, loseValue, tankiValue, nowValue, winLabel;

        // 表示中の値（目標へ素早く寄せて、額を変えたときに血が動いて見えるようにする）
        private float shownNow, shownGain, shownLoss;
        private float targetNow, targetGain, targetLoss;
        private float scaleTop = 1f;
        private bool hasValue;

        /// <summary>BettingPanel の直下から探す。無ければ作る。</summary>
        public static BetOutcomePreviewUI Ensure(Transform bettingPanel)
        {
            if (bettingPanel == null) return null;
            var go = SceneFirst.Child(bettingPanel, RootName, out bool made, typeof(RectTransform), typeof(BetOutcomePreviewUI));
            var ui = go.GetComponent<BetOutcomePreviewUI>();
            if (made)
            {
                var r = (RectTransform)go.transform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot = new Vector2(1f, 0.5f);
                r.anchoredPosition = new Vector2(-6f, 0f);   // パネルの左外に置く
                r.sizeDelta = new Vector2(CardW, CardH);
            }
            ui.Build();
            return ui;
        }

        private void Build()
        {
            var back = Rect(transform, "Back", 0, 0, CardW, CardH, out bool made);
            if (made) { var img = back.gameObject.AddComponent<Image>(); img.color = Ink; img.raycastTarget = false; }
            var frame = Rect(transform, "Frame", 0, 0, CardW, CardH, out made);
            if (made) { var g = frame.gameObject.AddComponent<PixelShapeGraphic>(); g.raycastTarget = false; g.color = Cream; }
            var shape = frame.GetComponent<PixelShapeGraphic>();
            if (shape != null) { shape.Begin(); shape.Frame(frame.rect, 1, shape.color); shape.End(); }

            var title = Rect(transform, "Title", 4, 4, CardW - 8, 14, out made);
            if (made) Label(title, 10, Cream, TextAlignmentOptions.Center).text = "賭けたあとの血";

            // 管。下から血が溜まる
            var tube = Rect(transform, "Tube", TubeX, TubeY, TubeW, TubeH, out made);
            if (made) { var img = tube.gameObject.AddComponent<Image>(); img.color = TubeBack; img.raycastTarget = false; }
            bloodFill = Band(tube, "BloodFill", Blood);
            gainBand = Band(tube, "GainBand", Gain);
            drainBand = Band(tube, "DrainBand", Drain);
            tankiTick = Band(tube, "TankiTick", TankiInk);

            // 右の数字。行は固定（管の高さに合わせると、小さい額で重なって読めない）
            float x = TubeX + TubeW + 6f, w = CardW - x - 4f;
            winLabel = RowLabel("WinLabel", x, 22, w, "勝ち", Gain);
            winValue = RowValue("WinValue", x, 31, w, Gain);
            RowLabel("LoseLabel", x, 49, w, "負け", LoseInk);
            loseValue = RowValue("LoseValue", x, 58, w, LoseInk);
            RowLabel("TankiLabel", x, 76, w, "単騎で負け", TankiInk);
            tankiValue = RowValue("TankiValue", x, 85, w, TankiInk);
            RowLabel("NowLabel", x, 103, w, "いま", Cream);
            nowValue = RowValue("NowValue", x, 112, w, Cream);
        }

        /// <summary>
        /// 額を変えるたびに呼ぶ。
        /// </summary>
        /// <param name="current">いまの血</param>
        /// <param name="bet">選んでいる賭け金</param>
        /// <param name="gain">勝ったときに増える額。上がれないなら負の値</param>
        /// <param name="assault">強襲を撃った局（獲得 0、同額が相手への追加ダメージ）</param>
        /// <param name="scaleMax">管のいちばん上が表す血の量</param>
        public void SetOutcome(int current, int bet, int gain, bool assault, int scaleMax)
        {
            int loss = Mathf.Max(0, bet);
            int tankiLoss = Mathf.RoundToInt(loss * GameRules.TankiLossMultiplier);
            bool canWin = gain >= 0;
            int winGain = canWin && !assault ? gain : 0;

            scaleTop = Mathf.Max(1, scaleMax);
            targetNow = current; targetGain = winGain; targetLoss = loss;
            if (!hasValue) { shownNow = targetNow; shownGain = targetGain; shownLoss = targetLoss; hasValue = true; }

            if (winLabel != null) winLabel.text = assault ? "勝ち（強襲）" : "勝ち";
            if (winValue != null)
                winValue.text = !canWin ? "上がれない" : $"{current + winGain} <size=80%>+{winGain}</size>";
            if (loseValue != null) loseValue.text = $"{Mathf.Max(0, current - loss)} <size=80%>-{loss}</size>";
            if (tankiValue != null) tankiValue.text = $"{Mathf.Max(0, current - tankiLoss)} <size=80%>-{tankiLoss}</size>";
            if (nowValue != null) nowValue.text = current.ToString();
            Layout();
        }

        private void Update()
        {
            if (!hasValue) return;
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f);
            shownNow = Mathf.Lerp(shownNow, targetNow, k);
            shownGain = Mathf.Lerp(shownGain, targetGain, k);
            shownLoss = Mathf.Lerp(shownLoss, targetLoss, k);
            Layout();
        }

        private void Layout()
        {
            float px(float hp) => Mathf.Clamp(hp / scaleTop, 0f, 1f) * TubeH;
            float now = px(shownNow);
            float lossTop = now, lossBottom = px(shownNow - shownLoss);
            float tanki = px(shownNow - shownLoss * GameRules.TankiLossMultiplier);

            Place(bloodFill, 0f, now);
            Place(gainBand, now, px(shownNow + shownGain));
            // 負けで流れ出る分は、血の上側を暗くして見せる
            Place(drainBand, lossBottom, lossTop);
            // 単騎の線は 1 ドット。管より少し横にはみ出させて目立たせる
            if (tankiTick != null)
            {
                tankiTick.anchoredPosition = new Vector2(-2f, Mathf.Round(tanki));
                tankiTick.sizeDelta = new Vector2(TubeW + 4f, 1f);
            }
        }

        private static void Place(RectTransform band, float from, float to)
        {
            if (band == null) return;
            // ドット絵に合わせて整数に揃える。0 より大きければ最低 1 ドットは見せる
            float y0 = Mathf.Round(from), h = Mathf.Round(to) - y0;
            if (to - from > 0.01f && h < 1f) h = 1f;
            band.anchoredPosition = new Vector2(0f, y0);
            band.sizeDelta = new Vector2(TubeW, Mathf.Max(0f, h));
        }

        private static RectTransform Band(RectTransform tube, string name, Color color)
        {
            var go = SceneFirst.Child(tube, name, out bool made, typeof(RectTransform), typeof(Image));
            var r = (RectTransform)go.transform;
            if (made)
            {
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 0f);
                r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(TubeW, 0f);
                var img = go.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
            }
            return r;
        }

        private TextMeshProUGUI RowLabel(string name, float x, float y, float w, string text, Color color)
        {
            var r = Rect(transform, name, x, y, w, 10, out bool made);
            if (made) Label(r, 8, color, TextAlignmentOptions.Left).text = text;
            return r.GetComponent<TextMeshProUGUI>();
        }

        private TextMeshProUGUI RowValue(string name, float x, float y, float w, Color color)
        {
            var r = Rect(transform, name, x, y, w, 13, out bool made);
            if (made) Label(r, 11, color, TextAlignmentOptions.Left).text = "";
            return r.GetComponent<TextMeshProUGUI>();
        }

        private static TextMeshProUGUI Label(RectTransform r, float size, Color color, TextAlignmentOptions align)
        {
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed");
            t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
            t.margin = Vector4.zero; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h, out bool made)
        {
            var go = SceneFirst.Child(parent, name, out made, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            if (made) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); }
            return r;
        }
    }
}
