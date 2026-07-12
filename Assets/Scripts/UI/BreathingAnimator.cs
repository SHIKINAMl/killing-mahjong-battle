using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 1枚の画像（Sprite等）をゆらゆらと動かして、疑似的に呼吸しているように見せるスクリプト
    /// </summary>
    public class BreathingAnimator : MonoBehaviour
    {
        [Header("Breathing Settings")]
        [Tooltip("呼吸の速さ（1が標準）")]
        [SerializeField] private float speed = 1.5f;
        
        [Tooltip("伸び縮みする大きさの幅")]
        [SerializeField] private float scaleAmount = 0.015f;
        
        [Tooltip("左右に揺れる角度の幅")]
        [SerializeField] private float rotationAmount = 0.5f;

        private Vector3 initialScale;
        private Quaternion initialRotation;
        private float timeElapsed = 0f;

        private void Start()
        {
            // 初期状態の大きさと角度を記憶
            initialScale = transform.localScale;
            initialRotation = transform.localRotation;
            
            // アニメーションのスタート位置をランダムにして、複数同時に動かした時にズレるようにする
            timeElapsed = Random.Range(0f, 10f);
        }

        private void Update()
        {
            timeElapsed += Time.deltaTime * speed;

            // Sin波（-1 〜 1を行き来する波）を使ってゆったりとした動きを作る
            float breath = Mathf.Sin(timeElapsed);

            // 息を吸うとY軸が伸びてX軸が縮む（胸郭の動きのイメージ）
            float scaleY = initialScale.y * (1f + breath * scaleAmount);
            float scaleX = initialScale.x * (1f - breath * scaleAmount * 0.5f);
            
            transform.localScale = new Vector3(scaleX, scaleY, initialScale.z);

            // わずかな左右の揺れ（体の重心移動のイメージ）
            float rotZ = breath * rotationAmount;
            transform.localRotation = initialRotation * Quaternion.Euler(0, 0, rotZ);
        }
    }
}
