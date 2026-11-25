using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

namespace KillingMahjong.UI.Effects
{
    [RequireComponent(typeof(CanvasGroup))]
    public class MatchMomentumUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField, Tooltip("グラフを描画するコンテナ")] 
        private RectTransform graphContainer;
        [SerializeField, Tooltip("演出テキスト（例: 圧倒的優勢）")] 
        private TextMeshProUGUI statusText;
        private CanvasGroup canvasGroup;
        
        [Header("Settings")]
        [SerializeField] private float displayDuration = 2.0f;
        [SerializeField] private float fadeDuration = 0.5f;

        private List<GameObject> spawnedUIElements = new List<GameObject>();
        private const float GrowthDuration = 1.5f;
        private const int GrowthPulseCount = 24;
        public bool IsShowing { get; private set; }

        // ---- 紙に描く（2026-11-25）----
        //
        // ユーザー「対局終了時のグラフがAIくさすぎます」→ 選んでもらった方向は「役一覧の紙に描く」。
        // 以前は半透明の黒い四角の上に、なめらかな赤と青の斜線・ギザギザの「ロン!」吹き出し・
        // 逆転の「?」を重ねていて、表計算ソフトのグラフのように見えていた。
        // - 台は右上の役一覧と同じ紙（Resources/UI.png）。上に「役一覧」の札が掛かっていない下半分を切り出し、
        //   上下に反転した2枚で1枚の紙にする。**元の絵は変えない**（切り出して読むだけ）
        // - 線は紙と同じドットの格子で描く（小さなテクスチャに点を打ち、Point で拡大）。なめらかな斜線にしない
        // - ロンの点には、精算で使っている血しぶきの絵を押して「ロン」と書く（勝った側の色）
        // - 逆転の「?」はやめた。線が交わるのを見れば分かる（字や記号を最小限に）
        // シーンの Backdrop（黒い四角）は残してあり、出すときに隠す。OpeningScene にも同じ物があるため

        /// <summary>
        /// 絵の1ドットの大きさ（この Canvas の単位）。右上の役一覧の紙と同じ
        /// （800x600 で 2。この Canvas は 960x720 の画面の画素そのままなので 2.4）。
        /// </summary>
        private const float ArtPixel = 2.4f;

        /// <summary>UI.png は読み込み後 1024x704 で、絵の1ドットが 8 画素。</summary>
        private const float PaperTexelsPerDot = 8f;

        /// <summary>
        /// UI.png の中の、紙の下半分（左下が原点の画素）。絵のドットで 左 13〜右 111・上から 45〜81。
        /// 枠は左右下とも 10 ドットぶんを伸ばさない縁にする（角の黄色い鋲も縁に入る）。
        /// </summary>
        private static readonly Rect PaperHalfRect = new Rect(104f, 56f, 784f, 288f);
        private static readonly Vector4 PaperHalfBorder = new Vector4(80f, 80f, 80f, 0f); // 左・下・右・上

        private static readonly Color32 PlayerInk = new Color32(38, 72, 168, 255);
        private static readonly Color32 EnemyInk = new Color32(176, 32, 44, 255);

        /// <summary>血しぶきの大きさ（満貫のとき）。紙のドットで 30。</summary>
        private const float StainDots = 30f;

        private static Sprite paperHalf;
        private RectTransform paper;
        private Texture2D lineTexture;
        private RectTransform reveal;
        private float revealCell;

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

        // ロンの点では線の伸びをスローにして吹き出しを出す（2026-10-15 のユーザーの要望
        // 「ロン！したとき、その場所にロン！の吹き出しを出して少しだけ溜めが欲しい」。
        // 最初は止めていたが「止まるのではなく、少しスローに」と直された）。流局の点ではスローにしない。
        // RonSlowRange は進み具合（0〜1）で測った前後の幅、RonSlowSpeed は点の上での速さの倍率
        private const float RonSlowRange = 0.07f;
        private const float RonSlowSpeed = 0.18f;
        private const float RonPopSeconds = 0.16f;
        private readonly List<RonCallout> ronCallouts = new List<RonCallout>();

        private struct RonCallout
        {
            public RectTransform Rect;
            public float Progress;
            public float Scale;
        }

        /// <summary>
        /// 吹き出しの大きさをロンの強さで変える（2026-10-15 のユーザーの要望）。
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

            // 戦況グラフは見せるだけの演出なので、裏のUIの入力を奪わないようにする
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        /// <summary>
        /// HPの推移を受け取り、モメンタムグラフを描画・表示する
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
            // 完成形を先に見せず、線の長さを0から始める。
            DrawGraph(playerHpHistory, enemyHpHistory, ronMarks);
            SetGraphProgress(0f);
            yield return StartCoroutine(FadeRoutine(1f));
            yield return StartCoroutine(GrowGraphRoutine());
            
            // テキスト演出の決定
            UpdateStatusText(playerHpHistory, enemyHpHistory);
            
            // 指定時間表示
            yield return new WaitForSeconds(displayDuration);
            
            // フェードアウト
            yield return StartCoroutine(FadeRoutine(0f));
            
            // グラフクリア & 非表示
            ClearGraph();
            IsShowing = false;
            gameObject.SetActive(false);
        }

        private IEnumerator GrowGraphRoutine()
        {
            if (reveal == null) yield break;
            ronCallouts.Sort((a, b) => a.Progress.CompareTo(b.Progress));
            int nextRon = 0;
            float progress = 0f;
            int lastPulse = -1;
            int presentationFrame = -1;
            while (progress < 1f)
            {
                // ロンの点に近づくと線の伸びがスローになり、点を過ぎると元の速さへ戻る
                // （2026-10-15 のユーザーの指摘「止まるのではなく、ロンで少しスローになる感じに」）
                progress = Mathf.Min(1f, progress + Time.deltaTime / GrowthDuration * RonSpeedAt(progress));
                while (nextRon < ronCallouts.Count && progress >= ronCallouts[nextRon].Progress)
                    StartCoroutine(PopRonCallout(ronCallouts[nextRon++]));
                if (progress >= 1f || PresentationTiming.IsNewFrame(ref presentationFrame, Time.time))
                    SetGraphProgress(progress);
                int pulse = Mathf.FloorToInt(progress * (GrowthPulseCount - 1));
                if (pulse != lastPulse)
                {
                    PlayGrowthPulse(pulse);
                    lastPulse = pulse;
                }
                yield return null;
            }
            // 吹き出しが弾け終わるまでは待つ（決着のロンは最後の点なので、伸び終わりと同時に出る）
            yield return new WaitForSeconds(RonPopSeconds);
        }

        /// <summary>
        /// 線の伸びる速さの倍率。ロンの点の前後 RonSlowRange の間だけ下げ、点の上で RonSlowSpeed になる。
        /// </summary>
        private float RonSpeedAt(float progress)
        {
            float speed = 1f;
            foreach (var callout in ronCallouts)
            {
                float distance = Mathf.Abs(progress - callout.Progress) / RonSlowRange;
                if (distance >= 1f) continue;
                float slow = Mathf.Lerp(RonSlowSpeed, 1f, Mathf.SmoothStep(0f, 1f, distance));
                speed = Mathf.Min(speed, slow);
            }
            return speed;
        }

        /// <summary>小さく出て一度ふくらみ、ロンの強さの大きさに落ち着く。</summary>
        private IEnumerator PopRonCallout(RonCallout callout)
        {
            if (callout.Rect == null) yield break;
            callout.Rect.gameObject.SetActive(true);
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio != null)
            {
                // 強いロンほど低く太い音にする（ダブル役満で約1オクターブ下）。
                // 音は Freesound（CC0）のポップ音2つを重ねたもの（2026-10-19）
                audio.PlayStingerPitched("se_ron_pop", 1f / callout.Scale,
                    KillingMahjong.Managers.AudioManager.PitchJitter.None);
            }
            float t = 0f;
            while (t < RonPopSeconds && callout.Rect != null)
            {
                float k = PresentationTiming.Sample(t) / RonPopSeconds;
                // 0 → 1.25倍 → 1倍
                float s = k < 0.6f ? Mathf.Lerp(0.3f, 1.25f, k / 0.6f) : Mathf.Lerp(1.25f, 1f, (k - 0.6f) / 0.4f);
                callout.Rect.localScale = Vector3.one * (s * callout.Scale);
                yield return null;
                t += Time.deltaTime;
            }
            if (callout.Rect != null) callout.Rect.localScale = Vector3.one * callout.Scale;
        }

        private static void PlayGrowthPulse(int pulse)
        {
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio == null) return;
            // 線が伸びるにつれて高くなる。最後の1回で1オクターブ上に届く。
            // 合成の三角波（2オクターブ上がる）から Freesound（CC0）の電子音2つを重ねたものに替えた（2026-10-19）。
            // 録音を2オクターブも上げると細くキンキンするので、上がり幅は1オクターブにしてある
            float pitch = Mathf.Pow(2f, pulse / (float)(GrowthPulseCount - 1));
            audio.PlayStingerPitched("se_graph_tick", pitch,
                KillingMahjong.Managers.AudioManager.PitchJitter.None);
        }

        private void SetGraphProgress(float progress)
        {
            // 線はドットの格子ごとに伸ばす（途中で半ドットにかからない）
            if (reveal == null || graphContainer == null) return;
            float width = graphContainer.rect.width;
            float shown = revealCell > 0f ? Mathf.Round(progress * width / revealCell) * revealCell : progress * width;
            reveal.sizeDelta = new Vector2(Mathf.Min(shown, width), 0f);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            IsShowing = false;
            ClearGraph();
        }

        private void DrawGraph(List<int> pHistory, List<int> eHistory, IList<RonMark> ronMarks)
        {
            if (pHistory == null || eHistory == null || pHistory.Count == 0 || eHistory.Count == 0 || graphContainer == null) return;

            // 非アクティブ状態から出した直後はレイアウトが未確定なことがあるので確定させてから測る
            Canvas.ForceUpdateCanvases();

            float width = graphContainer.rect.width;
            float height = graphContainer.rect.height;

            if (width <= 0f || height <= 0f)
            {
                Debug.LogWarning($"[MatchMomentumUI] {graphContainer.name} のサイズが 0 です。" +
                                 "Layout Group / Content Size Fitter を外して固定サイズにしてください。");
                return;
            }
            EnsurePaper();

            int maxPoints = Mathf.Max(pHistory.Count, eHistory.Count);

            // スケール計算用の最大HPを検索
            int maxHp = 1;
            foreach (int hp in pHistory) if (hp > maxHp) maxHp = hp;
            foreach (int hp in eHistory) if (hp > maxHp) maxHp = hp;

            // 紙のドットの格子。線は2ドットの太さなので、上と右に1ドット空ける
            int cols = Mathf.Max(4, Mathf.RoundToInt(width / ArtPixel));
            int rows = Mathf.Max(4, Mathf.RoundToInt(height / ArtPixel));
            revealCell = width / cols;
            Vector2Int[] pGrid = ToGrid(pHistory, maxPoints, maxHp, cols, rows);
            Vector2Int[] eGrid = ToGrid(eHistory, maxPoints, maxHp, cols, rows);

            var pixels = new Color32[cols * rows];
            // 相手を先に描き、重なったところは自分の線を上にする
            PlotPolyline(pixels, cols, rows, eGrid, EnemyInk);
            PlotPolyline(pixels, cols, rows, pGrid, PlayerInk);
            lineTexture = new Texture2D(cols, rows, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "MomentumInk"
            };
            lineTexture.SetPixels32(pixels);
            lineTexture.Apply(false, true);

            // 左から伸ばすための窓。中の絵は動かさず、窓の幅だけ広げる
            var revealObject = new GameObject("InkReveal", typeof(RectTransform), typeof(RectMask2D));
            reveal = (RectTransform)revealObject.transform;
            reveal.SetParent(graphContainer, false);
            reveal.anchorMin = new Vector2(0f, 0f);
            reveal.anchorMax = new Vector2(0f, 1f);
            reveal.pivot = new Vector2(0f, 0.5f);
            reveal.anchoredPosition = Vector2.zero;
            reveal.sizeDelta = Vector2.zero;
            spawnedUIElements.Add(revealObject);

            var inkObject = new GameObject("Ink", typeof(RectTransform), typeof(RawImage));
            var inkRect = (RectTransform)inkObject.transform;
            inkRect.SetParent(reveal, false);
            inkRect.anchorMin = new Vector2(0f, 0f);
            inkRect.anchorMax = new Vector2(0f, 1f);
            inkRect.pivot = new Vector2(0f, 0.5f);
            inkRect.anchoredPosition = Vector2.zero;
            inkRect.sizeDelta = new Vector2(width, 0f);
            var ink = inkObject.GetComponent<RawImage>();
            ink.texture = lineTexture;
            ink.raycastTarget = false;

            float cellX = width / cols, cellY = height / rows;
            System.Func<Vector2Int, Vector2> toLocal = g =>
                new Vector2((g.x + 1f) * cellX - width * 0.5f, (g.y + 1f) * cellY - height * 0.5f);
            PlaceRonCallouts(ronMarks, pGrid, eGrid, maxPoints, toLocal, width, height);
        }

        /// <summary>HP の並びを紙の格子の点にする。原点は左下。</summary>
        private static Vector2Int[] ToGrid(List<int> history, int maxPoints, int maxHp, int cols, int rows)
        {
            var points = new Vector2Int[history.Count];
            for (int i = 0; i < history.Count; i++)
            {
                int x = maxPoints > 1 ? Mathf.RoundToInt(i * (cols - 2) / (float)(maxPoints - 1)) : 0;
                int y = Mathf.RoundToInt(Mathf.Clamp01(history[i] / (float)maxHp) * (rows - 2));
                points[i] = new Vector2Int(x, y);
            }
            return points;
        }

        /// <summary>点を順に結ぶ。斜めは階段になる（ブレゼンハム）。太さは2ドット。</summary>
        private static void PlotPolyline(Color32[] pixels, int cols, int rows, Vector2Int[] points, Color32 color)
        {
            for (int i = 0; i + 1 < points.Length; i++)
            {
                int x0 = points[i].x, y0 = points[i].y, x1 = points[i + 1].x, y1 = points[i + 1].y;
                int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
                int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
                int err = dx + dy;
                while (true)
                {
                    for (int ox = 0; ox < 2; ox++)
                        for (int oy = 0; oy < 2; oy++)
                        {
                            int px = x0 + ox, py = y0 + oy;
                            if (px >= 0 && px < cols && py >= 0 && py < rows) pixels[py * cols + px] = color;
                        }
                    if (x0 == x1 && y0 == y1) break;
                    int e2 = 2 * err;
                    if (e2 >= dy) { err += dy; x0 += sx; }
                    if (e2 <= dx) { err += dx; y0 += sy; }
                }
            }
        }

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

        /// <summary>
        /// ロンの点ごとに血しぶきを押す（伸びる線が届くまで隠しておく）。
        /// 置くのはロンした側の線の上。色はその側の色。大きさはロンの強さ。
        /// </summary>
        private void PlaceRonCallouts(IList<RonMark> ronMarks, Vector2Int[] pGrid, Vector2Int[] eGrid, int maxPoints,
            System.Func<Vector2Int, Vector2> toLocal, float width, float height)
        {
            if (ronMarks == null || maxPoints < 2) return;
            var blue = Resources.Load<Sprite>("UI/Settlement/settlement_splatter_blue");
            var red = Resources.Load<Sprite>("UI/Settlement/settlement_splatter_red");

            foreach (var mark in ronMarks)
            {
                var grid = mark.IsLocalWin ? pGrid : eGrid;
                if (mark.Index < 1 || mark.Index >= grid.Length) continue;

                var go = new GameObject("RonStain", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(graphContainer, false);
                float size = StainDots * ArtPixel;
                rt.sizeDelta = new Vector2(size, size);
                spawnedUIElements.Add(go);

                var stain = new GameObject("Stain", typeof(RectTransform), typeof(Image));
                var stainRect = (RectTransform)stain.transform;
                stainRect.SetParent(rt, false);
                stainRect.anchorMin = Vector2.zero;
                stainRect.anchorMax = Vector2.one;
                stainRect.offsetMin = stainRect.offsetMax = Vector2.zero;
                var stainImage = stain.GetComponent<Image>();
                stainImage.sprite = mark.IsLocalWin ? blue : red;
                stainImage.preserveAspect = true;
                stainImage.raycastTarget = false;
                if (stainImage.sprite == null) stainImage.color = mark.IsLocalWin ? (Color)PlayerInk : (Color)EnemyInk;

                // 「ロン」は血しぶきの上に、その側のインクで書き添える（紙に書いた字に見せる。縁取りはしない）。
                // 白抜きで血しぶきに重ねると、しぶきの芯より字が大きく、「ロ」が紙の上で四角い枠に見えた
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                var labelRect = (RectTransform)labelObject.transform;
                labelRect.SetParent(rt, false);
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.sizeDelta = new Vector2(size * 1.6f, 30f);
                labelRect.anchoredPosition = new Vector2(0f, size * 0.32f);
                var label = labelObject.GetComponent<TextMeshProUGUI>();
                // 見出し（圧倒的優勢）と同じ字
                label.font = statusText != null && statusText.font != null ? statusText.font : KillingMahjong.UI.GameUiTypography.Body;
                label.text = "ロン";
                label.fontSize = 22f;
                label.color = mark.IsLocalWin ? (Color)PlayerInk : (Color)EnemyInk;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;

                // 点の上に押す。大きくなっても紙からはみ出さないよう、中心を枠の内側へ寄せる
                float scale = RankScale(mark.Rank);
                float half = size * 0.5f * scale;
                Vector2 at = toLocal(grid[mark.Index]);
                at.x = Mathf.Clamp(at.x, -width * 0.5f + half * 0.6f, width * 0.5f - half * 0.6f);
                at.y = Mathf.Clamp(at.y, -height * 0.5f + half * 0.6f, height * 0.5f - half * 0.6f);
                rt.anchoredPosition = at;

                go.SetActive(false);
                ronCallouts.Add(new RonCallout { Rect = rt, Scale = scale,
                    Progress = (float)mark.Index / (maxPoints - 1) });
            }
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
            ronCallouts.Clear();
            reveal = null;
            if (lineTexture != null) Destroy(lineTexture);
            lineTexture = null;
            if (statusText != null) statusText.text = "";
        }
    }
}
