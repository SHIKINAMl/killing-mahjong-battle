using KillingMahjong.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// タイトルで放置したときに出すランキング（2026-10-21、ユーザー指示「ランキング画面はタイトルで放置している場所に表示する」）。
    ///
    /// 展示のタイトル（どこを押しても始まる画面）で、しばらく操作が無いと全面に出す。
    /// 種類（最大打点・累計獲得点数・最大局数）を順に切り替えて見せ、何か操作したらタイトルへ戻る。
    /// 戻すための操作では、チュートリアルは始めない（いちばん手前で押した物を受け止める）。
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

        private static readonly string[] Kinds = { "max_damage", "match_points", "rounds" };
        private static readonly string[] KindTitles = { "最大打点", "累計獲得点数", "最大局数" };
        private static readonly string[] KindNotes =
        {
            "1回の和了で、相手に与えた損失のいちばん大きいもの",
            "1つの対局で獲得した点数の合計の、いちばん大きいもの",
            "1つの対局で打った局の数の、いちばん多いもの",
        };
        private static readonly string[] KindUnits = { "点", "点", "局" };

        private TitleUIManager title;
        private CanvasGroup group;
        private RankingFetcher fetcher;
        private TMP_Text kindLabel, kindNote, pager, status;
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
            Label(panel, "Hint", "画面を押すとタイトルに戻ります", new Vector2(0f, -212f), new Vector2(520f, 26f), 20f,
                new Color32(170, 156, 140, 255), TextAlignmentOptions.Center, font);

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

            if (Time.realtimeSinceStartup - pageStartedAt >= pageSeconds)
            {
                page = (page + 1) % Kinds.Length;
                pageStartedAt = Time.realtimeSinceStartup;
                Render();
            }
        }

        private void Show()
        {
            showing = true;
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
            SetVisible(true);
        }

        private void Hide()
        {
            if (!showing) return;
            showing = false;
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
