using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// UI要素や2Dスプライトなどを上下左右にふらふらと浮遊させる汎用アニメーションスクリプト
    /// </summary>
    public class FloatingAnimator : MonoBehaviour
    {
        [Header("Floating Settings")]
        [Tooltip("ふらふら動くスピード")]
        [SerializeField] private float floatSpeed = 2f;
        
        [Tooltip("縦に動く幅（ピクセル/ユニット）")]
        [SerializeField] private float floatAmplitudeY = 10f;
        
        [Tooltip("横に動く幅（ピクセル/ユニット）")]
        [SerializeField] private float floatAmplitudeX = 5f;
        
        [Header("Rotation Settings (Optional)")]
        [Tooltip("ゆらゆら回転するスピード")]
        [SerializeField] private float rotationSpeed = 1f;
        
        [Tooltip("ゆらゆら回転する角度の幅")]
        [SerializeField] private float rotationAmplitude = 2f;

        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private float randomOffset;

        private void Start()
        {
            // スクリプトがアタッチされたオブジェクトの初期座標と回転を記憶
            initialPosition = transform.localPosition;
            initialRotation = transform.localRotation;
            
            // 複数のオブジェクトが全く同じタイミングで動かないように、開始時間をランダムにずらす
            randomOffset = Random.Range(0f, 100f);
        }

        private void Update()
        {
            float time = Time.time + randomOffset;
            
            // Sin波とCos波を使って滑らかな浮遊オフセットを計算
            // XとYで少しスピードを変えることで、単調な斜め移動ではなく8の字や円に近い自然な揺れを作る
            float offsetY = Mathf.Sin(time * floatSpeed) * floatAmplitudeY;
            float offsetX = Mathf.Cos(time * floatSpeed * 0.8f) * floatAmplitudeX;
            
            // 初期座標に対してオフセットを足す
            transform.localPosition = initialPosition + new Vector3(offsetX, offsetY, 0);

            // 回転が設定されていれば回転させる
            if (rotationAmplitude > 0)
            {
                float rotZ = Mathf.Sin(time * rotationSpeed) * rotationAmplitude;
                transform.localRotation = initialRotation * Quaternion.Euler(0, 0, rotZ);
            }
        }
        
        /// <summary>
        /// もしプログラムから外部的に位置を移動させた場合、
        /// このメソッドを呼ぶことで「ふらふらの中心座標」を再設定できる。
        /// </summary>
        public void UpdateInitialPosition()
        {
            initialPosition = transform.localPosition;
        }
    }
}
