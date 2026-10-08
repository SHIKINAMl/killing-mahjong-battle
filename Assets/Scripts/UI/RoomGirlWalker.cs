using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    // **RoomScreenUI.cs から独立させた（2026-10-09）。**
    // 同じファイルに同居していると、Unity はこの部品をシーンに保存できない
    // （ファイル名とクラス名が合わないため）。部屋をシーンに保存したとき、
    // 女の子に「中身の無い部品」が残り、読み込むたびに警告が出ていた。
    /// <summary>部屋での待機・移動・瞬きを担当する、UI立ち絵用の小さな状態機械。</summary>
    public sealed class RoomGirlWalker : MonoBehaviour
    {
        public enum WalkerState
        {
            Idle,
            Walk
        }

        public WalkerState State { get; private set; } = WalkerState.Idle;
        public bool IsBlinking { get; private set; }

        private RectTransform girl;
        private Image face;
        private Sprite openFace;
        private Sprite closedFace;
        private Vector2 minPosition;
        private Vector2 maxPosition;
        private Vector2 basePosition;
        private Vector2 targetPosition;
        private float idleRemaining;
        private float walkSpeed;
        private float blinkEndsAt;
        private float nextBlinkAt;
        private bool initialized;

        public void Initialize(Image faceImage, Sprite openFaceSprite,
            Sprite closedFaceSprite, Vector2 min, Vector2 max)
        {
            girl = GetComponent<RectTransform>();
            face = faceImage;
            openFace = openFaceSprite;
            closedFace = closedFaceSprite;
            minPosition = min;
            maxPosition = max;
            basePosition = girl != null ? girl.anchoredPosition : Vector2.zero;
            targetPosition = basePosition;
            idleRemaining = UnityEngine.Random.Range(2.5f, 4.5f);
            nextBlinkAt = Time.unscaledTime + UnityEngine.Random.Range(3.5f, 5.5f);
            initialized = girl != null;
            ApplyVisuals();
        }

        private void Update()
        {
            if (!initialized) return;

            float delta = Time.unscaledDeltaTime;
            if (State == WalkerState.Idle)
            {
                idleRemaining -= delta;
                if (idleRemaining <= 0f) BeginWalk();
            }
            else
            {
                basePosition = Vector2.MoveTowards(basePosition, targetPosition, walkSpeed * delta);
                if ((basePosition - targetPosition).sqrMagnitude < 0.01f)
                {
                    State = WalkerState.Idle;
                    idleRemaining = UnityEngine.Random.Range(3f, 6f);
                }
            }

            UpdateBlink();
            ApplyVisuals();
        }

        private void BeginWalk()
        {
            State = WalkerState.Walk;
            float midpoint = (minPosition.x + maxPosition.x) * 0.5f;
            float targetX = basePosition.x <= midpoint ? maxPosition.x : minPosition.x;
            targetPosition = new Vector2(targetX, basePosition.y);

            walkSpeed = UnityEngine.Random.Range(44f, 66f);
        }

        private void UpdateBlink()
        {
            float now = Time.unscaledTime;
            if (!IsBlinking && now >= nextBlinkAt)
            {
                IsBlinking = true;
                blinkEndsAt = now + 0.12f;
                if (face != null) face.sprite = closedFace;
            }
            else if (IsBlinking && now >= blinkEndsAt)
            {
                IsBlinking = false;
                nextBlinkAt = now + UnityEngine.Random.Range(4f, 7f);
                if (face != null) face.sprite = openFace;
            }
        }

        private void ApplyVisuals()
        {
            float time = Time.unscaledTime;
            float bobAmplitude = State == WalkerState.Walk ? 4f : 1.8f;
            float bobSpeed = State == WalkerState.Walk ? 8f : 2.5f;
            Vector2 visualPosition = basePosition + Vector2.up * Mathf.Sin(time * bobSpeed) * bobAmplitude;
            float direction = State == WalkerState.Walk
                ? Mathf.Sign(targetPosition.x - basePosition.x) : 0f;
            float tilt = State == WalkerState.Walk ? -direction * 2.5f : 0f;

            girl.anchoredPosition = visualPosition;
            girl.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }
    }
}
