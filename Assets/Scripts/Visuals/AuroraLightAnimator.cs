using UnityEngine;

namespace KillingMahjong.Visuals
{
    public class AuroraLightAnimator : MonoBehaviour
    {
        [Header("Aurora Settings")]
        public float swaySpeed = 0.5f;
        public float swayAmplitude = 0.2f;
        public float colorSpeed = 0.4f;
        public bool animateScale = true; // スケール（形状）の揺らめきを有効にするか

        private Vector3 startScale;
        private MeshRenderer meshRenderer;
        private LineRenderer lineRenderer;
        private Color startColor;
        private float startWidthMultiplier;
        private Material cachedMaterial; // マテリアルをキャッシュして毎フレームのアクセス負荷を軽減

        void Start()
        {
            startScale = transform.localScale;
            meshRenderer = GetComponent<MeshRenderer>();
            lineRenderer = GetComponent<LineRenderer>();

            if (meshRenderer != null) cachedMaterial = meshRenderer.material;
            else if (lineRenderer != null)
            {
                cachedMaterial = lineRenderer.material;
                startWidthMultiplier = lineRenderer.widthMultiplier;
            }

            if (cachedMaterial != null)
            {
                if (cachedMaterial.HasProperty("_Color"))
                {
                    startColor = cachedMaterial.color;
                }
                else if (cachedMaterial.HasProperty("_TintColor"))
                {
                    startColor = cachedMaterial.GetColor("_TintColor");
                }
                else
                {
                    startColor = Color.cyan;
                }
            }
        }

        void Update()
        {
            // ユラユラとスケールを波打たせる（オーロラのような揺らぎ）
            if (animateScale)
            {
                float scaleX = startScale.x + Mathf.Sin(Time.time * swaySpeed) * swayAmplitude;
                float scaleZ = startScale.z + Mathf.Cos(Time.time * swaySpeed * 1.2f) * swayAmplitude;
                transform.localScale = new Vector3(scaleX, startScale.y, scaleZ);

                // LineRendererなら太さも波打たせる
                if (lineRenderer != null)
                {
                    // ベースの太さを1.5倍くらいにして左右全体に光が届くようにする
                    float newWidth = (startWidthMultiplier * 1.5f) + Mathf.Sin(Time.time * swaySpeed) * swayAmplitude * 2f;
                    lineRenderer.widthMultiplier = newWidth;
                }
            }

            // 色を少し変化させる
            if (cachedMaterial != null)
            {
                // 全体的に光を濃く（見やすく）する
                float alphaPulse = Mathf.Lerp(0.8f, 1.2f, (Mathf.Sin(Time.time * colorSpeed * 2f) + 1f) / 2f);
                Color newColor = startColor * alphaPulse;
                // アルファ値も高めに設定して根元からクッキリ見えるようにする
                newColor.a = Mathf.Lerp(0.7f, 1.0f, (Mathf.Sin(Time.time * colorSpeed) + 1f) / 2f);

                if (cachedMaterial.HasProperty("_Color"))
                {
                    cachedMaterial.color = newColor;
                }
                else if (cachedMaterial.HasProperty("_TintColor"))
                {
                    cachedMaterial.SetColor("_TintColor", newColor);
                }
            }
        }
    }
}
