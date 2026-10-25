using KillingMahjong.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// コレクションの「ランキング」タブ（2026-10-23 ユーザー指示「ランキングも追加しておいて」）。
    ///
    /// タイトルの放置画面に出すランキング（<see cref="TitleRankingBoardUI"/>）と同じ中身を見る。
    /// 2026-10-25 にタイトルのほうが「対人戦スコア」の1つの表（<see cref="VersusRanking"/>）になったので、
    /// こちらも種類のボタンをやめて、同じ表を上位10人まで一度に並べる。見出しはランキング板の表をそのまま使う。
    /// 中身はサーバーから取る（<see cref="RankingFetcher"/>）。タブを開いたとき、前に取ってから
    /// 60 秒たっていれば取り直す。「読み込み直す」で今すぐ取り直せる。
    /// </summary>
    public sealed partial class CollectionUI
    {
        private const int RankingTab = 7;
        private const int RankingRows = 10;
        private const float RankingRefreshSeconds = 60f;

        /// <summary>列の位置（行の真ん中から）。順位・名前（左寄せの左端）・トータル点・対局数・最大ダメ</summary>
        private static readonly float[] RankingColumnX = { -250f, -220f, 40f, 150f, 240f };

        private GameObject rankingPage;
        private RankingFetcher rankingFetcher;
        private readonly TextMeshProUGUI[,] rankingCells = new TextMeshProUGUI[RankingRows, 5];
        private TextMeshProUGUI rankingStatus;
        private bool rankingLoading, rankingFailed;

        private void BuildRankingPage(Transform parent)
        {
            var titles = TitleRankingBoardUI.ColumnTitles;
            for (int c = 0; c < titles.Length; c++)
            {
                RankingCell(parent, "RankingHead" + c, titles[c], c + 1, 128f, 14f, TextDim);
            }

            for (int i = 0; i < RankingRows; i++)
            {
                var row = NewImage(parent, "RankingRow" + (i + 1),
                    i % 2 == 0 ? new Color32(36, 22, 32, 255) : new Color32(28, 18, 26, 255));
                Center(row.rectTransform, new Vector2(560f, 26f));
                row.rectTransform.anchoredPosition = new Vector2(0f, 100f - i * 28f);
                row.raycastTarget = false;
                for (int c = 0; c < 5; c++)
                {
                    rankingCells[i, c] = RankingCell(row.transform, "Cell" + c, "", c, 0f, 18f, TextMain);
                }
                // 名前に < が入っても、文字の飾りとして読まない
                rankingCells[i, 1].richText = false;
            }
            rankingStatus = Label(parent, "RankingStatus", "", new Vector2(0f, -26f), new Vector2(560f, 28f),
                18f, TextAlignmentOptions.Center, TextMain);

            Button(parent, "RankingReload", "読み込み直す", new Vector2(0f, -216f), new Vector2(180f, 32f), 16f,
                () => FetchRanking(true));

            rankingFetcher = parent.gameObject.AddComponent<RankingFetcher>();
        }

        /// <summary>列 <paramref name="column"/>（0＝順位）の字を置く。名前は左寄せ、ほかは真ん中</summary>
        private TextMeshProUGUI RankingCell(Transform parent, string name, string text, int column, float y, float size, Color color)
        {
            bool isName = column == 1;
            var cellSize = isName ? new Vector2(250f, 26f) : new Vector2(100f, 26f);
            float x = isName ? RankingColumnX[1] + cellSize.x * .5f : RankingColumnX[column];
            return Label(parent, name, text, new Vector2(x, y), cellSize, size,
                isName ? TextAlignmentOptions.Left : TextAlignmentOptions.Center, color);
        }

        /// <summary>タブを開いたとき。古ければ取り直してから出す</summary>
        private void ShowRankingPage()
        {
            RenderRanking();
            FetchRanking(false);
        }

        private void FetchRanking(bool force)
        {
            if (rankingFetcher == null || rankingFetcher.IsBusy) return;
            bool stale = RankingFetcher.LatestAt < 0f || Time.realtimeSinceStartup - RankingFetcher.LatestAt > RankingRefreshSeconds;
            if (!force && !stale) return;
            rankingLoading = true;
            rankingFailed = false;
            RenderRanking();
            rankingFetcher.Fetch(VersusRanking.Kinds, VersusRanking.FetchLimit, ok =>
            {
                if (this == null) return;
                rankingLoading = false;
                rankingFailed = !ok;
                RenderRanking();
            });
        }

        private void RenderRanking()
        {
            if (rankingStatus == null) return;
            var rows = VersusRanking.Assemble(RankingFetcher.Latest, RankingRows);
            for (int i = 0; i < RankingRows; i++)
            {
                bool has = i < rows.Count;
                var r = has ? rows[i] : null;
                rankingCells[i, 0].text = has ? r.Rank + "位" : "";
                rankingCells[i, 1].text = has ? (string.IsNullOrEmpty(r.Name) ? "名無し" : r.Name) : "";
                rankingCells[i, 2].text = has ? r.Total.ToString("N0") : "";
                rankingCells[i, 3].text = has ? (r.HasRounds ? r.Rounds.ToString("N0") : "-") : "";
                rankingCells[i, 4].text = has ? (r.HasMaxDamage ? r.MaxDamage.ToString("N0") : "-") : "";
            }

            if (rows.Count > 0) rankingStatus.text = "";
            else if (rankingLoading) rankingStatus.text = "読み込み中…";
            else if (rankingFailed) rankingStatus.text = "ランキングを読み込めませんでした";
            else rankingStatus.text = "まだ記録がありません";
        }
    }
}
