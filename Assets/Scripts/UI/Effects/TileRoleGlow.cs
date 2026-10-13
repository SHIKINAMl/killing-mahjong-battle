using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI.Effects
{
    /// <summary>牌の画像そのものへ光を足す。役判定やクリック処理は持たない。</summary>
    public sealed class TileRoleGlow : MonoBehaviour
    {
        public const string RootName = "RoleTileSurfaceGlow";
        [SerializeField] private Color surfaceTint = new Color(1, .99f, .60f, .78f);
        [SerializeField] private Color lightTint = new Color(1, .85f, .40f, 1);
        [SerializeField] private float intensity = .22f, pulsePerSecond = .55f, fadeSeconds = .06f;
        private Image source, face, light;
        private CanvasGroup gate;
        private Material ownedMaterial;
        private bool highlighted;
        private float enteredAt;
        public bool IsHighlighted => highlighted && isActiveAndEnabled && source != null && source.enabled;
        public float Alpha => gate != null ? gate.alpha : 0;

        public static TileRoleGlow Attach(Image image)
        {
            if (image == null) return null;
            var go = SceneFirst.Child(image.transform, RootName, out bool made,
                typeof(RectTransform), typeof(CanvasGroup), typeof(TileRoleGlow));
            if (made) Stretch((RectTransform)go.transform, image.rectTransform.pivot);
            var effect = go.GetComponent<TileRoleGlow>();
            effect.source = image;
            effect.Build();
            return effect;
        }
        private static void Stretch(RectTransform rect, Vector2 pivot)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.pivot = pivot;
        }
        private void Build()
        {
            gate = GetComponent<CanvasGroup>(); gate.blocksRaycasts = gate.interactable = false;
            face = Layer("SurfaceTint"); light = Layer("SurfaceLight");
            gate.alpha = 0;
            if (ownedMaterial == null) ownedMaterial = NeonMaterials.CreateAll(lightTint, intensity);
            if (ownedMaterial != null) { ownedMaterial.name = "TileRoleGlow (runtime)"; light.material = ownedMaterial; }
            light.enabled = ownedMaterial != null;
            SyncShape();
        }
        private Image Layer(string name)
        {
            var go = SceneFirst.Child(transform, name, out bool made, typeof(RectTransform), typeof(Image));
            if (made) Stretch((RectTransform)go.transform, source.rectTransform.pivot);
            var image = go.GetComponent<Image>(); image.raycastTarget = false; return image;
        }
        public void SetHighlighted(bool value)
        {
            value &= source != null && ownedMaterial != null && gameObject.activeInHierarchy;
            if (!value) { highlighted = false; if (gate != null) gate.alpha = 0; return; }
            if (highlighted) return;
            highlighted = true; enteredAt = Time.unscaledTime; RenderAt(enteredAt);
        }
        private void LateUpdate() { if (highlighted) RenderAt(Time.unscaledTime); }
        public void RenderAt(float time)
        {
            if (!highlighted || source == null || !source.enabled || !source.gameObject.activeInHierarchy)
            { if (gate != null) gate.alpha = 0; return; }
            SyncShape();
            gate.alpha = Mathf.Clamp01((time - enteredAt) / Mathf.Max(.001f, fadeSeconds));
            face.color = surfaceTint;
            float pulse = .9f + .1f * Mathf.Sin(time * Mathf.PI * 2 * pulsePerSecond);
            light.color = new Color(1, 1, 1, .8f * pulse * source.color.a);
        }
        private void SyncShape()
        {
            if (source == null) return;
            CopyShape(face); CopyShape(light);
        }
        private void CopyShape(Image layer)
        {
            layer.sprite = source.sprite; layer.type = source.type; layer.preserveAspect = source.preserveAspect;
            layer.fillAmount = source.fillAmount; layer.fillMethod = source.fillMethod;
            layer.fillOrigin = source.fillOrigin; layer.fillClockwise = source.fillClockwise; layer.raycastTarget = false;
        }
        public void Release()
        {
            SetHighlighted(false); gameObject.SetActive(false); DisposeMaterial(); Destroy(gameObject);
        }
        private void DisposeMaterial()
        {
            if (ownedMaterial == null) return;
            if (light != null) light.material = null;
            Destroy(ownedMaterial); ownedMaterial = null;
        }
        private void OnDisable() { SetHighlighted(false); }
        private void OnDestroy() { DisposeMaterial(); }
    }
}
