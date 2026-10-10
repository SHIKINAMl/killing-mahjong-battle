using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 13枚選択時にサーバーから返った聴牌結果を表示するだけの実行時UI。
    /// 対局シーンは本編とチュートリアルの2つあるため、どちらのシーンにも保存しない。
    /// </summary>
    public class TenpaiPreviewUI : MonoBehaviour
    {
        private const float Width = 232f;
        private const float Height = 128f;
        // 4:3 (800x600) の右下寄り。既存のプレイヤー情報・相手情報・左側ゲージを避ける。
        private static readonly Vector2 AnchoredPosition = new Vector2(-16f, -118f);

        private RectTransform _rect;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _summary;
        private TextMeshProUGUI _details;
        private TMP_FontAsset _font;

        public static TenpaiPreviewUI Create()
        {
            var go = new GameObject("TenpaiPreviewUI", typeof(RectTransform));
            // 既存UIの親RectTransformは画面全体ではない。子にするとアンカーが親の
            // 局所座標を基準にしてクリップするため、独立したOverlay Canvasにする。
            // 実行時生成物はアクティブシーンに属するので、シーン遷移時には自動で破棄される。
            return go.AddComponent<TenpaiPreviewUI>();
        }

        private void Awake()
        {
            Build();
            Hide();
        }

        private void Build()
        {
            _font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed");

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = UISortingOrders.InfoPanelHighlight;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.matchWidthOrHeight = 0f;

            // CanvasのRectTransformは常に画面全体へ伸びる。見た目の矩形は子Panelに持たせる。
            var panel = new GameObject("Panel", typeof(RectTransform));
            _rect = panel.GetComponent<RectTransform>();
            _rect.SetParent(transform, false);
            _rect.anchorMin = _rect.anchorMax = new Vector2(1f, 0.5f);
            _rect.pivot = new Vector2(1f, 0.5f);
            _rect.sizeDelta = new Vector2(Width, Height);
            _rect.anchoredPosition = AnchoredPosition;

            var background = panel.AddComponent<Image>();
            background.color = new Color(0.035f, 0.05f, 0.09f, 0.93f);
            background.raycastTarget = false;
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.28f, 0.82f, 1f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            _title = MakeText("Title", new Vector2(8f, -8f), new Vector2(-8f, -30f), 17f, TextAlignmentOptions.Left);
            _summary = MakeText("Summary", new Vector2(8f, -34f), new Vector2(-8f, -55f), 13f, TextAlignmentOptions.Left);
            _details = MakeText("Details", new Vector2(8f, -59f), new Vector2(-8f, -8f), 11f, TextAlignmentOptions.TopLeft);
        }

        private TextMeshProUGUI MakeText(string name, Vector2 topLeft, Vector2 bottomRight,
                                         float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(_rect, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = bottomRight;
            rect.offsetMax = topLeft;

            var text = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) text.font = _font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        public void ShowPending()
        {
            gameObject.SetActive(true);
            _title.text = "聴牌判定";
            _title.color = new Color(0.72f, 0.9f, 1f);
            _summary.text = "サーバーに確認中…";
            _details.text = "13枚の手牌を判定しています";
        }

        public void ShowTenpai(WaitData[] waits)
        {
            gameObject.SetActive(true);
            if (waits == null || waits.Length == 0)
            {
                _title.text = "聴牌";
                _title.color = new Color(0.55f, 1f, 0.72f);
                _summary.text = "待ち牌の情報がありません";
                _details.text = string.Empty;
                return;
            }

            WaitData strongest = waits[0];
            for (int i = 1; i < waits.Length; i++)
            {
                if (waits[i].multiplier > strongest.multiplier
                    || (Mathf.Approximately(waits[i].multiplier, strongest.multiplier)
                        && waits[i].han > strongest.han))
                {
                    strongest = waits[i];
                }
            }

            string strength = FormatStrength(strongest);
            _title.text = $"聴牌  {waits.Length}種待ち";
            _title.color = new Color(0.55f, 1f, 0.72f);
            _summary.text = strongest.multiplier > 0f
                ? $"最高: {strength}  {strongest.han}翻  {strongest.multiplier:0.#}倍"
                : $"最高: {strength}  {strongest.han}翻";
            _details.text = FormatWaitDetails(waits);
        }

        public void ShowNotTenpai(string reason)
        {
            gameObject.SetActive(true);
            _title.text = "不聴";
            _title.color = new Color(1f, 0.54f, 0.48f);
            _summary.text = "聴牌していません";
            _details.text = string.IsNullOrEmpty(reason) ? string.Empty : reason;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private static string FormatStrength(WaitData wait)
        {
            if (!string.IsNullOrEmpty(wait.multiplier_label)) return wait.multiplier_label;
            return wait.mangan_or_more ? "満貫以上" : "満貫未満";
        }

        private static string FormatWaitDetails(WaitData[] waits)
        {
            var lines = new StringBuilder();
            int shown = Mathf.Min(3, waits.Length);
            for (int i = 0; i < shown; i++)
            {
                WaitData wait = waits[i];
                int tileKind = TileId.BaseId(wait.tile);
                string tileName = new TileData(tileKind).GetTileName();
                string yaku = wait.yaku != null && wait.yaku.Length > 0
                    ? string.Join("/", wait.yaku)
                    : "役情報なし";
                if (i > 0) lines.Append('\n');
                lines.Append(tileName).Append("  ").Append(FormatStrength(wait))
                    .Append("  ").Append(wait.han).Append("翻  ").Append(yaku);
            }
            if (waits.Length > shown)
            {
                lines.Append('\n').Append("ほか ").Append(waits.Length - shown).Append("種");
            }
            return lines.ToString();
        }
    }
}
