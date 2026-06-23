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
        }

        void Update()
        {
            // 上下へのフワフワした動き
            float newY = startPos.y + Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
            transform.localPosition = new Vector3(startPos.x, newY, startPos.z);

            // 回転
            transform.Rotate(rotationSpeed * Time.deltaTime, Space.World);
        }
    }
}
