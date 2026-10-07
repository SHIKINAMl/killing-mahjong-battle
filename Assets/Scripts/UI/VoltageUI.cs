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

        /// <summary>
        /// 区画の数。**左から順に埋まる**（2026-09-13 のプランナー指定）。
        /// </summary>
        private const int PipCount = 4;

        // 帯の寸法。**プランナーの参考画像を実測して決めた。**
        // 参考画像は 402x304 で、帯は 幅40・高さ4・区画10px・隙間4px だった。
        // このゲームは 800x600 なので、およそ2倍にしてある。
        private const float PipSize = 20f;     // 区画1つの横幅
        private const float PipHeight = 8f;    // 帯の高さ
        private const float PipGap = 8f;       // 区画の隙間

        /// <summary>帯全体の幅。倍率をその上に乗せるのに使う。</summary>
        private const float PipRowWidth = PipCount * PipSize + (PipCount - 1) * PipGap;

        // ---- 下段：次の段までを刻む行（2026-09-29 のユーザー指示）----
        //
        // **上段（段の数）はそのまま上へずらし、その下にもう一行足す。**
        // 下段は「いまの段から次の段へ上がるのに要るポイント」を、その数だけ
        // 区画に割ったもの。1ポイント溜まるごとに1区画が埋まり、**埋まり切ると
        // 上段の区画が1つ点いて、下段は次の段ぶんの刻みに作り直される。**
        //
        // 以前は上段の区画の中を半透明で横に伸ばして進み具合を見せていたが、
        // 不透明度45%・幅10pxほどの薄い影にしかならず、**溜まっているのに
        // 溜まって見えなかった**（2026-09-29 に実機で測った。計算自体は合っていた）。

        /// <summary>段ごとに要るポイント数。**仕様書の 0/2/5/9/14 の差分。**</summary>
        private static readonly int[] PointsPerTier = { 2, 3, 4, 5 };

        /// <summary>下段の区画の高さ。上段より薄くして、主役を上段に残す。</summary>
        private const float ChargeHeight = 6f;

        /// <summary>下段の区画どうしの隙間。</summary>
        private const float ChargeGap = 3f;

        /// <summary>上段と下段の間隔。</summary>
        private const float RowGap = 5f;

        /// <summary>下段の区画は**常に上段と同じ幅に収める**ので、数で割って決める。</summary>
        private static float ChargePipWidth(int count)
        {
            if (count <= 0) return 0f;
            return (PipRowWidth - (count - 1) * ChargeGap) / count;
        }

        /// <summary>倍率の文字の大きさ。参考画像では帯の5倍ほどの高さがあった。</summary>
        private const float MultiplierFontSize = 30f;

        /// <summary>
        /// 置き場所。画面中央からのずれ。**敵と自分で左右に分ける。**
        ///
        /// 自分（左）… 元は「YOUR TURN / ENEMY TURN」が出ていた場所（2026-09-08 の指示。
        /// あの文字は出さないことにした）。元の文字は (-280, 125) にあったが、その高さだと
        /// ドラ表示（画面左の中ほど）と重なったので、ドラより下の、壁と卓のあいだの
        /// 暗い帯へ下ろしてある。
        ///
        /// 敵（右）… **セリフの吹き出しの真下**（2026-09-27 の指示）。
        /// プランナーの以前の取り決めは「自分は右・敵は左」だったが、
        /// 吹き出しの下に敵を置く方を優先すると確認を取った。
        ///
        /// **ここは整数で持つこと。** 実機で合わせたときの値は (148.92, -67.86) だったが、
        /// 画面が 800x600 で CanvasScaler の基準も 800x600 なので UI は等倍。
        /// 小数を入れると四角の縁が半画素にかかって、ドット絵の中でそこだけ滲む。
        ///
        /// なお吹き出し（DialoguePanel）は FloatingAnimator で上下に浮いていて、
        /// 下端が 314〜324 の間を往復する。倍率ラベルの上端は 308 で固定なので、
        /// **すき間は 6〜16px の間で変わる。** 16px はいちばん離れた瞬間の値。
        /// </summary>
        private const float SelfPosX = -280f;
        private const float SelfPosY = -120f;
        private const float EnemyPosX = 149f;
        private const float EnemyPosY = -68f;

        // 点いた四角の色は段ごとに変わるので、ここには持たない。
        // 配っているのは VoltageFlame.PipColorFor（2026-09-13）。
        private static readonly Color PipOff = new Color32(70, 40, 40, 200);
        private static readonly Color PipBroken = new Color32(90, 90, 95, 200);
        private static readonly Color TextOn = new Color32(255, 190, 90, 255);
        private static readonly Color TextBroken = new Color32(140, 140, 145, 255);

        /// <summary>上段の高さ。下段を置くぶん上へ寄せる。</summary>
        private const float UpperRowY = (ChargeHeight + RowGap) * 0.5f;

        /// <summary>下段の高さ。</summary>
        private const float LowerRowY = -(PipHeight + RowGap) * 0.5f;

        private bool _isEnemy;
        private RectTransform _chargeRow;
        private Image[] _chargePips;
        private int _chargeTier = -1;
        private Image[] _pips;
        private Image[] _pipFills;
        private VoltageFlame[] _flames;

        /// <summary>点いたブロックの中で脈打つ心臓（2026-09-30 の指示）。中身は VoltageHeart.cs。</summary>
        private VoltageHeart[] _hearts;
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
        /// <summary>
        /// 作ったキャンバスを覚えておく。
        ///
        /// **`GameObject.Find` で探し直してはいけない（2026-09-14 に踏んだ）。**
        /// あれは**非アクティブな物を見つけられない**ので、一度伏せると
        /// 二度と見つからず、出し直せないうえに次の `EnsureCreated` で
        /// 2枚目が作られてしまう。
        /// </summary>
        private static GameObject _canvasObject;

        public static void EnsureCreated()
        {
            var existing = _canvasObject != null ? _canvasObject : GameObject.Find(CanvasName);
            RectTransform parent;

            if (existing != null)
            {
                _canvasObject = existing;
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

                _canvasObject = canvasObject;
                parent = (RectTransform)canvasObject.transform;
            }

            Attach(parent, isEnemy: true);
            Attach(parent, isEnemy: false);
        }

        private const string CanvasName = "VoltageCanvas";

        /// <summary>
        /// ゲージ全体の出し入れ（2026-09-14 のユーザー指示）。
        ///
        /// **打牌フェイズのときだけ出す。** 牌を選んでいる間や賭け金を決めている間は、
        /// ボルテージは動かないので出していても意味が無く、画面が混むだけだった。
        ///
        /// **まだ作られていないときは何もしない。** ここで作ってしまうと、
        /// 出さないはずの場面で作られて1フレーム映り込む。
        /// </summary>
        public static void SetCanvasVisible(bool visible)
        {
            if (_canvasObject == null) return;
            if (_canvasObject.activeSelf == visible) return;
            _canvasObject.SetActive(visible);
        }

        /// <summary>
        /// ゲージの置き場所。チュートリアルで「これがボルテージ」と指すのに使う（2026-10-08）。
        /// まだ作られていなければ null。
        /// </summary>
        public static RectTransform GetGaugeRect(bool isEnemy)
        {
            if (_canvasObject == null) return null;
            var found = _canvasObject.transform.Find(isEnemy ? "VoltageUI_Enemy" : "VoltageUI_Self");
            return found as RectTransform;
        }

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
            rect.anchoredPosition = isEnemy
                ? new Vector2(EnemyPosX, EnemyPosY)
                : new Vector2(SelfPosX, SelfPosY);
            rect.sizeDelta = new Vector2(PipRowWidth, 40f);

            var ui = root.GetComponent<VoltageUI>();
            ui.Build(isEnemy);
            return ui;
        }

        private void Build(bool isEnemy)
        {
            _isEnemy = isEnemy;
            _pips = new Image[PipCount];
            _pipFills = new Image[PipCount];
            _flames = new VoltageFlame[PipCount];
            _hearts = new VoltageHeart[PipCount];

            for (int i = 0; i < PipCount; i++)
            {
                var pip = new GameObject("Pip" + i, typeof(RectTransform), typeof(Image));
                var pipRect = (RectTransform)pip.transform;
                pipRect.SetParent(transform, false);
                pipRect.anchorMin = new Vector2(0f, 0.5f);
                pipRect.anchorMax = new Vector2(0f, 0.5f);
                pipRect.pivot = new Vector2(0f, 0.5f);
                // **上段は上へずらす。** 下に刻みの行を置くため（2026-09-29）
                pipRect.anchoredPosition = new Vector2(i * (PipSize + PipGap), UpperRowY);
                pipRect.sizeDelta = new Vector2(PipSize, PipHeight);

                var image = pip.GetComponent<Image>();
                image.raycastTarget = false;   // 牌のクリック判定を吸わない
                _pips[i] = image;

                // **次の段までの途中を、区画の中に左から塗る（2026-09-13）。**
                // 仕様書どおりだと、次の段まで2枚・3枚・4枚・5枚と要る。
                // 区画が点くまで何も動かないと、ゲージが壊れて見える。
                var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                var fillRect = (RectTransform)fill.transform;
                fillRect.SetParent(pipRect, false);
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.pivot = new Vector2(0f, 0.5f);
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = new Vector2(0f, 0f);

                var fillImage = fill.GetComponent<Image>();
                fillImage.raycastTarget = false;
                _pipFills[i] = fillImage;

                // 点いた四角の上で炎を揺らす（2026-09-13 の指示）。
                // 絵は使わず丸を積んで作っている。中身は VoltageFlame.cs。
                _flames[i] = VoltageFlame.Attach(pipRect);

                // **炎より後に付けること。** 兄弟の並び順がそのまま描く順になるので、
                // 先に付けると心臓が炎の粒の下に潜って見えなくなる
                _hearts[i] = VoltageHeart.Attach(pipRect);
            }

            // 下段の入れ物。中身は段が変わるたびに作り直す
            var chargeRow = new GameObject("ChargeRow", typeof(RectTransform));
            _chargeRow = (RectTransform)chargeRow.transform;
            _chargeRow.SetParent(transform, false);
            _chargeRow.anchorMin = new Vector2(0f, 0.5f);
            _chargeRow.anchorMax = new Vector2(0f, 0.5f);
            _chargeRow.pivot = new Vector2(0f, 0.5f);
            _chargeRow.anchoredPosition = new Vector2(0f, LowerRowY);
            _chargeRow.sizeDelta = new Vector2(PipRowWidth, ChargeHeight);

            // 倍率は四角の上に、四角の並びの中央に乗せる（ユーザーの指示 2026-09-08）。
            var label = new GameObject("Multiplier", typeof(RectTransform), typeof(TextMeshProUGUI));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(transform, false);
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            // **帯の真上に大きく出す（参考画像どおり）。**
            // 炎は帯の上で揺れるので、そのぶんの高さを空けてから文字を置く。
            labelRect.anchoredPosition = new Vector2(PipRowWidth * 0.5f, PipHeight * 0.5f + 36f);
            labelRect.sizeDelta = new Vector2(PipRowWidth + 40f, MultiplierFontSize + 6f);

            _multiplierText = label.GetComponent<TextMeshProUGUI>();
            _multiplierText.font = BorrowJapaneseFont();
            _multiplierText.fontSize = MultiplierFontSize;
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

            // 次の段まであとどれくらいか。0〜1。最大段まで行っていたら 0。
            int points = VoltageSystem.GetPoints(_isEnemy);
            int from = VoltageSystem.GetPointsAtCurrentLevel(_isEnemy);
            int to = VoltageSystem.GetPointsForNextLevel(_isEnemy);
            // progress は下段の区画の数で見せるので、ここでは使わない

            for (int i = 0; i < _pips.Length; i++)
            {
                if (_pips[i] == null) continue;
                bool lit = i < level;

                // **点いた四角は段の色で塗る（2026-09-13）。**
                // 炎より四角のほうが大きいので、そちらが段の色になっているほうが早く読める。
                // 色は炎と同じ表から配ってもらう（2箇所に書かないため）。
                _pips[i].color = lit
                    ? (broken ? PipBroken : VoltageFlame.PipColorFor(level))
                    : PipOff;

                // **炎は点いた四角にだけ。** 段が上がるほど、どの炎も激しくなる。
                // 「本数が増える」と「1本ずつ強くなる」の両方で段の差を出している。
                if (_flames[i] != null) _flames[i].SetLevel(lit ? level : 0, broken);
                if (_hearts != null && _hearts[i] != null) _hearts[i].SetLevel(lit ? level : 0, broken);

                // **上段の中は塗らない（2026-09-29）。** 途中経過は下段が受け持つ。
                // ここで半透明を横に伸ばしていたが、薄すぎて見えていなかった。
                if (_pipFills[i] != null)
                    _pipFills[i].rectTransform.sizeDelta = new Vector2(0f, 0f);
            }

            RefreshChargeRow(level, broken, points, from, to);

            // **0段でも出す（2026-09-13、参考画像どおり）。**
            // 以前は等倍のとき空にしていたが、参考画像は倍率を常に見せる作りで、
            // 帯と文字が揃っているほうが「ここが何の表示か」が分かる。
            _multiplierText.text = "×" + VoltageSystem.GetMultiplier(_isEnemy).ToString("0.0");
            _multiplierText.color = broken ? TextBroken : TextOn;
        }

        /// <summary>
        /// 下段（次の段までの刻み）を描き直す。
        ///
        /// **段が変わったときだけ作り直す。** 要る区画の数が段ごとに違う
        /// （2→3→4→5）ので、毎フレーム作ると無駄に生成し続けることになる。
        /// 最大段まで行ったら下段は空にする。もう溜めるものが無い。
        /// </summary>
        private void RefreshChargeRow(int level, bool broken, int points, int from, int to)
        {
            if (_chargeRow == null) return;

            int need = (!broken && level < PointsPerTier.Length) ? PointsPerTier[level] : 0;

            if (_chargeTier != level || _chargePips == null || _chargePips.Length != need)
            {
                for (int i = _chargeRow.childCount - 1; i >= 0; i--)
                    Destroy(_chargeRow.GetChild(i).gameObject);

                _chargePips = new Image[need];
                float w = ChargePipWidth(need);
                for (int i = 0; i < need; i++)
                {
                    var go = new GameObject("Charge" + i, typeof(RectTransform), typeof(Image));
                    var rt = (RectTransform)go.transform;
                    rt.SetParent(_chargeRow, false);
                    rt.anchorMin = new Vector2(0f, 0.5f);
                    rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(i * (w + ChargeGap), 0f);
                    rt.sizeDelta = new Vector2(w, ChargeHeight);

                    var img = go.GetComponent<Image>();
                    img.raycastTarget = false;   // 牌のクリック判定を吸わない
                    _chargePips[i] = img;
                }
                _chargeTier = level;
            }

            if (_chargePips == null) return;

            // いまの段で何個ぶん溜まったか
            int filled = Mathf.Clamp(points - from, 0, _chargePips.Length);
            var onColor = VoltageFlame.PipColorFor(level + 1);
            for (int i = 0; i < _chargePips.Length; i++)
            {
                if (_chargePips[i] == null) continue;
                _chargePips[i].color = i < filled ? onColor : PipOff;
            }
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
