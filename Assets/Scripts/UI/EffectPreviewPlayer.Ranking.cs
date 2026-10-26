using System.Collections;
using TMPro;
using UnityEngine;

namespace KillingMahjong.UI
{
    public sealed partial class EffectPreviewPlayer
    {
        private IEnumerator PreviewRankingRows()
        {
            var prefab = Resources.Load<GameObject>("Presentation/RankingRowsPreview");
            if (prefab == null) throw new System.InvalidOperationException("ランキングの試写用配置がありません。");
            var root = Instantiate(prefab, rig.surface, false);
            root.SetActive(true);
            ((RectTransform)root.transform).anchoredPosition = Vector2.zero;
            root.transform.localScale = Vector3.one * .92f;
            var rows = new RectTransform[5];
            var labels = new TMP_Text[5, 5];
            string[] names = { "Rank", "Name", "Total", "Games", "Damage" };
            for (int r = 0; r < rows.Length; r++)
            {
                rows[r] = (RectTransform)root.transform.Find("Row" + (r + 1));
                for (int c = 0; c < names.Length; c++) labels[r, c] = rows[r].Find(names[c]).GetComponent<TMP_Text>();
            }
            root.transform.Find("Status").gameObject.SetActive(false);
            var motion = root.AddComponent<RankingRowsTransition>();
            motion.Initialize(rows, labels);
            scope.AddCleanup(() => { if (motion != null) motion.Cancel(); if (root != null) Destroy(root); });
            FillRankingPreview(rows, labels, 0);
            yield return motion.Enter();
            yield return new WaitForSecondsRealtime(2f);
            yield return motion.Change(() => FillRankingPreview(rows, labels, 1));
            yield return new WaitForSecondsRealtime(2f);
            yield return motion.Exit();
        }

        private static void FillRankingPreview(RectTransform[] rows, TMP_Text[,] labels, int page)
        {
            string[] totals = { "320,000", "284,000", "250,000", "210,000", "198,000", "176,000", "150,000", "120,000", "98,000", "64,000" };
            for (int r = 0; r < rows.Length; r++)
            {
                int index = page * rows.Length + r;
                rows[r].gameObject.SetActive(true);
                labels[r, 0].text = (index + 1).ToString();
                labels[r, 1].text = "PLAYER " + (char)('A' + index);
                labels[r, 2].text = totals[index];
                labels[r, 3].text = "5";
                labels[r, 4].text = "32,000";
            }
        }
    }
}
