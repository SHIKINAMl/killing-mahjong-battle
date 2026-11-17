using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    public sealed class RonButtonFlow : RawImage
    {
        private const string FramesPath = "UI/RonCallout/ron_button_flow_frames";
        private const int Columns = 6;
        private const int Rows = 6;
        private const float FrameSeconds = .07f;

        private float startedAt;
        private int displayedFrame;

        internal static void Attach(GameObject panel)
        {
            var button = panel.GetComponentInChildren<Button>(true);
            if (button == null || button.GetComponentInChildren<RonButtonFlow>(true) != null) return;

            var frames = Resources.Load<Texture2D>(FramesPath);
            if (frames == null) return;

            var layer = new GameObject("RonButtonFlow", typeof(RectTransform), typeof(RonButtonFlow));
            layer.transform.SetParent(button.transform, false);
            var flow = layer.GetComponent<RonButtonFlow>();
            flow.texture = frames;
            flow.raycastTarget = false;
            flow.rectTransform.anchorMin = Vector2.zero;
            flow.rectTransform.anchorMax = Vector2.one;
            flow.rectTransform.offsetMin = Vector2.zero;
            flow.rectTransform.offsetMax = Vector2.zero;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            startedAt = Time.unscaledTime;
            ShowFrame(0);
        }

        private void LateUpdate()
        {
            int frame = Mathf.FloorToInt((Time.unscaledTime - startedAt) / FrameSeconds) % (Columns * Rows);
            if (frame != displayedFrame) ShowFrame(frame);
        }

        private void ShowFrame(int frame)
        {
            const float width = 1f / Columns;
            const float height = 1f / Rows;
            // アトラスのコマは左上から行順に並ぶ。
            uvRect = new Rect(frame % Columns * width, 1f - (frame / Columns + 1) * height, width, height);
            displayedFrame = frame;
        }
    }
}
