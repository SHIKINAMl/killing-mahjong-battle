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
        private Vector3 initialScale; // 追加：初期スケール
        private float randomOffset;

        private void Start()
        {
            // スクリプトがアタッチされたオブジェクトの初期座標と回転を記憶
            initialPosition = transform.localPosition;
            initialRotation = transform.localRotation;
            initialScale = transform.localScale; // スケールも記憶
            
            // 複数のオブジェクトが全く同じタイミングで動かないように、開始時間をランダムにずらす
            randomOffset = Random.Range(0f, 100f);
        }

        private void Update()
        {
            // ズーム中（スケールが変わっている時）は揺れアニメーションを停止する
            if (transform.localScale != initialScale)
            {
                return;
            }

            float time = Time.time + randomOffset;
            
            // Sin波とCos波を使って滑らかな浮遊オフセットを計算
            float offsetY = Mathf.Sin(time * floatSpeed) * floatAmplitudeY;
            float offsetX = Mathf.Cos(time * floatSpeed * 0.8f) * floatAmplitudeX;
            
            // 初期座標に対してオフセットを足す（回転処理のみジャギー防止のため無効化）
            transform.localPosition = initialPosition + new Vector3(offsetX, offsetY, 0);
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
