using KillingMahjong.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// コレクションの「ランキング」タブ（2026-10-23 ユーザー指示「ランキングも追加しておいて」）。
    ///
    /// タイトルの放置画面に出すランキング（<see cref="TitleRankingBoardUI"/>）と同じ中身を、
    /// 種類のボタンで選んで見る。種類・見出し・単位はランキング板の表をそのまま使う。
    /// 中身はサーバーから取る（<see cref="RankingFetcher"/>）。タブを開いたとき、前に取ってから
    /// 60 秒たっていれば取り直す。「読み込み直す」で今すぐ取り直せる。
    /// </summary>
    public sealed partial class CollectionUI
    {
        private const int RankingTab = 7;
        private const int RankingRows = 10;
        private const float RankingRefreshSeconds = 60f;

        private GameObject rankingPage;
        private RankingFetcher rankingFetcher;
        private readonly TextMeshProUGUI[,] rankingCells = new TextMeshProUGUI[RankingRows, 3];
        private readonly System.Collections.Generic.List<Image> rankingKindButtons = new System.Collections.Generic.List<Image>();
        private TextMeshProUGUI rankingNote, rankingStatus;
        private int rankingKind;
        private bool rankingLoading, rankingFailed;

        private void BuildRankingPage(Transform parent)
        {
            rankingKindButtons.Clear();
            var kinds = TitleRankingBoardUI.KindTitles;
            for (int i = 0; i < kinds.Length; i++)
            {
                int index = i;
                Button(parent, "RankingKind" + i, kinds[i], new Vector2(-200f + i * 200f, 158f), new Vector2(180f, 30f), 16f,
                    () => SelectRankingKind(index));
                rankingKindButtons.Add(parent.Find("RankingKind" + i).GetComponent<Image>());
            }
            rankingNote = Label(parent, "RankingNote", "", new Vector2(0f, 128f), new Vector2(600f, 18f),
                12f, TextAlignmentOptions.Center, TextDim);

            for (int i = 0; i < RankingRows; i++)
            {
                var row = NewImage(parent, "RankingRow" + (i + 1),
                    i % 2 == 0 ? new Color32(36, 22, 32, 255) : new Color32(28, 18, 26, 255));
                Center(row.rectTransform, new Vector2(560f, 26f));
                row.rectTransform.anchoredPosition = new Vector2(0f, 100f - i * 28f);
                row.raycastTarget = false;
                rankingCells[i, 0] = Label(row.transform, "Rank", "", new Vector2(-222f, 0f), new Vector2(80f, 26f),
                    18f, TextAlignmentOptions.Right, TextMain);
                rankingCells[i, 1] = Label(row.transform, "Name", "", new Vector2(-34f, 0f), new Vector2(260f, 26f),
                    18f, TextAlignmentOptions.Left, TextMain);
                rankingCells[i, 2] = Label(row.transform, "Value", "", new Vector2(190f, 0f), new Vector2(160f, 26f),
                    18f, TextAlignmentOptions.Right, TextMain);
                // 名前に < が入っても、文字の飾りとして読まない
                rankingCells[i, 1].richText = false;
            }
            rankingStatus = Label(parent, "RankingStatus", "", new Vector2(0f, -26f), new Vector2(560f, 28f),
                18f, TextAlignmentOptions.Center, TextMain);

            Button(parent, "RankingReload", "読み込み直す", new Vector2(0f, -216f), new Vector2(180f, 32f), 16f,
                () => FetchRanking(true));

            rankingFetcher = parent.gameObject.AddComponent<RankingFetcher>();
        }

        /// <summary>タブを開いたとき。古ければ取り直してから出す</summary>
        private void ShowRankingPage()
        {
            SelectRankingKind(rankingKind);
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
            rankingFetcher.Fetch(TitleRankingBoardUI.Kinds, RankingRows, ok =>
            {
                if (this == null) return;
                rankingLoading = false;
                rankingFailed = !ok;
                RenderRanking();
            });
        }

        private void SelectRankingKind(int index)
        {
            rankingKind = Mathf.Clamp(index, 0, TitleRankingBoardUI.Kinds.Length - 1);
            for (int i = 0; i < rankingKindButtons.Count; i++)
                if (rankingKindButtons[i] != null)
                    rankingKindButtons[i].color = i == rankingKind ? (Color)RowSelected : new Color32(52, 30, 42, 255);
            RenderRanking();
        }

        private void RenderRanking()
        {
            if (rankingNote == null) return;
            rankingNote.text = TitleRankingBoardUI.KindNotes[rankingKind];
            RankingFetcher.Latest.TryGetValue(TitleRankingBoardUI.Kinds[rankingKind], out RankingData data);
            int count = data != null && data.entries != null ? Mathf.Min(RankingRows, data.entries.Length) : 0;
            string unit = TitleRankingBoardUI.KindUnits[rankingKind];
            for (int i = 0; i < RankingRows; i++)
            {
                bool has = i < count;
                var e = has ? data.entries[i] : null;
                rankingCells[i, 0].text = has ? e.rank + "位" : "";
                rankingCells[i, 1].text = has ? (string.IsNullOrEmpty(e.name) ? "名無し" : e.name) : "";
                rankingCells[i, 2].text = has ? e.value.ToString("N0") + unit : "";
                // 1〜3位の色はランキング板と同じ
                rankingCells[i, 0].color = !has ? TextMain
                    : e.rank == 1 ? new Color32(230, 196, 90, 255)
                    : e.rank == 2 ? new Color32(200, 200, 212, 255)
                    : e.rank == 3 ? new Color32(198, 132, 82, 255)
                    : TextMain;
            }

            if (count > 0) rankingStatus.text = "";
            else if (rankingLoading) rankingStatus.text = "読み込み中…";
            else if (rankingFailed) rankingStatus.text = "ランキングを読み込めませんでした";
            else rankingStatus.text = "まだ記録がありません";
        }
    }
}
