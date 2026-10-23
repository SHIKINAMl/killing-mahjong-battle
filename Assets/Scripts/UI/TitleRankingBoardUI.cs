using KillingMahjong.Network;
using TMPro;
using KillingMahjong.Managers;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace KillingMahjong.UI
{
    /// <summary>
    /// タイトルで放置したときに出すランキング（2026-10-21、ユーザー指示「ランキング画面はタイトルで放置している場所に表示する」）。
    ///
    /// 展示のタイトル（どこを押しても始まる画面）で、しばらく操作が無いと全面に出す。
    /// 種類（最大打点・累計獲得点数・最大局数）を順に切り替えて見せ、何か操作したらタイトルへ戻る。
    /// 戻すための操作では、チュートリアルは始めない（いちばん手前で押した物を受け止める）。
    ///
    /// **ランキングと紹介動画を交互に出す**（2026-10-22、ユーザー指示「放置していた場合ランキングと動画交互に出てほしい」）。
    /// ランキングの種類を一巡したら動画、動画が終わったらまたランキング。
    /// 動画は紹介PV（`StreamingAssets/Video/title_pv.mp4`）。WebGL でも読めるよう、Resources ではなく
    /// StreamingAssets から URL で流す。流しているあいだはタイトルの曲を止めて動画の音を出す。
    /// 読めなかったときはランキングだけを回す。
    ///
    /// どちらを出している間も、下に「クリックでタイトルへ」をゆっくり点滅させて出しておく（同日のユーザー指示）。
    ///
    /// 中身はサーバーから取る（<see cref="RankingFetcher"/>）。対局の接続とは別に繋ぎ、終わったら切る。
    ///
    /// **画面の部品はシーンに置く**（<see cref="SceneFirst"/>）。<see cref="Ensure"/> は「探して、無ければ作る」。
    /// 位置や色はシーンのほうが正で、ここの数字はシーンに無かったときの控え。
    /// </summary>
    public sealed class TitleRankingBoardUI : MonoBehaviour
    {
        public const string RootName = "TitleRankingBoard";
        private const int RowCount = 10;

        /// <summary>何秒放置したら出すか。</summary>
        [SerializeField] private float idleSeconds = 25f;

        /// <summary>1つの種類を見せる秒数。</summary>
        [SerializeField] private float pageSeconds = 8f;

        /// <summary>前に取ってから何秒たっていたら取り直すか（サーバーも上位の一覧を 30 秒ためている）。</summary>
        [SerializeField] private float refreshSeconds = 60f;

        /// <summary>流す動画。StreamingAssets からの場所。</summary>
        [SerializeField] private string videoPath = "Video/title_pv.mp4";

        /// <summary>動画の読み込みを何秒待つか。過ぎたら、その回は飛ばしてランキングへ戻る。</summary>
        [SerializeField] private float videoPrepareTimeout = 8f;

        /// <summary>「クリックでタイトルへ」の点滅の1往復の秒数。ゆっくり（ユーザー指示）。</summary>
        [SerializeField] private float hintBlinkSeconds = 2.4f;

        internal static readonly string[] Kinds = { "max_damage", "match_points", "rounds" };
        internal static readonly string[] KindTitles = { "最大打点", "累計獲得点数", "最大局数" };
        internal static readonly string[] KindNotes =
        {
            // 文言は 2026-10-23 のユーザー指示
            "一回のアガリで相手に与えたダメージランキング",
            "一つの対局で獲得した点数ランキング",
            "一番長引いた局数ランキング",
        };
        internal static readonly string[] KindUnits = { "点", "点", "局" };

        private TitleUIManager title;
        private CanvasGroup group;
        private RankingFetcher fetcher;
        private TMP_Text kindLabel, kindNote, pager, status, returnHint;
        private RectTransform rankingFrame, videoLayer;
        private RawImage videoImage;
        private VideoPlayer player;
        private RenderTexture videoTexture;
        private bool playingVideo, videoUnavailable;
        private float videoStartedAt;
        private readonly TMP_Text[,] cells = new TMP_Text[RowCount, 3];

        private float lastInput;
        private bool showing;
        private int page;
        private float pageStartedAt;
        private bool loading, failed;

        public bool IsShowing => showing;

        /// <summary>シーンの物を探して（無ければ作って）つなぐ。タイトルの Start と焼き込みから呼ぶ。</summary>
        public static TitleRankingBoardUI Ensure(TitleUIManager owner)
        {
            var root = SceneFirst.Root(RootName, out bool made, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image), typeof(Button), typeof(CanvasGroup));
            if (made)
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = Common.UISortingOrders.TitleRankingBoard;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800f, 600f);
                var dim = root.GetComponent<Image>();
                dim.color = new Color32(8, 3, 8, 214);
                var button = root.GetComponent<Button>();
                button.targetGraphic = dim;
                button.transition = Selectable.Transition.None;
            }
            var board = root.GetComponent<TitleRankingBoardUI>();
            if (board == null) board = root.AddComponent<TitleRankingBoardUI>();
            board.Build();
            board.title = owner;
            return board;
        }

        private void Build()
        {
            var font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed");
            group = GetComponent<CanvasGroup>();
            fetcher = GetComponent<RankingFetcher>();
            if (fetcher == null) fetcher = gameObject.AddComponent<RankingFetcher>();

            // 枠は2枚重ね（くすんだ金の縁の内側に、夜色の板）。2単位ずつ内側へ寄せて、ドットの縁にする
            var frame = Box(transform, "Frame", new Vector2(564f, 484f), Vector2.zero, new Color32(120, 110, 85, 255));
            rankingFrame = frame;
            var panel = Box(frame, "Panel", new Vector2(556f, 476f), Vector2.zero, new Color32(22, 15, 24, 252));

            Label(panel, "Heading", "ランキング", new Vector2(0f, 205f), new Vector2(520f, 40f), 30f,
                new Color32(200, 40, 58, 255), TextAlignmentOptions.Center, font);
            kindLabel = Label(panel, "KindLabel", KindTitles[0], new Vector2(0f, 166f), new Vector2(520f, 28f), 20f,
                new Color32(232, 220, 200, 255), TextAlignmentOptions.Center, font);
            kindNote = Label(panel, "KindNote", KindNotes[0], new Vector2(0f, 143f), new Vector2(520f, 16f), 10f,
                new Color32(170, 156, 140, 255), TextAlignmentOptions.Center, font);
            pager = Label(panel, "Pager", "", new Vector2(0f, 124f), new Vector2(200f, 16f), 10f,
                new Color32(170, 156, 140, 255), TextAlignmentOptions.Center, font);

            var rows = Box(panel, "Rows", new Vector2(520f, 280f), new Vector2(0f, -38f), Color.clear);
            rows.GetComponent<Image>().raycastTarget = false;
            for (int i = 0; i < RowCount; i++)
            {
                var row = Box(rows, "Row" + (i + 1), new Vector2(520f, 26f), new Vector2(0f, 126f - i * 28f),
                    i % 2 == 0 ? new Color32(36, 22, 32, 255) : new Color32(28, 18, 26, 255));
                row.GetComponent<Image>().raycastTarget = false;
                cells[i, 0] = Label(row, "Rank", "", new Vector2(-212f, 0f), new Vector2(80f, 26f), 20f,
                    new Color32(232, 220, 200, 255), TextAlignmentOptions.Right, font);
                cells[i, 1] = Label(row, "Name", "", new Vector2(-34f, 0f), new Vector2(240f, 26f), 20f,
                    new Color32(232, 220, 200, 255), TextAlignmentOptions.Left, font);
                cells[i, 2] = Label(row, "Value", "", new Vector2(176f, 0f), new Vector2(150f, 26f), 20f,
                    new Color32(232, 220, 200, 255), TextAlignmentOptions.Right, font);
                // 名前に < が入っても、文字の飾りとして読まない
                cells[i, 1].richText = false;
            }

            status = Label(panel, "Status", "", new Vector2(0f, -38f), new Vector2(520f, 28f), 20f,
                new Color32(232, 220, 200, 255), TextAlignmentOptions.Center, font);
            // 動画の層。ランキングの枠と同じ場所に重ね、出すときは枠のほうを隠す。
            // 動画はゲームの画面と同じ 4:3（2026-10-22、ユーザーが黒帯なしの 4:3 版に決めた）。
            // 黒地を画面いっぱいに敷き、その中に 4:3 のまま収める（画面が横長なら左右だけ黒くなる）
            bool layerMade = transform.Find("Video") == null;
            videoLayer = Box(transform, "Video", Vector2.zero, Vector2.zero, Color.black);
            videoLayer.GetComponent<Image>().raycastTarget = false;
            if (layerMade)
            {
                videoLayer.anchorMin = Vector2.zero;
                videoLayer.anchorMax = Vector2.one;
            }
            var screen = SceneFirst.Child(videoLayer, "Screen", out bool madeScreen, typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            videoImage = screen.GetComponent<RawImage>();
            if (madeScreen)
            {
                var fit = screen.GetComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = 4f / 3f;
                videoImage.raycastTarget = false;
            }
            player = videoLayer.GetComponent<VideoPlayer>();
            if (player == null) player = videoLayer.gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            videoLayer.gameObject.SetActive(false);

            // 下の「クリックでタイトルへ」。ランキングでも動画でも出しっぱなしにして、ゆっくり点滅させる。
            // いちばん手前に描くよう、最後の子にしておく。画面の縦横比が変わっても下に出るよう、下端に揃えて置く。
            // 後ろのタイトル画面の下の文字が透けて重なるので、横いっぱいの暗い帯を敷く
            bool plateMade = transform.Find("ReturnHintPlate") == null;
            var plate = Box(transform, "ReturnHintPlate", new Vector2(0f, 44f), Vector2.zero, new Color32(8, 3, 8, 245));
            plate.GetComponent<Image>().raycastTarget = false;
            if (plateMade)
            {
                plate.anchorMin = new Vector2(0f, 0f);
                plate.anchorMax = new Vector2(1f, 0f);
                plate.anchoredPosition = new Vector2(0f, 28f);
            }
            plate.SetAsLastSibling();
            bool hintMade = transform.Find("ReturnHint") == null;
            returnHint = Label(transform, "ReturnHint", "クリックでタイトルへ", Vector2.zero, new Vector2(400f, 28f), 20f,
                new Color32(232, 220, 200, 255), TextAlignmentOptions.Center, font);
            if (hintMade)
            {
                var hintRt = returnHint.rectTransform;
                hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 0f);
                hintRt.anchoredPosition = new Vector2(0f, 28f);
            }
            returnHint.transform.SetAsLastSibling();

            SetVisible(false);
        }

        private RectTransform Box(Transform parent, string name, Vector2 size, Vector2 position, Color color)
        {
            var go = SceneFirst.Child(parent, name, out bool made, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            if (made)
            {
                rt.sizeDelta = size;
                rt.anchoredPosition = position;
                go.GetComponent<Image>().color = color;
            }
            return rt;
        }

        private TMP_Text Label(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize,
            Color color, TextAlignmentOptions align, TMP_FontAsset font)
        {
            var go = SceneFirst.Child(parent, name, out bool made, typeof(RectTransform), typeof(TextMeshProUGUI));
            var label = go.GetComponent<TextMeshProUGUI>();
            if (made)
            {
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = size;
                rt.anchoredPosition = position;
                if (font != null) label.font = font;
                label.fontSize = fontSize;
                label.color = color;
                label.alignment = align;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.raycastTarget = false;
                label.text = text;
            }
            return label;
        }

        private void Start()
        {
            var button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(Hide);
            }
            lastInput = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            if (ExhibitionIdleReturn.AnyInput())
            {
                lastInput = Time.realtimeSinceStartup;
                if (showing) Hide();
                return;
            }

            if (!showing)
            {
                bool waiting = title != null && title.IsWaitingForVisitor;
                if (!waiting) { lastInput = Time.realtimeSinceStartup; return; }
                if (Time.realtimeSinceStartup - lastInput >= idleSeconds) Show();
                return;
            }

            BlinkHint();

            if (playingVideo)
            {
                bool ended = player.isPrepared && !player.isPlaying && player.frame > 0;
                bool stuck = !player.isPrepared && Time.realtimeSinceStartup - videoStartedAt >= videoPrepareTimeout;
                if (stuck) videoUnavailable = true;
                if (ended || stuck) ShowRanking();
                return;
            }

            if (Time.realtimeSinceStartup - pageStartedAt >= pageSeconds)
            {
                // 種類を一巡したら動画へ
                if (page == Kinds.Length - 1 && !videoUnavailable) { ShowVideo(); return; }
                page = (page + 1) % Kinds.Length;
                pageStartedAt = Time.realtimeSinceStartup;
                Render();
            }
        }

        /// <summary>ゆっくり点滅させる。消え切らせず、薄いところで止めて読めるようにしておく。</summary>
        private void BlinkHint()
        {
            if (returnHint == null) return;
            float t = (Time.realtimeSinceStartup % hintBlinkSeconds) / hintBlinkSeconds;
            returnHint.alpha = Mathf.Lerp(0.2f, 1f, 0.5f + 0.5f * Mathf.Cos(t * Mathf.PI * 2f));
        }

        private void Show()
        {
            showing = true;
            ShowRanking();
            SetVisible(true);
        }

        private void ShowRanking()
        {
            StopVideo();
            page = 0;
            pageStartedAt = Time.realtimeSinceStartup;
            failed = false;
            bool stale = RankingFetcher.LatestAt < 0f || Time.realtimeSinceStartup - RankingFetcher.LatestAt > refreshSeconds;
            if (stale && !fetcher.IsBusy)
            {
                loading = true;
                fetcher.Fetch(Kinds, RowCount, ok =>
                {
                    if (this == null) return;
                    loading = false;
                    failed = !ok && RankingFetcher.Latest.Count == 0;
                    if (showing) Render();
                });
            }
            Render();
        }

        private void ShowVideo()
        {
            if (videoTexture == null)
            {
                videoTexture = new RenderTexture(960, 720, 0) { name = "TitleVideo" };
                videoTexture.Create();
            }
            videoImage.texture = videoTexture;
            player.isLooping = false;
            player.source = VideoSource.Url;
            player.url = System.IO.Path.Combine(Application.streamingAssetsPath, videoPath);
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = videoTexture;
            player.audioOutputMode = VideoAudioOutputMode.Direct;
            player.SetDirectAudioVolume(0, AudioManager.Instance != null ? AudioManager.Instance.BgmOutputVolume : 1f);
            player.errorReceived -= OnVideoError;
            player.errorReceived += OnVideoError;
            // 読み込み中に前の回の最後の絵が残らないよう、黒く消してから出す
            var active = RenderTexture.active;
            RenderTexture.active = videoTexture;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = active;

            if (AudioManager.Instance != null) AudioManager.Instance.FadeOutBgm(0.4f);
            rankingFrame.gameObject.SetActive(false);
            videoLayer.gameObject.SetActive(true);
            playingVideo = true;
            videoStartedAt = Time.realtimeSinceStartup;
            player.Play();
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            Debug.LogWarning("[TitleRankingBoard] 動画を流せませんでした: " + message);
            videoUnavailable = true;
            if (playingVideo) ShowRanking();
        }

        /// <summary>動画を止めてランキングの枠へ戻す。曲は動画のときだけ止めているので、そのときだけ鳴らし直す。</summary>
        private void StopVideo()
        {
            if (rankingFrame != null) rankingFrame.gameObject.SetActive(true);
            if (!playingVideo) return;
            playingVideo = false;
            if (player != null) player.Stop();
            if (videoLayer != null) videoLayer.gameObject.SetActive(false);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayTitleBgm();
        }

        private void OnDestroy()
        {
            if (videoTexture != null) { videoTexture.Release(); Destroy(videoTexture); }
        }

        private void Hide()
        {
            if (!showing) return;
            showing = false;
            StopVideo();
            lastInput = Time.realtimeSinceStartup;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        private void Render()
        {
            kindLabel.text = KindTitles[page];
            kindNote.text = KindNotes[page];
            var dots = new System.Text.StringBuilder();
            for (int i = 0; i < Kinds.Length; i++) dots.Append(i == page ? "■" : "□").Append(i < Kinds.Length - 1 ? " " : "");
            pager.text = dots.ToString();

            RankingFetcher.Latest.TryGetValue(Kinds[page], out RankingData data);
            int count = data != null && data.entries != null ? Mathf.Min(RowCount, data.entries.Length) : 0;
            for (int i = 0; i < RowCount; i++)
            {
                bool has = i < count;
                var e = has ? data.entries[i] : null;
                cells[i, 0].text = has ? e.rank + "位" : "";
                cells[i, 1].text = has ? (string.IsNullOrEmpty(e.name) ? "名無し" : e.name) : "";
                cells[i, 2].text = has ? e.value.ToString("N0") + KindUnits[page] : "";
                Color32 rankColor = !has ? new Color32(232, 220, 200, 255)
                    : e.rank == 1 ? new Color32(230, 196, 90, 255)
                    : e.rank == 2 ? new Color32(200, 200, 212, 255)
                    : e.rank == 3 ? new Color32(198, 132, 82, 255)
                    : new Color32(232, 220, 200, 255);
                cells[i, 0].color = rankColor;
            }

            if (count > 0) status.text = "";
            else if (loading) status.text = "読み込み中…";
            else if (failed) status.text = "ランキングを読み込めませんでした";
            else status.text = "まだ記録がありません";
        }
    }
}
