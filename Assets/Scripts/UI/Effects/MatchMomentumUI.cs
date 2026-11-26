using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 対局が終わったときに、その対局の血の増減をふり返る演出（戦況）。
    ///
    /// ---- 2人の体の中の血（2026-11-26）----
    ///
    /// ユーザー「人を2体用意して、その2人の体の中の血の増減する様子を見せてほしい。負けたほうは最後倒れる」。
    /// それまでは紙に線グラフを描いていた（11/25 紙に描く → 11/26 記録計のペン）が、どちらも「AIくさい」と言われた。
    /// - 紙（役一覧と同じ）の上に、左に自分・右に相手の人型を立てる
    /// - 人型は、賭け金を決めるスマホの画面の人型（Resources/人型メーター.png）。体の中が血の入れ物になっている絵で、
    ///   目盛りも付いている。スマホの画面の端で左腕と左足が切れているので、**右半分を左右に返して1人分にする**
    ///   （元の絵は変えない。読んで並べ直すだけ）。目盛りは元の位置のまま重ねる
    /// - 局ごとに、2人の体の血の高さが新しい値へ動く。ロンの局はゆっくりで、上がった側の頭の上に「ロン」、
    ///   抜かれた側は体が震える
    /// - 最後に、血が少ないほう（負けたほう）が倒れる。同じなら誰も倒れない
    /// シーンの Backdrop（黒い四角）は残してあり、出すときに隠す。OpeningScene にも同じ物があるため
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class MatchMomentumUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField, Tooltip("人型を立てる場所")]
        private RectTransform graphContainer;
        [SerializeField, Tooltip("演出テキスト（例: 圧倒的優勢）")]
        private TextMeshProUGUI statusText;
        private CanvasGroup canvasGroup;

        [Header("Settings")]
        [SerializeField] private float displayDuration = 2.0f;
        [SerializeField] private float fadeDuration = 0.5f;

        private readonly List<GameObject> spawnedUIElements = new List<GameObject>();
        public bool IsShowing { get; private set; }

        /// <summary>
        /// 絵の1ドットの大きさ（この Canvas の単位）。右上の役一覧の紙と同じ
        /// （800x600 で 2。この Canvas は 960x720 の画面の画素そのままなので 2.4）。
        /// </summary>
        private const float ArtPixel = 2.4f;

        // ---- 紙（2026-11-25。右上の役一覧と同じ紙）----

        /// <summary>UI.png は読み込み後 1024x704 で、絵の1ドットが 8 画素。</summary>
        private const float PaperTexelsPerDot = 8f;

        /// <summary>
        /// UI.png の中の、紙の下半分（左下が原点の画素）。絵のドットで 左 13〜右 111・上から 45〜81。
        /// 枠は左右下とも 10 ドットぶんを伸ばさない縁にする（角の黄色い鋲も縁に入る）。
        /// </summary>
        private static readonly Rect PaperHalfRect = new Rect(104f, 56f, 784f, 288f);
        private static readonly Vector4 PaperHalfBorder = new Vector4(80f, 80f, 80f, 0f); // 左・下・右・上

        private static Sprite paperHalf;
        private RectTransform paper;

        private static readonly Color32 PlayerInk = new Color32(38, 72, 168, 255);
        private static readonly Color32 EnemyInk = new Color32(176, 32, 44, 255);

        // ---- 人型 ----

        /// <summary>
        /// 人型メーター.png（1600 四方）は 10 画素で絵の1ドット。人型は ドットで 上 23〜下 99、
        /// 体の真ん中は 61 列と 62 列の境目。右半分（62〜94 列）が切れずに残っている。
        /// </summary>
        private const int FigureDotsPerSide = 160;
        private const int FigureTop = 23, FigureBottom = 99;
        private const int FigureAxis = 62, FigureRight = 94;
        /// <summary>目盛り（黒）を元の絵から写す範囲（スマホの画面の左端より右）。</summary>
        private const int FigureScreenLeft = 41;

        private static readonly Color32 FigureSkin = new Color32(238, 195, 154, 255);
        private static readonly Color32 FigureOutline = new Color32(29, 13, 22, 255); // 紙の上で輪郭が消えないように
        private static readonly Color32 FigureEmpty = new Color32(214, 204, 192, 255); // 血が抜けた所
        private static readonly Color32 FigureBlood = new Color32(176, 32, 44, 255);
        private static readonly Color32 FigureBloodTop = new Color32(214, 74, 82, 255); // 血の面（いちばん上の1段）

        private static Sprite figureBody, figureTicks;
        private static Texture2D figureBlood;
        private static int figureWidth, figureHeight, bloodLowRow, bloodHighRow;

        /// <summary>人型の足もとを、紙（graphContainer）の中のどこに置くか。</summary>
        private const float FigureSpacing = 190f;
        private const float FigureFloor = 34f; // 下の縁から足もとまで（下に名前を書く分）

        // ---- 動き ----

        /// <summary>1局ぶんの血が動く時間。ロンの局は長い（2026-10-15 の要望「ロンで少し溜めが欲しい」を引き継ぐ）。</summary>
        private const float StepSeconds = 0.22f;
        private const float RonStepSeconds = 0.6f;
        private const float RonPopSeconds = 0.16f;
        /// <summary>抜かれた側の体の震え（紙のドット）。</summary>
        private const float ShakeDots = 1.5f;

        /// <summary>倒れる：傾き始めてから床に着くまで、そのあと少し跳ね返って落ち着くまで。</summary>
        private const float FallSeconds = 0.34f;
        private const float BounceSeconds = 0.16f;
        private const float BounceDegrees = 7f;

        /// <summary>
        /// 履歴のどの点がロンだったか。流局や賭け金での増減と見分けるために、ロンの点にだけ付ける。
        /// Index は HP 履歴の添字（その点でロンの清算が済んでいる）。Rank は 満貫・跳満…役満。
        /// </summary>
        public struct RonMark
        {
            public int Index;
            public bool IsLocalWin;
            public string Rank;
        }

        /// <summary>人型1体ぶん。</summary>
        private sealed class Figure
        {
            public RectTransform Root;   // 足もとが原点。倒れるときはここを回す
            public RectTransform Shaker; // 震えはこちら（Root の回転とは別に動かす）
            public RawImage Blood;
            public RectTransform BloodRect;
            public TextMeshProUGUI Ron;
            public float Level;          // 0〜1
        }

        private Figure player, enemy;

        /// <summary>
        /// 「ロン」の字の大きさをロンの強さで変える（2026-10-15 のユーザーの要望）。
        /// 本編のロンは満貫以上なので、満貫を基準の 1 にする。
        /// </summary>
        private static float RankScale(string rank)
        {
            switch (rank)
            {
                case "ダブル役満": return 1.9f;
                case "役満": return 1.7f;
                case "三倍満": return 1.45f;
                case "倍満": return 1.3f;
                case "跳満": return 1.15f;
                default: return 1f;
            }
        }

        private void Awake()
        {
            EnsureCanvasGroup();
            canvasGroup.alpha = 0f;
        }

        private void Start()
        {
            // 初期表示を隠すのは Awake ではなく Start で行う。
            // Awake で SetActive(false) すると、シーン上で非アクティブに置かれていた場合に
            // ShowMomentum() の SetActive(true) が Awake を誘発し、その場で自分を非アクティブへ
            // 戻してしまうため、直後の StartCoroutine が "game object is inactive" で失敗する。
            if (!IsShowing) gameObject.SetActive(false);
        }

        private void EnsureCanvasGroup()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

            // 戦況は見せるだけの演出なので、裏のUIの入力を奪わないようにする
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        /// <summary>
        /// HPの推移を受け取り、2人の体の血の増減を見せる
        /// </summary>
        public void ShowMomentum(List<int> playerHpHistory, List<int> enemyHpHistory, IList<RonMark> ronMarks = null)
        {
            // シーン上で非アクティブに置かれていると Awake が未実行なので、ここで取得しておく
            EnsureCanvasGroup();
            StopAllCoroutines();
            canvasGroup.alpha = 0f;

            IsShowing = true;
            gameObject.SetActive(true);
            ClearGraph();

            StartCoroutine(DisplayRoutine(playerHpHistory, enemyHpHistory, ronMarks));
        }

        private IEnumerator DisplayRoutine(List<int> playerHpHistory, List<int> enemyHpHistory, IList<RonMark> ronMarks)
        {
            bool ready = playerHpHistory != null && enemyHpHistory != null &&
                         playerHpHistory.Count > 0 && enemyHpHistory.Count > 0 && BuildFigures();
            int maxHp = 1;
            if (ready)
            {
                foreach (int hp in playerHpHistory) maxHp = Mathf.Max(maxHp, hp);
                foreach (int hp in enemyHpHistory) maxHp = Mathf.Max(maxHp, hp);
                // 始まりの血の量で立っている
                SetLevel(player, playerHpHistory[0] / (float)maxHp);
                SetLevel(enemy, enemyHpHistory[0] / (float)maxHp);
            }
            yield return StartCoroutine(FadeRoutine(1f));

            if (ready)
            {
                yield return StartCoroutine(PlayHistory(playerHpHistory, enemyHpHistory, ronMarks, maxHp));
                int lastP = playerHpHistory[playerHpHistory.Count - 1];
                int lastE = enemyHpHistory[enemyHpHistory.Count - 1];
                if (lastP != lastE) yield return StartCoroutine(Collapse(lastP < lastE ? player : enemy, lastP < lastE));
            }

            // テキスト演出の決定
            if (ready) UpdateStatusText(playerHpHistory, enemyHpHistory);

            // 指定時間表示
            yield return new WaitForSeconds(displayDuration);

            // フェードアウト
            yield return StartCoroutine(FadeRoutine(0f));

            // 片付け & 非表示
            ClearGraph();
            IsShowing = false;
            gameObject.SetActive(false);
        }

        /// <summary>局ごとに、2人の血を新しい値へ動かす。</summary>
        private IEnumerator PlayHistory(List<int> pHistory, List<int> eHistory, IList<RonMark> ronMarks, int maxHp)
        {
            int count = Mathf.Max(pHistory.Count, eHistory.Count);
            for (int i = 1; i < count; i++)
            {
                float pTo = pHistory[Mathf.Min(i, pHistory.Count - 1)] / (float)maxHp;
                float eTo = eHistory[Mathf.Min(i, eHistory.Count - 1)] / (float)maxHp;
                RonMark? ron = null;
                if (ronMarks != null)
                    foreach (var mark in ronMarks) if (mark.Index == i) ron = mark;

                float seconds = ron.HasValue ? RonStepSeconds : StepSeconds;
                Figure winner = null, loser = null;
                if (ron.HasValue)
                {
                    winner = ron.Value.IsLocalWin ? player : enemy;
                    loser = ron.Value.IsLocalWin ? enemy : player;
                    StartCoroutine(PopRon(winner, RankScale(ron.Value.Rank)));
                }

                float pFrom = player.Level, eFrom = enemy.Level;
                float t = 0f;
                while (t < seconds)
                {
                    float k = PresentationTiming.Sample(t) / seconds;
                    // 勢いよく動いて、最後はゆっくり止まる
                    float eased = 1f - (1f - k) * (1f - k);
                    SetLevel(player, Mathf.Lerp(pFrom, pTo, eased));
                    SetLevel(enemy, Mathf.Lerp(eFrom, eTo, eased));
                    if (loser != null)
                    {
                        // 血が抜かれているあいだ、抜かれた側が震える（ドット単位で左右に）
                        float amount = (1f - k) * ShakeDots * ArtPixel;
                        loser.Shaker.anchoredPosition = new Vector2(
                            Mathf.Round(Mathf.Sin(t * 70f) * amount / ArtPixel) * ArtPixel, 0f);
                    }
                    yield return null;
                    t += Time.deltaTime;
                }
                SetLevel(player, pTo);
                SetLevel(enemy, eTo);
                if (loser != null) loser.Shaker.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>上がった側の頭の上に「ロン」を出す。小さく出て一度ふくらみ、強さの大きさに落ち着く。</summary>
        private IEnumerator PopRon(Figure figure, float scale)
        {
            if (figure == null || figure.Ron == null) yield break;
            // 前のロンの字は消して、今のロンだけを出す
            foreach (var other in new[] { player, enemy })
                if (other != null && other.Ron != null) other.Ron.gameObject.SetActive(false);
            var rect = figure.Ron.rectTransform;
            figure.Ron.gameObject.SetActive(true);
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio != null)
            {
                // 強いロンほど低く太い音にする（ダブル役満で約1オクターブ下）。
                // 音は Freesound（CC0）のポップ音2つを重ねたもの（2026-10-19）
                audio.PlayStingerPitched("se_ron_pop", 1f / scale,
                    KillingMahjong.Managers.AudioManager.PitchJitter.None);
            }
            float t = 0f;
            while (t < RonPopSeconds && rect != null)
            {
                float k = PresentationTiming.Sample(t) / RonPopSeconds;
                // 0 → 1.25倍 → 1倍
                float s = k < 0.6f ? Mathf.Lerp(0.3f, 1.25f, k / 0.6f) : Mathf.Lerp(1.25f, 1f, (k - 0.6f) / 0.4f);
                rect.localScale = Vector3.one * (s * scale);
                yield return null;
                t += Time.deltaTime;
            }
            if (rect != null) rect.localScale = Vector3.one * scale;
        }

        /// <summary>
        /// 負けたほうが倒れる。足もとを軸に、だんだん速く傾いて床に着き、少し跳ね返って落ち着く。
        /// 倒れる向きは紙の内側（自分は右へ、相手は左へ）。外へ倒すと頭が紙からはみ出る。
        /// 2人の間は、倒れた頭が立っているほうに届かない幅にしてある（FigureSpacing）。
        /// </summary>
        private IEnumerator Collapse(Figure figure, bool isPlayer)
        {
            if (figure == null) yield break;
            foreach (var other in new[] { player, enemy })
                if (other != null && other.Ron != null) other.Ron.gameObject.SetActive(false);
            float side = isPlayer ? -1f : 1f; // z の回転：負で右へ倒れる
            // 倒れる側の足もとの角を軸にする（足の真ん中を軸にすると、腕が床の下へもぐる）。
            // 軸を変えても、立っている位置は変えない
            var root = figure.Root;
            float halfWidth = root.sizeDelta.x * 0.5f;
            root.pivot = new Vector2(isPlayer ? 1f : 0f, 0f);
            root.anchoredPosition += new Vector2(isPlayer ? halfWidth : -halfWidth, 0f);
            float t = 0f;
            while (t < FallSeconds)
            {
                float k = PresentationTiming.Sample(t) / FallSeconds;
                figure.Root.localEulerAngles = new Vector3(0f, 0f, side * 90f * k * k);
                yield return null;
                t += Time.deltaTime;
            }
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio != null)
                audio.PlayStingerPitched("se_hit", 0.8f, KillingMahjong.Managers.AudioManager.PitchJitter.VerySmall, 0.7f);
            t = 0f;
            while (t < BounceSeconds)
            {
                float k = PresentationTiming.Sample(t) / BounceSeconds;
                float angle = 90f - BounceDegrees * Mathf.Sin(k * Mathf.PI);
                figure.Root.localEulerAngles = new Vector3(0f, 0f, side * angle);
                yield return null;
                t += Time.deltaTime;
            }
            figure.Root.localEulerAngles = new Vector3(0f, 0f, side * 90f);
        }

        /// <summary>血の高さ（0〜1）。体の中の、足の先から頭のてっぺんまでを、1ドットの段ごとに満たす。</summary>
        private static void SetLevel(Figure figure, float level)
        {
            if (figure == null) return;
            figure.Level = Mathf.Clamp01(level);
            int span = bloodHighRow - bloodLowRow + 1;
            int rows = Mathf.RoundToInt(figure.Level * span);
            // 何も無いときも、血の段は1つも出さない
            float visible = rows <= 0 ? 0f : (bloodLowRow + rows) / (float)figureHeight;
            figure.BloodRect.anchorMax = new Vector2(1f, visible);
            figure.BloodRect.offsetMax = Vector2.zero;
            figure.Blood.uvRect = new Rect(0f, 0f, 1f, visible);
            figure.Blood.enabled = rows > 0;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            IsShowing = false;
            ClearGraph();
        }

        // ---- 人型を作る ----

        private bool BuildFigures()
        {
            if (graphContainer == null) return false;
            Canvas.ForceUpdateCanvases();
            EnsurePaper();
            if (!EnsureFigureArt()) return false;
            float height = graphContainer.rect.height;
            float floor = -height * 0.5f + FigureFloor;
            player = CreateFigure("PlayerFigure", new Vector2(-FigureSpacing, floor), "あなた", PlayerInk);
            enemy = CreateFigure("EnemyFigure", new Vector2(FigureSpacing, floor), "あいて", EnemyInk);
            return true;
        }

        private Figure CreateFigure(string name, Vector2 feet, string caption, Color32 ink)
        {
            var size = new Vector2(figureWidth * ArtPixel, figureHeight * ArtPixel);
            var rootObject = new GameObject(name, typeof(RectTransform));
            var root = (RectTransform)rootObject.transform;
            root.SetParent(graphContainer, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = size;
            root.anchoredPosition = feet;
            spawnedUIElements.Add(rootObject);

            var shaker = Stretch(new GameObject("Body", typeof(RectTransform)), root);
            var body = Stretch(new GameObject("Skin", typeof(RectTransform), typeof(Image)), shaker);
            SetupImage(body.GetComponent<Image>(), figureBody);

            var bloodObject = new GameObject("Blood", typeof(RectTransform), typeof(RawImage));
            var bloodRect = Stretch(bloodObject, shaker);
            var blood = bloodObject.GetComponent<RawImage>();
            blood.texture = figureBlood;
            blood.raycastTarget = false;

            var ticks = Stretch(new GameObject("Ticks", typeof(RectTransform), typeof(Image)), shaker);
            SetupImage(ticks.GetComponent<Image>(), figureTicks);

            // 名前は足もとの下。倒れても動かさない（Root の外に置く）
            var nameText = CreateText(name + "Name", graphContainer, caption, 20f, ink);
            var nameRect = nameText.rectTransform;
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = feet + new Vector2(0f, -ArtPixel * 2f);

            // 「ロン」は頭の上（体と一緒に動く）
            var ron = CreateText("Ron", shaker, "ロン", 26f, ink);
            var ronRect = ron.rectTransform;
            ronRect.anchorMin = ronRect.anchorMax = new Vector2(0.5f, 1f);
            ronRect.pivot = new Vector2(0.5f, 0f);
            ronRect.anchoredPosition = new Vector2(0f, ArtPixel * 2f);
            ron.gameObject.SetActive(false);

            return new Figure { Root = root, Shaker = shaker, Blood = blood, BloodRect = bloodRect, Ron = ron };
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color32 color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(120f, 30f);
            if (parent == graphContainer) spawnedUIElements.Add(go);
            var label = go.GetComponent<TextMeshProUGUI>();
            // 見出し（圧倒的優勢）と同じ字。紙に書いた字に見せるので縁取りはしない
            label.font = statusText != null && statusText.font != null ? statusText.font : KillingMahjong.UI.GameUiTypography.Body;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform Stretch(GameObject go, Transform parent)
        {
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void SetupImage(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.raycastTarget = false;
        }

        /// <summary>
        /// 人型メーター.png から人型の絵を3枚（体・血・目盛り）作る。1回だけ作って使い回す。
        /// 絵の読み込み設定は読めない（isReadable オフ）ので、一度 GPU で写して読む。
        /// 1ドットの真ん中の色を拾い、決まった色（背景・肌・中身・目盛り）のどれに近いかで分ける。
        /// </summary>
        private static bool EnsureFigureArt()
        {
            if (figureBody != null && figureTicks != null && figureBlood != null) return true;
            var source = Resources.Load<Texture2D>("人型メーター");
            if (source == null) return false;

            var temporary = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, temporary);
            var previous = RenderTexture.active;
            RenderTexture.active = temporary;
            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply(false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            // 元の絵の、ドット (x, y) の種類。y は上から数える
            char Kind(int x, int y)
            {
                Color c = readable.GetPixelBilinear((x + 0.5f) / FigureDotsPerSide, 1f - (y + 0.5f) / FigureDotsPerSide);
                char best = ' '; float bestDistance = float.MaxValue;
                foreach (var (kind, color) in new[] {
                    (' ', new Color32(95, 205, 228, 255)), ('s', new Color32(238, 195, 154, 255)),
                    ('b', new Color32(255, 152, 161, 255)), ('k', new Color32(0, 0, 0, 255)) })
                {
                    Color reference = color;
                    float d = (c.r - reference.r) * (c.r - reference.r) + (c.g - reference.g) * (c.g - reference.g) +
                              (c.b - reference.b) * (c.b - reference.b);
                    if (d < bestDistance) { bestDistance = d; best = kind; }
                }
                return bestDistance < 0.05f ? best : ' ';
            }

            int half = FigureRight - FigureAxis + 1;
            int w = half * 2 + 2, h = FigureBottom - FigureTop + 1 + 2; // 輪郭の分、上下左右に1ドット
            var kinds = new char[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) kinds[x, y] = ' ';
            for (int row = FigureTop; row <= FigureBottom; row++)
                for (int k = 0; k < half; k++)
                {
                    char right = Kind(FigureAxis + k, row);
                    // 目盛りは元の位置にだけ写す（返した側には付けない）
                    if (right == 'k') right = 'b';
                    int y = h - 2 - (row - FigureTop); // 下から数える
                    kinds[1 + half + k, y] = right;
                    kinds[half - k, y] = right;
                }
            for (int row = FigureTop; row <= FigureBottom; row++)
                for (int x = FigureScreenLeft; x <= FigureRight; x++)
                    if (Kind(x, row) == 'k')
                        kinds[1 + half + (x - FigureAxis), h - 2 - (row - FigureTop)] = 'k';
            Object.Destroy(readable);

            var body = new Color32[w * h];
            var blood = new Color32[w * h];
            var ticks = new Color32[w * h];
            int low = h, high = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char kind = kinds[x, y];
                    int i = y * w + x;
                    if (kind == 's') body[i] = FigureSkin;
                    else if (kind == 'b' || kind == 'k')
                    {
                        body[i] = FigureEmpty;
                        blood[i] = FigureBlood;
                        low = Mathf.Min(low, y);
                        high = Mathf.Max(high, y);
                        if (kind == 'k') ticks[i] = new Color32(0, 0, 0, 255);
                    }
                    else
                    {
                        // 体のすぐ外側の1ドットを濃い輪郭にする
                        bool edge = false;
                        for (int dy = -1; dy <= 1 && !edge; dy++)
                            for (int dx = -1; dx <= 1 && !edge; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if ((dx != 0 || dy != 0) && nx >= 0 && nx < w && ny >= 0 && ny < h && kinds[nx, ny] != ' ')
                                    edge = true;
                            }
                        if (edge) body[i] = FigureOutline;
                    }
                }
            if (high < 0) return false;
            // 血の左の縁（肌と接する内側の1ドット）を少し明るくして、平らな塗りに丸みを出す
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (blood[i].a == 0) continue;
                    if (x > 0 && kinds[x - 1, y] == 's') blood[i] = FigureBloodTop;
                }

            figureWidth = w; figureHeight = h;
            bloodLowRow = low; bloodHighRow = high;
            figureBody = MakeSprite(body, w, h, "MomentumFigureBody");
            figureTicks = MakeSprite(ticks, w, h, "MomentumFigureTicks");
            figureBlood = MakeTexture(blood, w, h, "MomentumFigureBlood");
            return true;
        }

        private static Texture2D MakeTexture(Color32[] pixels, int w, int h, string name)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Sprite MakeSprite(Color32[] pixels, int w, int h, string name)
        {
            var sprite = Sprite.Create(MakeTexture(pixels, w, h, name), new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
            sprite.name = name;
            return sprite;
        }

        // ---- 紙 ----

        /// <summary>
        /// 紙を敷く。シーンの Backdrop（黒い四角）の場所と大きさに合わせ、黒い四角は隠す。
        /// 紙は使い回す（出すたびに作らない）。
        /// </summary>
        private void EnsurePaper()
        {
            var backdrop = transform.Find("Backdrop") as RectTransform;
            if (backdrop != null)
            {
                var backdropImage = backdrop.GetComponent<Image>();
                if (backdropImage != null) backdropImage.enabled = false;
            }
            if (paper != null) return;
            if (paperHalf == null)
            {
                var texture = Resources.Load<Texture2D>("UI");
                if (texture == null) return;
                // 縁の 80 画素（10 ドット）が ArtPixel の 10 倍に見える大きさ（Canvas の基準は 100 画素で 1 単位）
                float ppu = 100f * PaperTexelsPerDot / ArtPixel;
                paperHalf = Sprite.Create(texture, PaperHalfRect, new Vector2(0.5f, 0.5f), ppu, 0,
                    SpriteMeshType.FullRect, PaperHalfBorder);
                paperHalf.name = "MomentumPaperHalf";
            }

            var paperObject = new GameObject("Paper", typeof(RectTransform));
            paper = (RectTransform)paperObject.transform;
            paper.SetParent(transform, false);
            paper.SetAsFirstSibling();
            if (backdrop != null)
            {
                paper.anchorMin = backdrop.anchorMin;
                paper.anchorMax = backdrop.anchorMax;
                paper.pivot = backdrop.pivot;
                paper.anchoredPosition = backdrop.anchoredPosition;
                paper.sizeDelta = backdrop.sizeDelta;
            }
            else
            {
                paper.anchorMin = paper.anchorMax = paper.pivot = new Vector2(0.5f, 0.5f);
                paper.sizeDelta = new Vector2(660f, 400f);
            }
            // 下半分はそのまま、上半分は上下に反転して、真ん中で突き合わせる
            PaperHalf(paper, "Lower", false);
            PaperHalf(paper, "Upper", true);
        }

        private static void PaperHalf(RectTransform parent, string name, bool flipped)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, flipped ? 0.5f : 0f);
            rt.anchorMax = new Vector2(1f, flipped ? 1f : 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (flipped) rt.localScale = new Vector3(1f, -1f, 1f);
            var image = go.GetComponent<Image>();
            image.sprite = paperHalf;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.raycastTarget = false;
        }

        private void UpdateStatusText(List<int> pHistory, List<int> eHistory)
        {
            if (statusText == null || pHistory.Count == 0 || eHistory.Count == 0) return;

            int lastP = pHistory[pHistory.Count - 1];
            int lastE = eHistory[eHistory.Count - 1];

            if (lastP > lastE * 2)
            {
                statusText.text = "圧倒的優勢";
                statusText.color = PlayerInk; // 紙の上なので墨の色（2026-11-25。以前は黄）
            }
            else if (lastP < lastE / 2)
            {
                statusText.text = "絶体絶命";
                statusText.color = EnemyInk;
            }
            else if (lastP > lastE)
            {
                statusText.text = "優勢";
                statusText.color = new Color32(62, 70, 120, 255);
            }
            else if (lastP < lastE)
            {
                statusText.text = "劣勢";
                statusText.color = new Color32(120, 64, 64, 255);
            }
            else
            {
                statusText.text = "互角";
                statusText.color = new Color32(60, 40, 45, 255);
            }
        }

        private IEnumerator FadeRoutine(float targetAlpha)
        {
            if (canvasGroup == null) yield break;

            float startAlpha = canvasGroup.alpha;
            float time = 0;
            while (time < fadeDuration)
            {
                time += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, PresentationTiming.Sample(time) / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = targetAlpha;
        }

        private void ClearGraph()
        {
            foreach (var element in spawnedUIElements)
            {
                if (element != null) Destroy(element);
            }
            spawnedUIElements.Clear();
            player = enemy = null;
            if (statusText != null) statusText.text = "";
        }
    }
}
