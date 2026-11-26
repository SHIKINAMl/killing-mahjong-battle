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
        [SerializeField] private float fadeDuration = 0.5f;

        private List<GameObject> spawnedUIElements = new List<GameObject>();
        public bool IsShowing { get; private set; }

        // ---- 振り返りの紙（2026-11-26）----
        //
        // プランナー「グラフを見たい人の心理としては、対局の結果を振り返りたいことだけだから、
        // 動画にして瞬間的に表示するものでもない気がする」（2人の人型の案には「コミカルすぎる」）。
        // それまでは、線が左から伸び（ピッという音つき）、2秒で勝手に消える「演出」だった。
        // - 最初から全部描いた状態で出す。伸びる動き・音はなし（→ 2026-11-27 ユーザーの指示で線を引く形に戻した。DrawLines）
        // - クリックするまで閉じない（右下の ▼ が合図。ED の本文と同じ印）
        // - 振り返りに要るものだけ書き添える：ロンの点に役の名前（満貫・跳満…）、線の右端に最後の血の量
        // - 出てすぐのクリックは受けない（結果を待って連打していた指で、見る前に閉じないように）

        /// <summary>出てからクリックを受け付けるまでの時間。</summary>
        private const float ClickGuardSeconds = 0.6f;

        /// <summary>
        /// ▼ の高さ（グラフの枠の下端から）。紙の下の余白はほとんど無く、負けた側の線は血 0 で
        /// 右下の角に着くので、紙の中に置くと線か緑の縁に重なる。紙のすぐ下（卓の上）に置く。
        /// </summary>
        private const float NextMarkY = -72f;
        private bool dismissed;
        private RectTransform clickCatcher;
        private RectTransform nextMark;

        /// <summary>閉じる（クリックと同じ）。試写の台の自動送りからも呼ぶ。</summary>
        public void Dismiss() => dismissed = true;

        // ---- 紙に描く（2026-11-25）----
        //
        // ユーザー「対局終了時のグラフがAIくさすぎます」→ 選んでもらった方向は「役一覧の紙に描く」。
        // 以前は半透明の黒い四角の上に、なめらかな赤と青の斜線・ギザギザの「ロン!」吹き出し・
        // 逆転の「?」を重ねていて、表計算ソフトのグラフのように見えていた。
        // - 台は右上の役一覧と同じ紙（Resources/UI.png）。上に「役一覧」の札が掛かっていない下半分を切り出し、
        //   上下に反転した2枚で1枚の紙にする。**元の絵は変えない**（切り出して読むだけ）
        // - 線は紙と同じドットの格子で描く（小さなテクスチャに点を打ち、Point で拡大）。なめらかな斜線にしない
        // - ロンの点には、線と同じドットで描いた血のしずくを置いて役の名前を書く（勝った側の色。下の DropPattern）
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

        /// <summary>
        /// ロンの点に置く血のしずく（紙のドットで 9x12。上が先、# が血、+ が光、- が影）。
        /// 精算の血しぶきの絵を縮めて押していたが、ユーザー「ロンの血が汚すぎます」（2026-11-26）。
        /// 紙の上で黒い汚れに見えたので、線と同じドットで描いた、輪郭のはっきりしたしずくにした。
        /// </summary>
        private static readonly string[] DropPattern =
        {
            "....#....",
            "....#....",
            "...###...",
            "...###...",
            "..#####..",
            ".##+####.",
            ".#+#####.",
            "#++#####-",
            "#+######-",
            "#######--",
            ".#####--.",
            "..#---...",
        };

        /// <summary>しずくの丸い所の真ん中（下から数えた行）。ここを線の点に合わせる。</summary>
        private const float DropBulbRow = 3.5f;

        private static Texture2D playerDrop, enemyDrop;

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

        /// <summary>ロンの点に置いたしずく（字つき）。</summary>
        private readonly List<RonCallout> ronCallouts = new List<RonCallout>();

        private struct RonCallout
        {
            public RectTransform Rect;
            /// <summary>線がここまで引かれたら出す（0〜1、グラフの幅で測る）。</summary>
            public float Progress;
            public float Scale;
        }

        /// <summary>大差がついた点（線がここまで引かれたら派手にする）。</summary>
        private readonly List<BigSwing> bigSwings = new List<BigSwing>();

        /// <summary>最後の血の量（線を引き終えてから出す）。</summary>
        private readonly List<GameObject> finalLabels = new List<GameObject>();

        private struct BigSwing
        {
            public float Progress;
            /// <summary>差を広げた側（あなたなら青、あいてなら赤で光らせる）。</summary>
            public bool ByPlayer;
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

            // 出ていないあいだは裏のUIの入力を奪わない（出したときにクリックを受ける。ShowClickCatcher）
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
            // 目盛りだけの紙を出してから、線を左から右へ引く（DrawLines）。引き終えたら見出しを押す
            DrawGraph(playerHpHistory, enemyHpHistory, ronMarks);
            SetGraphProgress(0f);
            UpdateStatusText(playerHpHistory, enemyHpHistory);
            if (statusText != null) statusText.rectTransform.localScale = Vector3.zero; // 引き終えてから押す（StampHeadline）
            ShowClickCatcher();

            dismissed = false;
            yield return StartCoroutine(FadeRoutine(1f));
            yield return StartCoroutine(DrawLines());
            yield return StartCoroutine(StampHeadline());

            // クリックされるまで待つ。出てすぐのクリックは受けない
            float shown = 0f;
            while (!dismissed || shown < ClickGuardSeconds)
            {
                if (shown < ClickGuardSeconds) dismissed = false;
                shown += Time.unscaledDeltaTime;
                // ▼ をゆっくり上下させる（ED の本文送りと同じ合図）。ドット単位で動かす
                if (nextMark != null)
                    nextMark.anchoredPosition = new Vector2(0f, NextMarkY + Mathf.Round(Mathf.Sin(shown * 4f)) * ArtPixel);
                yield return null;
            }

            // フェードアウト
            yield return StartCoroutine(FadeRoutine(0f));

            // グラフクリア & 非表示
            ClearGraph();
            IsShowing = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 画面全体でクリックを受ける透明の板と、右下の ▼ を出す。
        /// 板があるあいだは裏の盤面のボタンを押せない（閉じるクリックが裏に抜けないように）。
        /// </summary>
        private void ShowClickCatcher()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            if (clickCatcher == null)
            {
                var catcher = new GameObject("ClickToClose", typeof(RectTransform), typeof(Image), typeof(Button));
                clickCatcher = (RectTransform)catcher.transform;
                clickCatcher.SetParent(transform, false);
                clickCatcher.anchorMin = Vector2.zero;
                clickCatcher.anchorMax = Vector2.one;
                clickCatcher.offsetMin = new Vector2(-4000f, -4000f); // 親の大きさによらず画面を覆う
                clickCatcher.offsetMax = new Vector2(4000f, 4000f);
                var image = catcher.GetComponent<Image>();
                image.color = Color.clear;
                var button = catcher.GetComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(Dismiss);
            }
            clickCatcher.SetAsLastSibling();

            if (nextMark == null && graphContainer != null)
            {
                var mark = new GameObject("NextMark", typeof(RectTransform), typeof(TextMeshProUGUI));
                nextMark = (RectTransform)mark.transform;
                nextMark.SetParent(graphContainer, false);
                nextMark.anchorMin = nextMark.anchorMax = new Vector2(1f, 0f);
                nextMark.pivot = new Vector2(1f, 0f);
                nextMark.sizeDelta = new Vector2(24f, 16f);
                var label = mark.GetComponent<TextMeshProUGUI>();
                label.font = statusText != null && statusText.font != null ? statusText.font : KillingMahjong.UI.GameUiTypography.Body;
                label.text = "▼";
                label.fontSize = 18f;
                label.color = new Color32(250, 244, 214, 255); // 紙の色。卓の緑の上で見えるように
                label.alignment = TextAlignmentOptions.BottomRight;
                label.raycastTarget = false;
            }
        }

        // ---- ドパれる紙（2026-11-27）----
        //
        // プランナー「横に点数とかつけられんかな」「図が全然ドパれないからもっとドパれるようにしてほしいかも」。
        // 出したまま・クリックで閉じる形は変えず（「動画にして瞬間的に表示するものでもない」）、紙そのものを派手にする。
        // - 左に血の量の目盛り（0・半分・上）と点線。線は目盛りのぶん右から始める
        // - 2本の線のあいだを、上にいる側の色で網目に塗る（どこで勝っていたかが面で見える）
        // - ロンの字に、そのロンで増えた血の量を添える（跳満 +12,000）
        // - 紙が出たら見出し（圧倒的優勢など）を判子のように押す。音つき・紙が少し揺れる。押すのはこの一度だけ
        // - 最後の血の量の字を少し大きく（26 だと見出しの「圧倒的優勢」にぶつかった）

        // ---- 線を引く・大差で派手に（2026-11-27）----
        //
        // ユーザー「線を一括で表示するんじゃなくて、左から右に引いてくれないか」「デカく差がついた時には派手な感じで」。
        // 紙のまま・クリックで閉じる形は変えず、出だしだけ線を引いて見せる。
        // - 目盛りだけの紙が出てから、2本の線を左から右へ同時に引く（網目も一緒に）。ロンの点の前後は少しゆっくり
        // - ロンの点に線が届いたら、しずくがぽんと出る（強い役ほど低い音）
        // - 1回で差が大きく動いた点（目盛りの上の値の BigSwingRatio 以上）では、線が一瞬止まり、
        //   差を広げた側の色で紙が光り、紙が大きく揺れ、ドンと鳴る
        // - 引いている途中のクリックは「最後まで飛ばす」（見たい人を待たせない）。閉じるのは次のクリック

        /// <summary>線を引き終えるまでの時間（ロンでゆっくりになるぶんは別）。</summary>
        private const float DrawSeconds = 1.6f;
        /// <summary>ロンの点の前後（進み具合で測る幅）と、点の上での速さの倍率。</summary>
        private const float RonSlowRange = 0.06f;
        private const float RonSlowSpeed = 0.3f;
        private const float RonPopSeconds = 0.16f;
        /// <summary>この割合（目盛りの上の値に対して）以上、1回で差が動いたら大差。</summary>
        private const float BigSwingRatio = 0.3f;
        /// <summary>大差の点で線を止める時間。</summary>
        private const float BigSwingHoldSeconds = 0.24f;
        /// <summary>引いている音を鳴らす間隔（紙のドットの列）。</summary>
        private const int PenSoundEveryColumns = 8;

        private IEnumerator DrawLines()
        {
            if (reveal == null || graphContainer == null) yield break;
            ronCallouts.Sort((a, b) => a.Progress.CompareTo(b.Progress));
            bigSwings.Sort((a, b) => a.Progress.CompareTo(b.Progress));
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            float width = graphContainer.rect.width;
            int nextRon = 0, nextSwing = 0, lastSoundColumn = AxisDots;
            // 線は目盛りの右から始まるので、そこまでは最初から見せる
            float progress = revealCell > 0f && width > 0f ? AxisDots * revealCell / width : 0f;
            while (progress < 1f && !dismissed) // 途中のクリックは最後まで飛ばす
            {
                float next = Mathf.Min(1f, progress + Time.unscaledDeltaTime / DrawSeconds * RonSpeedAt(progress));
                bool swing = nextSwing < bigSwings.Count && next >= bigSwings[nextSwing].Progress;
                if (swing) next = Mathf.Max(progress, bigSwings[nextSwing].Progress);
                progress = next;
                SetGraphProgress(progress);
                PopRonsUpTo(progress, ref nextRon, audio);
                if (swing)
                {
                    // 大差の点に届いたら、そこで止めて派手にする
                    yield return StartCoroutine(BigSwingBurst(bigSwings[nextSwing++].ByPlayer));
                    continue;
                }
                int column = revealCell > 0f ? Mathf.FloorToInt(progress * width / revealCell) : 0;
                if (audio != null && column - lastSoundColumn >= PenSoundEveryColumns)
                {
                    lastSoundColumn = column;
                    audio.PlayStingerPitched("se_graph_tick", 1f, KillingMahjong.Managers.AudioManager.PitchJitter.VerySmall, 0.35f);
                }
                yield return null;
            }

            // 飛ばしたとき・引き終えたときは全部見せる
            bool skipped = dismissed;
            SetGraphProgress(1f);
            for (; nextRon < ronCallouts.Count; nextRon++)
            {
                var callout = ronCallouts[nextRon];
                if (callout.Rect == null) continue;
                callout.Rect.gameObject.SetActive(true);
                callout.Rect.localScale = Vector3.one * callout.Scale;
            }
            if (!skipped) yield return new WaitForSecondsRealtime(RonPopSeconds);
            // 最後の血の量は、線が右端に着いてから出す（先に出すと結果が分かってしまう）
            foreach (var label in finalLabels)
                if (label != null) label.SetActive(true);
            dismissed = false; // 飛ばしたクリックで閉じない
        }

        private void PopRonsUpTo(float progress, ref int nextRon, KillingMahjong.Managers.AudioManager audio)
        {
            while (nextRon < ronCallouts.Count && progress >= ronCallouts[nextRon].Progress)
                StartCoroutine(PopRonCallout(ronCallouts[nextRon++], audio));
        }

        /// <summary>線の伸びる速さの倍率。ロンの点の前後だけ下げる。</summary>
        private float RonSpeedAt(float progress)
        {
            float speed = 1f;
            foreach (var callout in ronCallouts)
            {
                float distance = Mathf.Abs(progress - callout.Progress) / RonSlowRange;
                if (distance >= 1f) continue;
                speed = Mathf.Min(speed, Mathf.Lerp(RonSlowSpeed, 1f, Mathf.SmoothStep(0f, 1f, distance)));
            }
            return speed;
        }

        /// <summary>小さく出て一度ふくらみ、ロンの強さの大きさに落ち着く。強いロンほど低い音。</summary>
        private IEnumerator PopRonCallout(RonCallout callout, KillingMahjong.Managers.AudioManager audio)
        {
            if (callout.Rect == null) yield break;
            callout.Rect.gameObject.SetActive(true);
            audio?.PlayStingerPitched("se_ron_pop", 1f / callout.Scale, KillingMahjong.Managers.AudioManager.PitchJitter.None);
            float t = 0f;
            while (t < RonPopSeconds && callout.Rect != null)
            {
                float k = t / RonPopSeconds;
                float s = k < 0.6f ? Mathf.Lerp(0.3f, 1.3f, k / 0.6f) : Mathf.Lerp(1.3f, 1f, (k - 0.6f) / 0.4f);
                callout.Rect.localScale = Vector3.one * (s * callout.Scale);
                yield return null;
                t += Time.unscaledDeltaTime;
            }
            if (callout.Rect != null) callout.Rect.localScale = Vector3.one * callout.Scale;
        }

        /// <summary>大差の点：線を止め、差を広げた側の色で紙を光らせ、大きく揺らしてドンと鳴らす。</summary>
        private IEnumerator BigSwingBurst(bool byPlayer)
        {
            KillingMahjong.Managers.AudioManager.Instance?.PlayImpactSE();
            Color32 ink = byPlayer ? PlayerInk : EnemyInk;

            // 光：グラフの枠いっぱいにその側の色を重ね、すぐ引かせる
            var flashObject = new GameObject("BigSwingFlash", typeof(RectTransform), typeof(Image));
            var flash = (RectTransform)flashObject.transform;
            flash.SetParent(graphContainer, false);
            flash.anchorMin = Vector2.zero;
            flash.anchorMax = Vector2.one;
            flash.offsetMin = flash.offsetMax = Vector2.zero;
            spawnedUIElements.Add(flashObject);
            var flashImage = flashObject.GetComponent<Image>();
            flashImage.raycastTarget = false;

            Vector2 home = paper != null ? paper.anchoredPosition : Vector2.zero;
            Vector2 graphHome = graphContainer.anchoredPosition;
            // 揺れ：判子より大きく、横にも振る（ドット単位）
            Vector2[] offsets = { new Vector2(4, 3), new Vector2(-4, -3), new Vector2(3, -2), new Vector2(-3, 2), new Vector2(1, -1), Vector2.zero };
            float step = BigSwingHoldSeconds / offsets.Length;
            for (int i = 0; i < offsets.Length && !dismissed; i++)
            {
                var d = offsets[i] * ArtPixel;
                if (paper != null) paper.anchoredPosition = home + d;
                graphContainer.anchoredPosition = graphHome + d;
                // 薄く重ねると紙のクリーム色と混ざって灰色に濁るので、最初の2コマは濃く塗ってから引かせる
                float k = i / (float)(offsets.Length - 1);
                flashImage.color = new Color32(ink.r, ink.g, ink.b, (byte)Mathf.RoundToInt(i < 2 ? 210f - 50f * i : 120f * (1f - k)));
                yield return new WaitForSecondsRealtime(step);
            }
            if (paper != null) paper.anchoredPosition = home;
            graphContainer.anchoredPosition = graphHome;
            flashImage.color = Color.clear;
        }

        /// <summary>見出しを押す時間（大きい所から元の大きさへ）。</summary>
        private const float StampSeconds = 0.14f;

        private IEnumerator StampHeadline()
        {
            if (statusText == null || string.IsNullOrEmpty(statusText.text)) yield break;
            var head = statusText.rectTransform;
            float t = 0f;
            while (t < StampSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / StampSeconds);
                head.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, k * k);
                yield return null;
            }
            head.localScale = Vector3.one;
            KillingMahjong.Managers.AudioManager.Instance?.PlayImpactSE();

            // 紙を数コマだけドット単位で揺らす
            if (paper != null)
            {
                Vector2 home = paper.anchoredPosition;
                Vector2 graphHome = graphContainer != null ? graphContainer.anchoredPosition : Vector2.zero;
                float[] offsets = { 2f, -2f, 1f, -1f, 0f };
                foreach (float o in offsets)
                {
                    var d = new Vector2(0f, o * ArtPixel);
                    paper.anchoredPosition = home + d;
                    if (graphContainer != null) graphContainer.anchoredPosition = graphHome + d;
                    head.anchoredPosition += d;
                    yield return new WaitForSecondsRealtime(0.035f);
                    head.anchoredPosition -= d;
                }
                paper.anchoredPosition = home;
                if (graphContainer != null) graphContainer.anchoredPosition = graphHome;
            }
        }

        /// <summary>目盛りの字を置く幅（紙のドット）。線はこの右から始める。</summary>
        private const int AxisDots = 26;

        private static readonly Color32 GuideInk = new Color32(214, 198, 150, 255);

        /// <summary>目盛りの上の値。きりのよい数に切り上げる（20,000 なら 5,000 刻み、それより上は 10,000 刻み）。</summary>
        private static int NiceTop(int maxHp)
        {
            int step = maxHp <= 20000 ? 5000 : 10000;
            return Mathf.Max(step, Mathf.CeilToInt(maxHp / (float)step) * step);
        }

        /// <summary>折れ線の、列ごとの高さ（点のあいだは直線で結ぶ）。点より左右は -1。</summary>
        private static int[] ColumnHeights(Vector2Int[] points, int cols)
        {
            var ys = new int[cols];
            for (int x = 0; x < cols; x++) ys[x] = -1;
            for (int i = 0; i + 1 < points.Length; i++)
            {
                var a = points[i]; var b = points[i + 1];
                for (int x = a.x; x <= b.x && x < cols; x++)
                {
                    float k = b.x == a.x ? 1f : (x - a.x) / (float)(b.x - a.x);
                    ys[x] = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, k));
                }
            }
            if (points.Length == 1 && points[0].x < cols) ys[points[0].x] = points[0].y;
            return ys;
        }

        /// <summary>2本の線のあいだを、上にいる側の色の網目（市松）で塗る。線を描く前に呼ぶ。</summary>
        private static void FillLead(Color32[] pixels, int cols, int rows, Vector2Int[] pGrid, Vector2Int[] eGrid)
        {
            int[] py = ColumnHeights(pGrid, cols), ey = ColumnHeights(eGrid, cols);
            var paperColor = new Color32(250, 244, 214, 255);
            Color32 pTint = Color32.Lerp(paperColor, PlayerInk, 0.55f), eTint = Color32.Lerp(paperColor, EnemyInk, 0.45f);
            for (int x = 0; x < cols; x++)
            {
                if (py[x] < 0 || ey[x] < 0 || py[x] == ey[x]) continue;
                Color32 tint = py[x] > ey[x] ? pTint : eTint;
                int lo = Mathf.Min(py[x], ey[x]) + 2, hi = Mathf.Max(py[x], ey[x]);
                for (int y = lo; y < hi && y < rows; y++)
                    if (((x + y) & 1) == 0) pixels[y * cols + x] = tint;
            }
        }

        /// <summary>目盛りの点線（1ドットおき）。</summary>
        private static void GuideLine(Color32[] pixels, int cols, int rows, int y, int fromX)
        {
            if (y < 0 || y >= rows) return;
            for (int x = fromX; x < cols; x += 2) pixels[y * cols + x] = GuideInk;
        }

        private void PlaceAxisLabel(int value, float localY, float axisRight, float width)
        {
            var go = new GameObject("Axis" + value, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform;
            rect.SetParent(graphContainer, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(70f, 20f);
            rect.anchoredPosition = new Vector2(axisRight - width * 0.5f, localY);
            spawnedUIElements.Add(go);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = statusText != null && statusText.font != null ? statusText.font : KillingMahjong.UI.GameUiTypography.Body;
            label.text = value.ToString("N0");
            label.fontSize = 14f;
            label.color = new Color32(130, 112, 80, 255);
            label.alignment = TextAlignmentOptions.Right;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
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
            maxHp = NiceTop(maxHp);

            // 紙のドットの格子。線は2ドットの太さなので、上と右に1ドット空ける。左は目盛りの字のぶん空ける
            int cols = Mathf.Max(AxisDots + 4, Mathf.RoundToInt(width / ArtPixel));
            int rows = Mathf.Max(4, Mathf.RoundToInt(height / ArtPixel));
            revealCell = width / cols;
            Vector2Int[] pGrid = ToGrid(pHistory, maxPoints, maxHp, cols, rows, AxisDots);
            Vector2Int[] eGrid = ToGrid(eHistory, maxPoints, maxHp, cols, rows, AxisDots);

            var pixels = new Color32[cols * rows];
            // 目盛りの点線 → 勝っている側の網目 → 線 の順に重ねる
            float cellX0 = width / cols, cellY0 = height / rows;
            for (int tick = 0; tick <= 2; tick++)
            {
                int value = maxHp * tick / 2;
                int gy = Mathf.RoundToInt(value / (float)maxHp * (rows - 2));
                GuideLine(pixels, cols, rows, gy, AxisDots);
                PlaceAxisLabel(value, (gy + 1f) * cellY0 - height * 0.5f, (AxisDots - 2) * cellX0, width);
            }
            FillLead(pixels, cols, rows, pGrid, eGrid);
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

            // 線を見せる窓（左から広げて線を引いて見せる。DrawLines）
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
            PlaceRonCallouts(ronMarks, pGrid, eGrid, maxPoints, toLocal, width, height, pHistory, eHistory);
            FindBigSwings(pHistory, eHistory, pGrid, maxHp, toLocal, width);

            // 最後の血の量を、紙の上の帯（見出しの左右）に書く。グラフの中に書くと線やしずくに重なる
            PlaceFinalAmount("あなた", pHistory[pHistory.Count - 1], PlayerInk, true);
            PlaceFinalAmount("あいて", eHistory[eHistory.Count - 1], EnemyInk, false);
        }

        /// <summary>1回で差（あなた − あいて）が BigSwingRatio 以上動いた点を探す。</summary>
        private void FindBigSwings(List<int> pHistory, List<int> eHistory, Vector2Int[] pGrid, int maxHp,
            System.Func<Vector2Int, Vector2> toLocal, float width)
        {
            bigSwings.Clear();
            int n = Mathf.Min(Mathf.Min(pHistory.Count, eHistory.Count), pGrid.Length);
            for (int i = 1; i < n; i++)
            {
                int change = (pHistory[i] - eHistory[i]) - (pHistory[i - 1] - eHistory[i - 1]);
                if (Mathf.Abs(change) < maxHp * BigSwingRatio) continue;
                bigSwings.Add(new BigSwing { Progress = (toLocal(pGrid[i]).x + width * 0.5f) / width, ByPlayer = change > 0 });
            }
        }

        /// <summary>グラフの枠の上端から、見出しの高さまでの距離（見出しと同じ高さに並べる）。</summary>
        private const float FinalAmountAboveGraph = 40f;

        private void PlaceFinalAmount(string who, int amount, Color32 ink, bool left)
        {
            var go = new GameObject(left ? "PlayerFinal" : "EnemyFinal", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform;
            rect.SetParent(graphContainer, false);
            rect.anchorMin = rect.anchorMax = new Vector2(left ? 0f : 1f, 1f);
            rect.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            rect.sizeDelta = new Vector2(200f, 30f);
            rect.anchoredPosition = new Vector2(0f, FinalAmountAboveGraph);
            spawnedUIElements.Add(go);
            finalLabels.Add(go);
            go.SetActive(false); // DrawLines の終わりで出す
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = statusText != null && statusText.font != null ? statusText.font : KillingMahjong.UI.GameUiTypography.Body;
            label.text = who + " " + amount.ToString("N0");
            label.fontSize = 22f;
            label.color = ink;
            label.alignment = left ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
        }

        /// <summary>HP の並びを紙の格子の点にする。原点は左下。</summary>
        private static Vector2Int[] ToGrid(List<int> history, int maxPoints, int maxHp, int cols, int rows, int left)
        {
            var points = new Vector2Int[history.Count];
            for (int i = 0; i < history.Count; i++)
            {
                int x = left + (maxPoints > 1 ? Mathf.RoundToInt(i * (cols - 2 - left) / (float)(maxPoints - 1)) : 0);
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

        /// <summary>しずくの絵を作る（色ごとに1枚、使い回す）。</summary>
        private static Texture2D DropTexture(bool player)
        {
            var cached = player ? playerDrop : enemyDrop;
            if (cached != null) return cached;
            Color32 ink = player ? PlayerInk : EnemyInk;
            Color32 light = Color32.Lerp(ink, new Color32(255, 255, 255, 255), 0.45f);
            Color32 shade = Color32.Lerp(ink, new Color32(0, 0, 0, 255), 0.35f);
            int w = DropPattern[0].Length, h = DropPattern.Length;
            var pixels = new Color32[w * h];
            for (int row = 0; row < h; row++)
                for (int x = 0; x < w; x++)
                {
                    char c = DropPattern[row][x];
                    // テクスチャは下の行から並ぶ
                    pixels[(h - 1 - row) * w + x] = c == '#' ? ink : c == '+' ? light : c == '-' ? shade : default;
                }
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = player ? "MomentumDropPlayer" : "MomentumDropEnemy"
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            if (player) playerDrop = texture; else enemyDrop = texture;
            return texture;
        }

        /// <summary>
        /// ロンの点ごとに血のしずくを置く（伸びる線が届くまで隠しておく）。
        /// 置くのはロンした側の線の上。色はその側の色。大きさはロンの強さ。
        /// </summary>
        private void PlaceRonCallouts(IList<RonMark> ronMarks, Vector2Int[] pGrid, Vector2Int[] eGrid, int maxPoints,
            System.Func<Vector2Int, Vector2> toLocal, float width, float height, List<int> pHistory, List<int> eHistory)
        {
            if (ronMarks == null || maxPoints < 2) return;
            int dropW = DropPattern[0].Length, dropH = DropPattern.Length;

            foreach (var mark in ronMarks)
            {
                var grid = mark.IsLocalWin ? pGrid : eGrid;
                if (mark.Index < 1 || mark.Index >= grid.Length) continue;

                var go = new GameObject("RonDrop", typeof(RectTransform), typeof(RawImage));
                var rt = (RectTransform)go.transform;
                rt.SetParent(graphContainer, false);
                var size = new Vector2(dropW * ArtPixel, dropH * ArtPixel);
                rt.sizeDelta = size;
                // しずくの丸い所を点に合わせる（大きくなるときもそこを中心にふくらむ）
                rt.pivot = new Vector2(0.5f, DropBulbRow / dropH);
                spawnedUIElements.Add(go);
                var drop = go.GetComponent<RawImage>();
                drop.texture = DropTexture(mark.IsLocalWin);
                drop.raycastTarget = false;

                // 役の名前（満貫・跳満…）をしずくの上に、その側のインクで書き添える（紙に書いた字に見せる。縁取りはしない）。
                // しずくは役の強さで大きくするが、字は大きさをそろえる（「ダブル役満」が大きくなりすぎる）
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                var labelRect = (RectTransform)labelObject.transform;
                labelRect.SetParent(rt, false);
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 1f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.sizeDelta = new Vector2(80f, 30f);
                labelRect.anchoredPosition = new Vector2(0f, ArtPixel);
                var label = labelObject.GetComponent<TextMeshProUGUI>();
                // 見出し（圧倒的優勢）と同じ字
                label.font = statusText != null && statusText.font != null ? statusText.font : KillingMahjong.UI.GameUiTypography.Body;
                label.text = string.IsNullOrEmpty(mark.Rank) ? "ロン" : mark.Rank;
                // そのロンで増えた血の量（ロンした側の、前の点からの差）
                var history = mark.IsLocalWin ? pHistory : eHistory;
                if (history != null && mark.Index < history.Count)
                {
                    int gain = history[mark.Index] - history[mark.Index - 1];
                    if (gain > 0) label.text += " +" + gain.ToString("N0");
                }
                label.fontSize = 20f;
                label.color = mark.IsLocalWin ? (Color)PlayerInk : (Color)EnemyInk;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;

                // しずくは点からずらさない（ずらすと線の先から離れて、どのロンか分からなくなる）。
                // 左右と下だけ、しずくが枠から出ないように寄せる。上は紙の余白があるので寄せない
                float scale = RankScale(mark.Rank);
                labelRect.localScale = Vector3.one / scale;
                labelRect.sizeDelta = new Vector2(label.GetPreferredValues(label.text).x + 4f, 30f);
                Vector2 at = toLocal(grid[mark.Index]);
                float halfW = size.x * 0.5f * scale, below = size.y * rt.pivot.y * scale;
                at.x = Mathf.Clamp(at.x, -width * 0.5f + halfW, width * 0.5f - halfW);
                at.y = Mathf.Max(at.y, -height * 0.5f + below);
                rt.anchoredPosition = at;

                // 字の上に余白が無い（紙の縁にかかる）ときは、字をしずくの左に書く
                const float LabelHeight = 30f, PaperRoomAbove = 30f;
                float labelHalfWidth = labelRect.sizeDelta.x * 0.5f;
                float labelTop = at.y + (size.y * (1f - rt.pivot.y) + ArtPixel) * scale + LabelHeight;
                // ロンした側の線がもう一方より下にあるときは、字をしずくの下に書く
                // （上に書くと、上の線と、あいだの網目に重なる）
                var other = mark.IsLocalWin ? eGrid : pGrid;
                bool otherAbove = mark.Index < other.Length && other[mark.Index].y > grid[mark.Index].y;
                float labelBottom = at.y - below - ArtPixel * scale - LabelHeight;
                if (otherAbove && labelBottom > -height * 0.5f)
                {
                    labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
                    labelRect.pivot = new Vector2(0.5f, 1f);
                    float overflow = Mathf.Max(0f, at.x + labelHalfWidth - width * 0.5f)
                                   - Mathf.Max(0f, -width * 0.5f - (at.x - labelHalfWidth));
                    labelRect.anchoredPosition = new Vector2(-overflow / scale, -ArtPixel);
                }
                else if (labelTop > height * 0.5f + PaperRoomAbove)
                {
                    labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, rt.pivot.y);
                    labelRect.pivot = new Vector2(1f, 0.5f);
                    labelRect.anchoredPosition = new Vector2(-ArtPixel, 0f);
                    label.alignment = TextAlignmentOptions.Right;
                }
                else
                {
                    // 上に書くとき、字が左右の枠から出るなら内側へずらす
                    float overflow = Mathf.Max(0f, at.x + labelHalfWidth - width * 0.5f)
                                   - Mathf.Max(0f, -width * 0.5f - (at.x - labelHalfWidth));
                    labelRect.anchoredPosition = new Vector2(-overflow / scale, ArtPixel);
                }

                rt.localScale = Vector3.one * scale;
                go.SetActive(false);
                // 線がしずくの点（寄せる前の位置）まで引かれたら出す
                ronCallouts.Add(new RonCallout { Rect = rt, Progress = (toLocal(grid[mark.Index]).x + width * 0.5f) / width, Scale = scale });
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
            bigSwings.Clear();
            finalLabels.Clear();
            if (nextMark != null) Destroy(nextMark.gameObject);
            nextMark = null;
            if (clickCatcher != null) Destroy(clickCatcher.gameObject);
            clickCatcher = null;
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            reveal = null;
            if (lineTexture != null) Destroy(lineTexture);
            lineTexture = null;
            if (statusText != null)
            {
                statusText.text = "";
                statusText.rectTransform.localScale = Vector3.one;
            }
        }
    }
}
