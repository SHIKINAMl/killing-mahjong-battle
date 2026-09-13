using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    /// <summary>
    /// ボルテージの表示。ユーザーの指示（2026-09-08）。
    ///
    /// 段数を四角の点で、倍率を数字で出す。**河ごとに1つ**作って、その人のそばに置く。
    /// 段数の中身は <see cref="VoltageSystem"/> が持っていて、ここは描くだけ。
    ///
    /// **シーンには置かず実行時に組み立てる。** 対局シーンが2つ（UIテストシーン /
    /// OpeningScene）あり、河も自分・相手で2つずつあるので、シーンに持たせると
    /// 4か所を直すことになる（AGENTS.md §2、RiverUI の MaxPerRow と同じ理由）。
    /// </summary>
    public class VoltageUI : MonoBehaviour
    {
        // ---- 調整値（シーンではなくここを触る）----

        private const int PipCount = 4;
        private const float PipSize = 13f;
        private const float PipGap = 4f;

        /// <summary>四角を並べた全体の幅。倍率をその中央に乗せるのに使う。</summary>
        private const float PipRowWidth = PipCount * PipSize + (PipCount - 1) * PipGap;

        /// <summary>
        /// 置き場所。画面中央からのずれ。**「YOUR TURN / ENEMY TURN」が出ていた場所**に置く
        /// （ユーザーの指示 2026-09-08。あの文字は出さないことにした）。
        /// 元の文字は (-280, 125) にあった。上が相手、下が自分。
        /// </summary>
        /// <summary>
        /// **ドラ表示（画面左の中ほど）を避けた位置。**
        /// 元の「YOUR TURN」の高さ(125)に置いたら、自分側のゲージがドラと重なった。
        /// ドラより下の、壁と卓のあいだの暗い帯が空いている。
        /// </summary>
        private const float PosX = -280f;
        private const float EnemyPosY = 20f;
        private const float SelfPosY = -30f;

        private static readonly Color PipOn = new Color32(255, 150, 40, 255);
        private static readonly Color PipOff = new Color32(70, 40, 40, 200);
        private static readonly Color PipBroken = new Color32(90, 90, 95, 200);
        private static readonly Color TextOn = new Color32(255, 190, 90, 255);
        private static readonly Color TextBroken = new Color32(140, 140, 145, 255);

        private bool _isEnemy;
        private Image[] _pips;
        private VoltageFlame[] _flames;
        private TextMeshProUGUI _multiplierText;

        /// <summary>
        /// 画面左の空きに1つ作る。すでにあれば作り直さない。
        ///
        /// **河ではなく一番上の Canvas にぶら下げる。** 河は手牌を選んでいる間は消えるので、
        /// 河の下に置くとゲージまで一緒に消える。左の空きに出しておけば常に見える。
        ///
        /// 自分と相手で名前を分ける。同じ親に2つ並ぶので、名前が同じだと
        /// 「すでにある」と誤判定して1つしか作られない。
        /// </summary>
        /// <summary>
        /// 自分と相手のぶんをまとめて作る。対局画面の用意ができた時点で呼ぶ。
        ///
        /// **専用の Canvas を自分で持つ。** 既存のキャンバスにぶら下げると、
        /// 親が場面ごとに消えるのに巻き込まれる（河のキャンバスに付けたら、
        /// 手牌を選んでいる間ゲージまで消えた）。RoomScreenUI などと同じやり方。
        /// </summary>
        public static void EnsureCreated()
        {
            var existing = GameObject.Find(CanvasName);
            RectTransform parent;

            if (existing != null)
            {
                parent = existing.transform as RectTransform;
            }
            else
            {
                var canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler));
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = UISortingOrders.VoltageGauge;

                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                // このゲーム本来の画面比。他の実行時UIと揃えてある。
                scaler.referenceResolution = new Vector2(800f, 600f);
                scaler.matchWidthOrHeight = 0.5f;

                parent = (RectTransform)canvasObject.transform;
            }

            Attach(parent, isEnemy: true);
            Attach(parent, isEnemy: false);
        }

        private const string CanvasName = "VoltageCanvas";

        private static VoltageUI Attach(RectTransform parent, bool isEnemy)
        {
            if (parent == null) return null;

            string name = isEnemy ? "VoltageUI_Enemy" : "VoltageUI_Self";
            var existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<VoltageUI>();

            var root = new GameObject(name, typeof(RectTransform), typeof(VoltageUI));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(PosX, isEnemy ? EnemyPosY : SelfPosY);
            rect.sizeDelta = new Vector2(PipRowWidth, 40f);

            var ui = root.GetComponent<VoltageUI>();
            ui.Build(isEnemy);
            return ui;
        }

        private void Build(bool isEnemy)
        {
            _isEnemy = isEnemy;
            _pips = new Image[PipCount];
            _flames = new VoltageFlame[PipCount];

            for (int i = 0; i < PipCount; i++)
            {
                var pip = new GameObject("Pip" + i, typeof(RectTransform), typeof(Image));
                var pipRect = (RectTransform)pip.transform;
                pipRect.SetParent(transform, false);
                pipRect.anchorMin = new Vector2(0f, 0.5f);
                pipRect.anchorMax = new Vector2(0f, 0.5f);
                pipRect.pivot = new Vector2(0f, 0.5f);
                pipRect.anchoredPosition = new Vector2(i * (PipSize + PipGap), 0f);
                pipRect.sizeDelta = new Vector2(PipSize, PipSize);

                var image = pip.GetComponent<Image>();
                image.raycastTarget = false;   // 牌のクリック判定を吸わない
                _pips[i] = image;

                // 点いた四角の上で炎を揺らす（2026-09-13 の指示）。
                // 絵は使わず丸を積んで作っている。中身は VoltageFlame.cs。
                _flames[i] = VoltageFlame.Attach(pipRect);
            }

            // 倍率は四角の上に、四角の並びの中央に乗せる（ユーザーの指示 2026-09-08）。
            var label = new GameObject("Multiplier", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(transform, false);
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            // **炎のぶん上へ逃がす（2026-09-13）。** 四角の真上は炎が使うので、
            // 元の +3 では数字と炎が重なって読めなくなった。
            labelRect.anchoredPosition = new Vector2(PipRowWidth * 0.5f, PipSize * 0.5f + 20f);
            labelRect.sizeDelta = new Vector2(80f, 22f);

            _multiplierText = label.GetComponent<TextMeshProUGUI>();
            _multiplierText.font = BorrowJapaneseFont();
            _multiplierText.fontSize = 16f;
            _multiplierText.alignment = TextAlignmentOptions.Center;
            _multiplierText.raycastTarget = false;

            Refresh();
        }

        private void OnEnable()
        {
            VoltageSystem.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            VoltageSystem.Changed -= Refresh;
        }

        private void Refresh()
        {
            if (_pips == null || _multiplierText == null) return;

            int level = VoltageSystem.GetLevel(_isEnemy);
            bool broken = VoltageSystem.IsBroken(_isEnemy);

            for (int i = 0; i < _pips.Length; i++)
            {
                if (_pips[i] == null) continue;
                bool lit = i < level;
                _pips[i].color = lit ? (broken ? PipBroken : PipOn) : PipOff;

                // **炎は点いた四角にだけ。** 段が上がるほど、どの炎も激しくなる。
                // 「本数が増える」と「1本ずつ強くなる」の両方で段の差を出している。
                if (_flames[i] != null) _flames[i].SetLevel(lit ? level : 0, broken);
            }

            // 等倍のときは数字を出さない。常に「×1.0」が並んでいると、
            // 段が乗ったときの変化に気づきにくい。
            _multiplierText.text = level > 0
                ? "×" + VoltageSystem.GetMultiplier(_isEnemy).ToString("0.0")
                : string.Empty;
            _multiplierText.color = broken ? TextBroken : TextOn;
        }

        /// <summary>
        /// 画面にある文字から日本語の使えるフォントを借りる。
        /// `PixelMplus10_DynamicFixed` は同名のアセットが複数あって直接引くと当たりが不定なため
        /// （RoomScreenUI / TutorialManager.Intro と同じやり方）。
        /// </summary>
        private static TMP_FontAsset BorrowJapaneseFont()
        {
            var labels = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var label in labels)
            {
                if (label != null && label.font != null) return label.font;
            }

            return TMP_Settings.defaultFontAsset;
        }
    }
}
