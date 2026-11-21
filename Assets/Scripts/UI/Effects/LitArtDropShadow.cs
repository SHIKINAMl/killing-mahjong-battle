using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 物が落とす影。場面の光（SceneLight）と反対側へずらした黒い形を、落ちる先（壁や卓）の上に描く。
    /// 影の部品はシーンに置き、元の物（source）を毎フレームなぞる。元の物の色や表示には手を出さない。
    /// - 影が Image のとき: 元の Graphic（UI）と同じ絵・大きさを、画面上でずらした位置に置く。
    ///   sourceGroup を使うと、その下に出ている絵全体を囲む四角の影にする（山牌の列など）
    /// - 影が SpriteRenderer のとき: 元の SpriteRenderer と同じ絵を、画面上でずらした位置に置く
    /// </summary>
    // 再生中だけ動く（編集中に位置を書き換えるとシーンのファイルが毎回変わるため）。置いた時点では影を消しておく
    public sealed class LitArtDropShadow : MonoBehaviour
    {
        [SerializeField] private Graphic sourceGraphic;
        [SerializeField] private SpriteRenderer sourceSprite;
        [Tooltip("ここに置いた部品の下に出ている絵全体を囲む四角を影にする（並んだ牌など）。使うときは sourceGraphic は空にする")]
        [SerializeField] private RectTransform sourceGroup;

        [Tooltip("影のずれの長さ（画面の高さを 600 とした px）。物が落ちる先から離れているほど長い")]
        [SerializeField] private float length = 18f;
        [Tooltip("ずれの長さの上限に対する、光からの距離の効き方。0 で距離によらず一定")]
        [SerializeField, Range(0f, 1f)] private float distanceInfluence = 0.5f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.45f;
        [SerializeField] private Color shadowColor = new Color(0.08f, 0.0f, 0.03f, 1f);

        private Image shadowImage;
        private SpriteRenderer shadowSprite;

        private void OnEnable()
        {
            shadowImage = GetComponent<Image>();
            shadowSprite = GetComponent<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            var light = SceneLight.Current;
            if (light == null || !light.isActiveAndEnabled) { Hide(); return; }

            if (shadowImage != null && (sourceGraphic != null || sourceGroup != null)) FollowGraphic(light);
            else if (shadowSprite != null && sourceSprite != null) FollowSprite(light);
            else Hide();
        }

        private void Hide()
        {
            if (shadowImage != null) shadowImage.enabled = false;
            if (shadowSprite != null) shadowSprite.enabled = false;
        }

        private Vector2 OffsetFor(SceneLight light, Vector2 center)
        {
            float scale = Screen.height / 600f;
            Vector2 away = center - light.ScreenPosition;
            float dist = away.magnitude;
            if (dist < 1e-3f) return Vector2.zero;
            // 光から遠い物ほど影が少し長く伸びる
            float stretch = Mathf.Lerp(1f, Mathf.Clamp(dist / (300f * scale), 0.5f, 1.8f), distanceInfluence);
            return away / dist * length * scale * stretch;
        }

        private void FollowGraphic(SceneLight light)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            float alpha = 0f;
            if (sourceGroup != null)
            {
                if (!sourceGroup.gameObject.activeInHierarchy) { shadowImage.enabled = false; return; }
                foreach (var g in sourceGroup.GetComponentsInChildren<Graphic>(false))
                {
                    if (!g.isActiveAndEnabled) continue;
                    float a = g.color.a * g.canvasRenderer.GetInheritedAlpha();
                    if (a < 0.01f) continue;
                    Encapsulate(g.rectTransform, CameraOf(g.canvas), ref min, ref max);
                    alpha = Mathf.Max(alpha, a);
                }
            }
            else if (sourceGraphic.isActiveAndEnabled)
            {
                alpha = sourceGraphic.color.a * sourceGraphic.canvasRenderer.GetInheritedAlpha();
                if (alpha >= 0.01f) Encapsulate(sourceGraphic.rectTransform, CameraOf(sourceGraphic.canvas), ref min, ref max);
            }
            if (alpha < 0.01f) { shadowImage.enabled = false; return; }

            Vector2 offset = OffsetFor(light, (min + max) * 0.5f);
            var parent = (RectTransform)transform.parent;
            var canvas = shadowImage.canvas;
            Camera cam = CameraOf(canvas);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, min + offset, cam, out var lmin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, max + offset, cam, out var lmax);

            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            Vector2 parentCenter = parent.rect.center;
            rt.anchoredPosition = (lmin + lmax) * 0.5f - parentCenter;
            rt.sizeDelta = lmax - lmin;

            if (sourceGroup == null && sourceGraphic is Image src)
            {
                shadowImage.sprite = src.sprite;
                shadowImage.preserveAspect = src.preserveAspect;
            }
            else
            {
                shadowImage.sprite = null;
            }
            shadowImage.raycastTarget = false;
            var c = shadowColor;
            c.a = opacity * alpha * Mathf.Clamp01(light.ShadowFlicker);
            shadowImage.color = c;
            shadowImage.enabled = true;
        }

        private static Camera CameraOf(Canvas canvas) =>
            canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        private static void Encapsulate(RectTransform rt, Camera cam, ref Vector2 min, ref Vector2 max)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            for (int i = 0; i < 4; i++)
            {
                Vector2 s = RectTransformUtility.WorldToScreenPoint(cam, c[i]);
                min = Vector2.Min(min, s);
                max = Vector2.Max(max, s);
            }
        }

        private void FollowSprite(SceneLight light)
        {
            bool visible = sourceSprite.enabled && sourceSprite.gameObject.activeInHierarchy && sourceSprite.sprite != null && sourceSprite.color.a > 0.01f;
            var cam = Camera.main;
            if (!visible || cam == null) { shadowSprite.enabled = false; return; }

            Vector3 srcPos = sourceSprite.bounds.center;
            Vector3 screen = cam.WorldToScreenPoint(srcPos);
            Vector2 offset = OffsetFor(light, screen);
            Vector3 moved = cam.ScreenToWorldPoint(new Vector3(screen.x + offset.x, screen.y + offset.y, screen.z));
            Vector3 delta = moved - srcPos;

            var t = sourceSprite.transform;
            // 影は元の少し奥に置き、同じ並び順でも元より先に描かれるようにする
            Vector3 back = cam.transform.forward * 0.05f;
            transform.SetPositionAndRotation(t.position + delta + back, t.rotation);
            var parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(t.lossyScale.x / parentScale.x, t.lossyScale.y / parentScale.y, t.lossyScale.z / parentScale.z);

            shadowSprite.sprite = sourceSprite.sprite;
            shadowSprite.flipX = sourceSprite.flipX;
            shadowSprite.flipY = sourceSprite.flipY;
            shadowSprite.sortingLayerID = sourceSprite.sortingLayerID;
            shadowSprite.sortingOrder = sourceSprite.sortingOrder;
            var c = shadowColor;
            c.a = opacity * sourceSprite.color.a * Mathf.Clamp01(light.ShadowFlicker);
            shadowSprite.color = c;
            shadowSprite.enabled = true;
        }
    }
}
