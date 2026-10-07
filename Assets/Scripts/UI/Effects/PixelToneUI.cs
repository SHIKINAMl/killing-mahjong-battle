using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>元画像を変更せず、細かなドットと少ない色数でUI画像を描く。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RawImage))]
    public sealed class PixelToneUI : MonoBehaviour
    {
        private RawImage image;
        private Material original, owned;

        public static PixelToneUI Attach(RawImage target, float pixelSize = 2f, float colorSteps = 8f)
        {
            var effect = target.GetComponent<PixelToneUI>();
            if (effect == null) effect = target.gameObject.AddComponent<PixelToneUI>();
            if (effect.owned != null)
            {
                effect.owned.SetFloat("_PixelSize", Mathf.Clamp(pixelSize, 1f, 8f));
                effect.owned.SetFloat("_ColorSteps", Mathf.Clamp(colorSteps, 2f, 32f));
            }
            return effect;
        }

        private void Awake()
        {
            image = GetComponent<RawImage>();
            var shader = Resources.Load<Shader>("Presentation/PixelToneUI");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError("[PixelToneUI] ドット描画用シェーダーを読み込めません。");
                return;
            }
            original = image.material;
            owned = new Material(shader) { name = "PixelToneUI (Instance)" };
            image.material = owned;
        }

        private void OnDestroy()
        {
            if (image != null && image.material == owned) image.material = original;
            if (owned != null) Destroy(owned);
        }
    }
}
