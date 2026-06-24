using UnityEngine;

namespace KillingMahjong.Visuals
{
    public class DoraFloatAnimator : MonoBehaviour
    {
        [Header("Float Settings")]
        public float floatSpeed = 2f;
        public float floatAmplitude = 0.5f;

        [Header("Rotation Settings")]
        public Vector3 rotationSpeed = new Vector3(0f, 90f, 0f);

        private Vector3 startPos;

        void Start()
        {
            startPos = transform.localPosition;
            
            // アニメーション速度の上書き（ゆっくりに）
            rotationSpeed = new Vector3(0f, 25f, 0f);
            floatSpeed = 0.8f; // デフォルト(2f)よりゆっくり
        }

        private UnityEngine.UI.Image uiImage;

        void Update()
        {
            // 上下へのフワフワした動き
            float newY = startPos.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            transform.localPosition = new Vector3(startPos.x, newY, startPos.z);

            // 回転をやめ、常にメインカメラの方向を向くようにする（ビルボード化）
            if (Camera.main != null)
            {
                transform.rotation = Camera.main.transform.rotation;
            }

            // Imageのα値を 0.5 ~ 0.8 で推移（点滅）させる
            if (uiImage == null) uiImage = GetComponent<UnityEngine.UI.Image>();
            if (uiImage != null)
            {
                Color c = uiImage.color;
                // Sinカーブ(-1~1)を(0~1)に変換して 0.4f ~ 0.8f の間で補間、明滅スピードも少しゆっくりに(3f -> 1.5f)
                float alpha = Mathf.Lerp(0.4f, 0.8f, (Mathf.Sin(Time.time * 1.5f) + 1f) / 2f);
                c.a = alpha;
                uiImage.color = c;
            }
        }
    }
}
