using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// カットインの、2倍表示の静止ドット背景。ロンは <c>gothic_band</c>、スキルは <c>skill_band_red</c>／<c>skill_band_blue</c>。
    /// どの絵も帯の形（透明なところ）は同じ。
    ///
    /// 絵の透明でないところだけを切り抜く Mask も持つ。子に置いた物（ロンの立ち絵など）は、
    /// 背景の絵の外に出ない（2026-10-20、ユーザー指示「女の子はカットインの背景の中だけ写す」）。
    /// </summary>
    public sealed class CutinPixelBackgroundUI : MonoBehaviour
    {
        private const float PixelScale = 2f;
        private RectTransform band;
        private RectTransform rect;
        private RectTransform canvasRect;

        /// <summary>帯の最初の子として背景を置き、その RectTransform を返す（中身を切り抜く入れ物にもなる）。</summary>
        public static RectTransform Attach(RectTransform band, string textureName = "gothic_band")
        {
            var texture = Resources.Load<Texture2D>("UI/Cutin/" + textureName);
            if (texture == null)
            {
                Debug.LogError("[CutinPixelBackgroundUI] Background texture is missing: " + textureName);
                return null;
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
            // 透明なところは Mask の型にならない（Mask は型の絵を透明度で切る）
            go.AddComponent<Mask>().showMaskGraphic = true;
            background.LateUpdate();
            return background.rect;
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
