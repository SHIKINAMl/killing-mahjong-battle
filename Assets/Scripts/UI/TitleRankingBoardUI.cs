using KillingMahjong.Network;
using TMPro;
using KillingMahjong.Managers;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 展示のタイトル画面＝ランキングと紹介動画（2026-10-21 にタイトルの放置画面として作り、
    /// 2026-10-23 にプランナー指示「もう動画とランキング画面がタイトル画面というイメージ」でタイトルそのものにした）。
    ///
    /// 展示のタイトル（どこを押しても始まる画面）に、最初から全面に出しておく。押せばそのままチュートリアルが始まる
    /// （クリックはこの板では受けず、手前の全画面クリック受付が受ける。並び順は受付の奥）。
    /// 受付の上の「ルールを知っている方：対局へ」「デバッグ部屋」のボタンは、この板より手前に見えて押せる。
    /// 対局を選ぶ画面や設定を開いたら引っ込め、閉じたらまたランキングの最初から出す。
    /// 以前は 25 秒放置したら出し、何か操作したらタイトルの絵へ戻していた。
    ///
    /// **動画が流れ始めたら、展示用リセットと同じ物を消す**（<see cref="Core.ExhibitionReset.ClearSaved"/>。
    /// 同日のプランナー指示「動画流れ始めたらリセットボタンをオートで押すのもアリ」）。
    /// ランキングを一巡するあいだ（約 24 秒）誰も触らなかったら、前の人は帰ったとみなす。
    ///
    /// **ランキングは「対人戦スコア」の1つの表**（2026-10-25、プランナー yabazaemon さんの見本の絵）。
    /// 以前は種類（最大打点・累計獲得点数・最大局数）を1つずつ切り替えて見せていた。
    /// 1人1行で 名前・トータル点・対局数・最大ダメ を並べ、赤い帯（<see cref="BarSpritePath"/>）に載せる。
    /// 上位10人を5人ずつ2回に分けて見せる。並べ方とつなぎの事情は <see cref="VersusRanking"/>。
    /// 見本の通り、暗い板や枠は敷かず、部屋の絵の上にじかに置く。
    ///
    /// **ランキングと紹介動画を交互に出す**（2026-10-22、ユーザー指示「放置していた場合ランキングと動画交互に出てほしい」）。
    /// ランキングを一巡したら動画、動画が終わったらまたランキング。
    /// 動画は紹介PV（`StreamingAssets/Video/title_pv.mp4`）。WebGL でも読めるよう、Resources ではなく
    /// StreamingAssets から URL で流す。流しているあいだはタイトルの曲を止めて動画の音を出す。
    /// 読めなかったときはランキングだけを回す。
    ///
    /// どちらを出している間も、下に「クリックでスタート」をゆっくり点滅させて出しておく
    /// （2026-10-22 のユーザー指示。そのときは「クリックでタイトルへ」）。
    ///
    /// 中身はサーバーから取る（<see cref="RankingFetcher"/>）。対局の接続とは別に繋ぎ、終わったら切る。
    ///
    /// **画面の部品はシーンに置く**（<see cref="SceneFirst"/>）。<see cref="Ensure"/> は「探して、無ければ作る」。
    /// 位置や色はシーンのほうが正で、ここの数字はシーンに無かったときの控え。
    /// </summary>
    public sealed partial class TitleRankingBoardUI : MonoBehaviour
    {
        public const string RootName = "TitleRankingBoard";

        /// <summary>載せる人数と、1回に見せる人数（帯の本数）。</summary>
        private const int RowCount = 10, RowsPerPage = 5;

        /// <summary>帯の絵。見本の絵から測った形を1ドットずつ組んだもの（黒の縁・黄色の線・赤の塗り、両端がとがる）。</summary>
        public const string BarSpritePath = "UI/Ranking/ranking_bar";

        /// <summary>見出しと表の字。見本の絵の太いドットの字</summary>
        private const string FontPath = "x8y12pxDenkiChip/x8y12pxDenkiChip_Dynamic";

        /// <summary>1回（5人）を見せる秒数。</summary>
        [SerializeField] private float pageSeconds = 8f;

        /// <summary>前に取ってから何秒たっていたら取り直すか（サーバーも上位の一覧を 30 秒ためている）。</summary>
        [SerializeField] private float refreshSeconds = 60f;

        /// <summary>流す動画。StreamingAssets からの場所。</summary>
        [SerializeField] private string videoPath = "Video/title_pv.mp4";

        /// <summary>動画の読み込みを何秒待つか。過ぎたら、その回は飛ばしてランキングへ戻る。</summary>
        [SerializeField] private float videoPrepareTimeout = 8f;

        /// <summary>「クリックでスタート」の点滅の1往復の秒数。ゆっくり（ユーザー指示）。</summary>
        [SerializeField] private float hintBlinkSeconds = 2.4f;

        /// <summary>表の見出し（見本の絵の文言どおり）。列の並びは <see cref="ColumnX"/> と同じ</summary>
        internal static readonly string[] ColumnTitles = { "NAME", "トータル点", "対局数", "最大ダメ" };

        /// <summary>列の位置（真ん中から）。名前だけは左寄せで、その左端。ほかは字の真ん中</summary>
        private static readonly float[] ColumnX = { -301f, 6f, 152f, 278f };

        private static readonly string[] ColumnNames = { "Name", "Total", "Games", "Damage" };

        private TitleUIManager title;
        private CanvasGroup group;
        private RankingFetcher fetcher;
        private TMP_Text status, returnHint;
        private RectTransform rankingFrame, videoLayer;
        private RawImage videoImage;
        private VideoPlayer player;
        private RenderTexture videoTexture;
        private bool playingVideo, videoUnavailable;
        private float videoStartedAt;
        private readonly RectTransform[] bars = new RectTransform[RowsPerPage];
        private readonly TMP_Text[,] cells = new TMP_Text[RowsPerPage, 5];

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

        /// <summary>列 <paramref name="column"/> の字の置き場（行の真ん中から）。名前は左寄せなので、左端が <see cref="ColumnX"/> に来るよう幅の半分ずらす</summary>
        private static Vector2 CellPosition(int column)
        {
            return column == 0 ? new Vector2(ColumnX[0] + CellSize(0).x * .5f, 0f) : new Vector2(ColumnX[column], 0f);
        }

        private static Vector2 CellSize(int column) => column == 0 ? new Vector2(250f, 40f) : new Vector2(130f, 40f);

        private void Build()
        {
            var bodyFont = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed");
            group = GetComponent<CanvasGroup>();
            fetcher = GetComponent<RankingFetcher>();
            if (fetcher == null) fetcher = gameObject.AddComponent<RankingFetcher>();

            var font = Resources.Load<TMP_FontAsset>(FontPath) ?? bodyFont;
            var bar = Resources.Load<Sprite>(BarSpritePath);
            var white = new Color32(255, 255, 255, 255);

            // 表をまとめる入れ物（動画のあいだはこれごと隠す）。見本どおり、暗い板や枠は敷かない
            var scores = Box(transform, "Scores", new Vector2(800f, 600f), Vector2.zero, Color.clear);
            scores.GetComponent<Image>().raycastTarget = false;
            rankingFrame = scores;

            Label(scores, "Heading", "対人戦スコア", new Vector2(0f, 249f), new Vector2(600f, 56f), 48f,
                white, TextAlignmentOptions.Center, font);
            var head = Box(scores, "ColumnHeads", new Vector2(800f, 40f), new Vector2(0f, 163f), Color.clear);
            head.GetComponent<Image>().raycastTarget = false;
            for (int c = 0; c < ColumnTitles.Length; c++)
            {
                Label(head, "Head" + c, ColumnTitles[c], CellPosition(c), CellSize(c), 24f, white,
                    c == 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, font);
            }

            // 帯は 382×32 ドット（1ドット＝2単位）。見本は 78 間隔だが、5本目が下の「クリックでスタート」の帯に
            // かからないよう 76 間隔にした
            for (int i = 0; i < RowsPerPage; i++)
            {
                bool madeBar = transform.Find("Scores/Row" + (i + 1)) == null;
                var row = Box(scores, "Row" + (i + 1), new Vector2(764f, 64f), new Vector2(0f, 89f - i * 76f), white);
                var image = row.GetComponent<Image>();
                image.raycastTarget = false;
                if (madeBar && bar != null)
                {
                    image.sprite = bar;
                    image.type = Image.Type.Sliced;
                }
                bars[i] = row;
                // 順位は、帯の左のとがった所と名前のあいだに置く
                cells[i, 0] = Label(row, "Rank", "", new Vector2(-340f, 0f), new Vector2(40f, 40f), 24f, white,
                    TextAlignmentOptions.Center, font);
                for (int c = 0; c < ColumnTitles.Length; c++)
                {
                    cells[i, c + 1] = Label(row, ColumnNames[c], "", CellPosition(c), CellSize(c), 24f, white,
                        c == 0 ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, font);
                }
                // 名前に < が入っても、文字の飾りとして読まない
                cells[i, 1].richText = false;
            }

            rankingMotion = scores.GetComponent<RankingRowsTransition>();
            if (rankingMotion == null) rankingMotion = scores.gameObject.AddComponent<RankingRowsTransition>();
            rankingMotion.Initialize(bars, cells);

            status = Label(scores, "Status", "", new Vector2(0f, -40f), new Vector2(600f, 32f), 24f,
                white, TextAlignmentOptions.Center, font);
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

            // 下の「クリックでスタート」。ランキングでも動画でも出しっぱなしにして、ゆっくり点滅させる。
            // いちばん手前に描くよう、最後の子にしておく。画面の縦横比が変わっても下に出るよう、下端に揃えて置く。
            // 横いっぱいの暗い帯を敷き、その上に字と、手前の「対局へ」「デバッグ部屋」のボタンを横に並べる
            // （並びはシーンで決めてある。字は左寄り）
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
            returnHint = Label(transform, "ReturnHint", "クリックでスタート", Vector2.zero, new Vector2(300f, 28f), 20f,
                new Color32(232, 220, 200, 255), TextAlignmentOptions.Center, bodyFont);
            if (hintMade)
            {
                var hintRt = returnHint.rectTransform;
                hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 0f);
                hintRt.anchoredPosition = new Vector2(-210f, 28f);
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

        private void Update()
        {
            bool waiting = title != null && title.IsWaitingForVisitor;
            if (!showing)
            {
                if (waiting) Show();
                return;
            }
            // 対局を選ぶ画面や設定を開いたら引っ込める。チュートリアルなどへ移る暗転のあいだは出したままにする
            // （引っ込めると、後ろの古いタイトルの絵が一瞬見える）
            if (!waiting && (title == null || !title.IsLeavingScene)) { Hide(); return; }

            BlinkHint();

            if (playingVideo)
            {
                bool ended = player.isPrepared && !player.isPlaying && player.frame > 0;
                // 読み込めない・流れ始めないときは、その回だけ飛ばす。次の一巡でまた試す。
                // 読み込めても流れないことがある（Web 版で、ブラウザに再生を断られたとき。真っ黒のまま止まっていた）
                bool stuck = !(player.isPlaying || player.frame > 0)
                    && Time.realtimeSinceStartup - videoStartedAt >= videoPrepareTimeout;
                if (ended || stuck) ShowRanking();
                return;
            }

            if (rankingInTransition) return;
            if (Time.realtimeSinceStartup - pageStartedAt >= pageSeconds)
            {
                // 全員を見せ終えたら動画へ
                int pages = PageCount();
                if (page >= pages - 1 && !videoUnavailable) { StartVideoTransition(); return; }
                ChangeRankingPage((page + 1) % pages);
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
            CancelRankingMotion();
            StopVideo();
            page = 0;
            pageStartedAt = Time.realtimeSinceStartup;
            failed = false;
            bool stale = RankingFetcher.LatestAt < 0f || Time.realtimeSinceStartup - RankingFetcher.LatestAt > refreshSeconds;
            if (stale && !fetcher.IsBusy)
            {
                loading = true;
                fetcher.Fetch(VersusRanking.Kinds, VersusRanking.FetchLimit, ok =>
                {
                    if (this == null) return;
                    loading = false;
                    failed = !ok && RankingFetcher.Latest.Count == 0;
                    if (showing)
                    {
                        if (!playingVideo) CancelRankingMotion();
                        Render();
                        if (!playingVideo) StartRankingEntrance();
                    }
                });
            }
            Render();
            StartRankingEntrance();
        }

        private void ShowVideo()
        {
            // ランキングを一巡するあいだ誰も触らなかったので、次の人のために名前・記録・チュートリアルの進み具合を消す
            Core.ExhibitionReset.ClearSaved();
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
            // 起動してまだ誰も押していないと、ブラウザは音つきの動画を流させない（真っ黒のまま止まった）。
            // そのあいだは音を消して流す。誰かが押したあとの回からは音を出す
            player.audioOutputMode = PageHasUserActivation() ? VideoAudioOutputMode.Direct : VideoAudioOutputMode.None;
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

        /// <summary>Web 版で、このページを誰かが一度でも押したか。Web 版以外はいつも true</summary>
        private static bool PageHasUserActivation()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return KmHasUserActivation() != 0;
#else
            return true;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int KmHasUserActivation();
#endif

        private void OnDestroy()
        {
            CancelRankingMotion();
            if (videoTexture != null) { videoTexture.Release(); Destroy(videoTexture); }
        }

        private void Hide()
        {
            if (!showing) return;
            showing = false;
            CancelRankingMotion();
            StopVideo();
            SetVisible(false);
        }

        /// <summary>見せるだけで、クリックは受けない（手前の全画面クリック受付へ通す）</summary>
        private void SetVisible(bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        private System.Collections.Generic.List<VersusRankingRow> rows = new System.Collections.Generic.List<VersusRankingRow>();

        /// <summary>何回に分けて見せるか（5人ずつ）。誰もいなくても1回</summary>
        private int PageCount() => Mathf.Max(1, (rows.Count + RowsPerPage - 1) / RowsPerPage);

        private void Render()
        {
            rows = VersusRanking.Assemble(RankingFetcher.Latest, RowCount);
            page = Mathf.Clamp(page, 0, PageCount() - 1);
            for (int i = 0; i < RowsPerPage; i++)
            {
                int index = page * RowsPerPage + i;
                bool has = index < rows.Count;
                // 誰もいない帯は出さない（空の帯が並ぶと、壊れて見える）
                bars[i].gameObject.SetActive(has);
                if (!has) continue;
                var r = rows[index];
                cells[i, 0].text = r.Rank.ToString();
                cells[i, 1].text = string.IsNullOrEmpty(r.Name) ? "名無し" : r.Name;
                cells[i, 2].text = r.Total.ToString("N0");
                cells[i, 3].text = r.HasRounds ? r.Rounds.ToString("N0") : "-";
                cells[i, 4].text = r.HasMaxDamage ? r.MaxDamage.ToString("N0") : "-";
            }

            if (rows.Count > 0) status.text = "";
            else if (loading) status.text = "読み込み中…";
            else if (failed) status.text = "ランキングを読み込めませんでした";
            else status.text = "まだ記録がありません";
        }
    }
}
