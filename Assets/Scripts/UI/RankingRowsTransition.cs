using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>ランキングの帯を残し、走査光に合わせて上から数字を出す。データ取得は持たない。</summary>
    public sealed class RankingRowsTransition : MonoBehaviour
    {
        public const string EffectId = "title.ranking_rows";
        private const float LeaveSeconds = .20f, EmptySeconds = .45f;
        private const float LeadSeconds = .18f, RowStep = .10f, RevealSeconds = .16f;
        private RectTransform[] rows;
        private TMP_Text[,] labels;
        private CanvasGroup[] groups;
        private float[] originalGroupAlpha;
        private float[,] originalTextAlpha;
        private Image scan;
        private int version;
        public bool IsPlaying { get; private set; }

        public void Initialize(RectTransform[] rowRects, TMP_Text[,] texts)
        {
            Cancel();
            rows = rowRects;
            labels = texts;
            groups = new CanvasGroup[rows.Length];
            originalGroupAlpha = new float[rows.Length];
            originalTextAlpha = new float[texts.GetLength(0), texts.GetLength(1)];
            for (int r = 0; r < rows.Length; r++)
            {
                groups[r] = rows[r].GetComponent<CanvasGroup>();
                if (groups[r] == null) groups[r] = rows[r].gameObject.AddComponent<CanvasGroup>();
                originalGroupAlpha[r] = groups[r].alpha;
                for (int c = 0; c < labels.GetLength(1); c++) originalTextAlpha[r, c] = labels[r, c].alpha;
            }
            if (scan == null)
            {
                var go = new GameObject("RankingScan", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                scan = go.GetComponent<Image>();
                // 白い文字と同じ色を使う。帯の赤・黄の色や材質は書き換えない。
                scan.color = Color.white;
                scan.raycastTarget = false;
                scan.rectTransform.sizeDelta = new Vector2(rows[0].rect.width - 28f, 6f);
            }
            scan.gameObject.SetActive(false);
        }

        public IEnumerator Enter() { yield return Run(false, false, null); }
        public IEnumerator Change(Action bindNextPage) { yield return Run(true, false, bindNextPage); }
        public IEnumerator Exit() { yield return Run(true, true, null); }

        private IEnumerator Run(bool leave, bool exit, Action bind)
        {
            Cancel();
            int stamp = version;
            IsPlaying = true;
            if (!leave) PrepareEntrance();
            try
            {
                if (leave) yield return Leave(stamp);
                if (stamp != version) yield break;
                if (exit) yield break;
                if (leave)
                    for (float t = 0f; t < EmptySeconds && stamp == version; t += Time.unscaledDeltaTime)
                        yield return null;
                if (stamp != version) yield break;
                bind?.Invoke();
                yield return Reveal(stamp);
            }
            finally
            {
                if (stamp == version) { Restore(); IsPlaying = false; }
            }
        }

        private IEnumerator Leave(int stamp)
        {
            for (float t = 0f; t < LeaveSeconds && stamp == version; t += Time.unscaledDeltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / LeaveSeconds);
                for (int r = 0; r < rows.Length; r++) SetRow(r, Mathf.Lerp(1f, .28f, u), 1f - u);
                yield return null;
            }
            if (stamp == version)
                for (int r = 0; r < rows.Length; r++) SetRow(r, .28f, 0f);
        }

        private IEnumerator Reveal(int stamp)
        {
            int count = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                SetRow(r, .28f, 0f);
                if (rows[r].gameObject.activeSelf) count = r + 1;
            }
            if (count == 0) yield break;
            float duration = LeadSeconds + (count - 1) * RowStep + RevealSeconds + .04f;
            float top = rows[0].anchoredPosition.y + rows[0].rect.height * .5f;
            float bottom = rows[count - 1].anchoredPosition.y - rows[count - 1].rect.height * .5f;
            for (float t = 0f; t < duration && stamp == version; t += Time.unscaledDeltaTime)
            {
                for (int r = 0; r < rows.Length; r++)
                {
                    float local = t - LeadSeconds - r * RowStep;
                    float band = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(local / .08f));
                    float text = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((local - .04f) / RevealSeconds));
                    SetRow(r, Mathf.Lerp(.28f, 1f, band), text);
                }
                float travel = Mathf.Clamp01((t - LeadSeconds) / ((count - 1) * RowStep + RevealSeconds));
                scan.gameObject.SetActive(t >= LeadSeconds && t < duration - .04f);
                scan.rectTransform.anchoredPosition = new Vector2(rows[0].anchoredPosition.x, Mathf.Round(Mathf.Lerp(top, bottom, travel)));
                scan.color = new Color(1f, 1f, 1f, .65f * Mathf.Min(
                    Mathf.Clamp01((t - LeadSeconds) / .04f), Mathf.Clamp01((duration - .04f - t) / .08f)));
                yield return null;
            }
        }

        public void PrepareEntrance()
        {
            if (groups != null)
                for (int r = 0; r < groups.Length; r++) SetRow(r, .28f, 0f);
            if (scan != null) scan.gameObject.SetActive(false);
        }

        private void SetRow(int r, float band, float text)
        {
            if (groups[r] != null) groups[r].alpha = originalGroupAlpha[r] * band;
            for (int c = 0; c < labels.GetLength(1); c++)
                if (labels[r, c] != null) labels[r, c].alpha = originalTextAlpha[r, c] * text;
        }

        private void Restore()
        {
            if (groups != null)
                for (int r = 0; r < groups.Length; r++) SetRow(r, 1f, 1f);
            if (scan != null) scan.gameObject.SetActive(false);
        }

        public void Cancel() { version++; Restore(); IsPlaying = false; }
        private void OnDisable() { Cancel(); }
    }
}
