using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>ロンとスキルで共用する、2倍表示の静止ドット背景。</summary>
    public sealed class CutinPixelBackgroundUI : MonoBehaviour
    {
        private const float PixelScale = 2f;
        private RectTransform band;
        private RectTransform rect;
        private RectTransform canvasRect;

        public static void Attach(RectTransform band)
        {
            var texture = Resources.Load<Texture2D>("UI/Cutin/gothic_band");
            if (texture == null)
            {
                Debug.LogError("[CutinPixelBackgroundUI] Background texture is missing.");
                return;
            }
            var go = new GameObject("PixelBackground", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(band, false);
            go.transform.SetAsFirstSibling();
            var image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            var background = go.AddComponent<CutinPixelBackgroundUI>();
            background.band = band;
            background.rect = (RectTransform)go.transform;
            background.rect.anchorMin = background.rect.anchorMax = background.rect.pivot = new Vector2(.5f, .5f);
            background.rect.sizeDelta = new Vector2(texture.width, texture.height) * PixelScale;
            var canvas = band.GetComponentInParent<Canvas>();
            background.canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            background.LateUpdate();
        }

        private void LateUpdate()
        {
            if (band == null || canvasRect == null) return;
            // 帯・マスク・立ち絵の既存の傾きは保ち、背景画像だけ画面に対して水平に戻す。
            rect.rotation = canvasRect.rotation;
            // スキルの飛び込み中も背景の1ドットを2単位の格子へ合わせる。
            Vector3 center = canvasRect.InverseTransformPoint(band.position);
            center.x = Mathf.Round(center.x / PixelScale) * PixelScale;
            center.y = Mathf.Round(center.y / PixelScale) * PixelScale;
            rect.position = canvasRect.TransformPoint(center);
        }
    }
}
