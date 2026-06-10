using UnityEngine;

namespace KillingMahjong.Visuals
{
    [RequireComponent(typeof(Light))]
    public class SpotlightFlicker : MonoBehaviour
    {
        [Header("Flicker Settings (明るさの揺らぎ)")]
        [SerializeField] private bool enableFlicker = true;
        [SerializeField] private float intensityBase = 5f;
        [SerializeField] private float intensityVariance = 0.5f; // 揺らぎの幅
        [SerializeField] private float flickerSpeed = 2f;      // 揺らぎの速さ

        [Header("Sway Settings (角度の揺れ)")]
        [SerializeField] private bool enableSway = true;
        [SerializeField] private float swayAmount = 1.5f; // 揺れる角度（度）
        [SerializeField] private float swaySpeed = 0.5f;  // 揺れる速さ

        private Light targetLight;
        private Quaternion initialRotation;
        private float noiseOffset;

        private void Start()
        {
            targetLight = GetComponent<Light>();
            initialRotation = transform.localRotation;
            noiseOffset = Random.Range(0f, 100f); // ランダムな開始位置
            
            if (targetLight != null && intensityBase <= 0) 
            {
                intensityBase = targetLight.intensity;
            }
        }

        private void Update()
        {
            float time = Time.time;

            if (enableFlicker && targetLight != null)
            {
                // パーリンノイズを使った、炎や古い電球のような自然な明滅
                float noise = Mathf.PerlinNoise(time * flickerSpeed, noiseOffset) * 2f - 1f; // -1.0 ～ 1.0
                targetLight.intensity = intensityBase + (noise * intensityVariance);
            }

            if (enableSway)
            {
                // ゆっくりとした振り子のような角度の揺れ（サスペンス感を出す）
                float swayX = Mathf.Sin(time * swaySpeed) * swayAmount;
                float swayZ = Mathf.Cos(time * swaySpeed * 0.8f) * swayAmount; // 周期をずらして複雑な8の字軌道に
                
                transform.localRotation = initialRotation * Quaternion.Euler(swayX, 0, swayZ);
            }
        }
    }
}
