using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 「この牌が通った＝相手の待ちではなかった」という推理を可視化する。
    ///
    /// ツモが無いこのゲームでは毎ターン新しい情報が入らず、打牌が作業になりやすい。
    /// 実際には「通った」という事実そのものが情報なので、それを数として見せる。
    ///
    /// **ここで勝敗やロンの判定は一切しない。** 判定はサーバーの担当。
    /// 見えている事実（誰が何を切ったか）から候補を消していくだけの表示用ロジック。
    ///
    /// 候補が減る理由は2つ:
    ///   1. 自分が切って通った  → その牌種は相手の待ちではない
    ///   2. 相手自身が切った    → フリテンになるので、その牌種では和了れない
    /// </summary>
    public class WaitDeductionUI : MonoBehaviour
    {
        /// <summary>牌種は 0〜28（萬子9・筒子9・索子9・東・西）。</summary>
        private const int KindCount = 29;

        private readonly HashSet<int> _eliminated = new HashSet<int>();

        private RectTransform _panel;
        private TextMeshProUGUI _countText;
        private TextMeshProUGUI _flashText;
        private Coroutine _flashRoutine;

        [Header("表示")]
        // 左側は上から「設定 → ターン表示 → ドラ → 待ち候補」の順に並ぶ。
        // 設定ボタン(Y=490..590)やドラと当たらない位置を実機で詰めた値。
        [Tooltip("画面上の位置（Canvas中央からの相対）")]
        [SerializeField] private Vector2 anchoredPos = new Vector2(-292f, -18f);
        [Tooltip("通ったときの一言を出す")]
        [SerializeField] private bool showFlash = true;

        private TMP_FontAsset _font;

        /// <summary>残っている待ち候補の数。</summary>
        public int RemainingCandidates => KindCount - _eliminated.Count;

        private void Awake()
        {
            Build();
            Refresh();
        }

        private void Build()
        {
            var canvasGo = new GameObject("WaitDeductionCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 盤面の情報表示なので「場の血」と同じ段に置く。
            // これより手前にするとフェーズ演出の黒帯を突き抜けて残ってしまう。
            canvas.sortingOrder = KillingMahjong.Common.UISortingOrders.BetPot;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800, 600);
            scaler.matchWidthOrHeight = 0f;

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            _panel = panelGo.GetComponent<RectTransform>();
            _panel.SetParent(canvasGo.transform, false);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(178f, 40f);
            _panel.anchoredPosition = anchoredPos;

            var bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.04f, 0.06f, 0.82f);
            bg.raycastTarget = false;
            var ol = panelGo.AddComponent<Outline>();
            ol.effectColor = new Color(0.8f, 0.15f, 0.15f, 0.7f);
            ol.effectDistance = new Vector2(2f, -2f);

            _countText = MakeText(_panel, "count", new Vector2(0f, 0f), 15f, TextAlignmentOptions.Center);
            _flashText = MakeText(_panel, "flash", new Vector2(0f, -34f), 12f, TextAlignmentOptions.Center);
            _flashText.color = new Color(1f, 0.75f, 0.35f, 0f);
        }

        private TextMeshProUGUI MakeText(RectTransform parent, string name, Vector2 pos, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(300f, 40f);
            rt.anchoredPosition = pos;

            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            if (_font != null) t.font = _font;
            return t;
        }

        /// <summary>局が変わったら推理をやり直す。</summary>
        public void ResetForNewRound()
        {
            _eliminated.Clear();
            Refresh();
            if (_flashText != null) _flashText.color = new Color(1f, 0.75f, 0.35f, 0f);
        }

        /// <summary>
        /// 打牌を1つ取り込む。通った牌・相手が切った牌のどちらも候補を減らす。
        /// </summary>
        public void RegisterDiscard(int tileId, bool isLocalPlayer)
        {
            if (tileId < 0) return;                 // -1 は無効値。0 は一萬なので弾かない
            int kind = tileId & 0x1F;               // ドラ/赤ドラのビットを落として牌種にする
            if (kind < 0 || kind >= KindCount) return;

            if (!_eliminated.Add(kind)) return;     // 既に判明済みなら何もしない

            Refresh();
            if (showFlash) Flash(kind, isLocalPlayer);
        }

        private void Refresh()
        {
            if (_countText == null) return;
            int remain = RemainingCandidates;
            // 残りが少ないほど赤くして、締め付けられている感じを出す
            float t = 1f - Mathf.Clamp01(remain / (float)KindCount);
            var c = Color.Lerp(new Color(0.85f, 0.9f, 1f), new Color(1f, 0.35f, 0.3f), t);
            _countText.color = c;
            _countText.text = $"相手の待ち候補  残り {remain} 種";
        }

        private void Flash(int kind, bool isLocalPlayer)
        {
            string name = new TileData(kind).GetTileName();
            string msg = isLocalPlayer
                ? $"{name} が通った"
                : $"相手が {name} を切った";
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine($"{msg} → 候補 {RemainingCandidates} 種"));
        }

        private IEnumerator FlashRoutine(string msg)
        {
            _flashText.text = msg;
            float t = 0f;
            while (t < 0.15f) { t += Time.deltaTime; SetFlashAlpha(t / 0.15f); yield return null; }
            SetFlashAlpha(1f);
            yield return new WaitForSeconds(1.4f);
            t = 0f;
            while (t < 0.5f) { t += Time.deltaTime; SetFlashAlpha(1f - t / 0.5f); yield return null; }
            SetFlashAlpha(0f);
            _flashRoutine = null;
        }

        private void SetFlashAlpha(float a)
        {
            var c = _flashText.color; c.a = Mathf.Clamp01(a); _flashText.color = c;
        }

        public void SetVisible(bool v)
        {
            if (_panel != null) _panel.gameObject.SetActive(v);
        }
    }
}
