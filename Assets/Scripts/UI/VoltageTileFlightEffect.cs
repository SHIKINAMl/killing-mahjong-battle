using KillingMahjong.Common;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// A short flight of light from a newly seen local discard to the voltage gauge.
    /// This creates only runtime UI shapes: no scene object, prefab, or image asset is required.
    /// RiverUI is responsible for limiting it to the tutorial prototype.
    /// </summary>
    public sealed class VoltageTileFlightEffect : MonoBehaviour
    {
        private const string CanvasName = "VoltageTileFlightCanvas";
        private const string SelfGaugeName = "VoltageUI_Self";
        private const string EnemyGaugeName = "VoltageUI_Enemy";
        // **実測して決めた数字（2026-09-13）。**
        // 最初は 3〜6px の四角だったが、800x600 の画面では点が見えず、
        // 録画でも1フレームも光として写らなかった。円のスプライトに変え、
        // 大きさを4倍ほどにしてようやく「光が飛ぶ」ように見える。
        private const int ParticleCount = 14;
        private const float FlightDuration = 0.55f;
        private const float LaunchInterval = 0.03f;
        private const float FinishHold = 0.18f;
        private const float ParticleMinSize = 13f;
        private const float ParticleMaxSize = 24f;

        private sealed class Particle
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Start;
            public Vector2 Control;
            public Vector2 End;
            public float Delay;
            public float Size;
            public float Rotation;
        }

        private Particle[] _particles;
        private Image _arrivalSpark;
        private float _elapsed;
        private float _totalDuration;
        private float _arrivalTime;

        // 光が着いたときに1度だけ呼ぶ。段を上げるのはここ。
        private System.Action _onArrive;
        private bool _arrived;

        /// <summary>
        /// 捨てた牌から、その人のボルテージゲージへ光を飛ばす。
        /// ゲージが無い場面（表示を切っているチュートリアルの冒頭など）では
        /// **何も作らずに false を返す。** 呼んだ側は、そのときは段を自分で上げること。
        /// </summary>
        /// <param name="sourceTile">河に置かれた牌。</param>
        /// <param name="isEnemy">相手の打牌なら true。飛ぶ先のゲージが変わる。</param>
        /// <param name="onArrive">光が着いた瞬間に1度だけ呼ばれる。</param>
        public static bool TryPlay(RectTransform sourceTile, bool isEnemy, System.Action onArrive)
        {
            if (sourceTile == null) return false;

            var gaugeObject = GameObject.Find(isEnemy ? EnemyGaugeName : SelfGaugeName);
            var targetGauge = gaugeObject != null ? gaugeObject.GetComponent<RectTransform>() : null;
            if (targetGauge == null) return false;

            var canvasRect = EnsureCanvas();
            if (canvasRect == null) return false;

            Vector2 start = ToCanvasPosition(canvasRect, sourceTile);
            Vector2 end = ToCanvasPosition(canvasRect, targetGauge);

            var root = new GameObject("VoltageTileFlight", typeof(RectTransform), typeof(VoltageTileFlightEffect));
            var effectRect = (RectTransform)root.transform;
            effectRect.SetParent(canvasRect, false);
            effectRect.anchorMin = Vector2.zero;
            effectRect.anchorMax = Vector2.one;
            effectRect.offsetMin = Vector2.zero;
            effectRect.offsetMax = Vector2.zero;

            // **牌の位置は、このフレームではまだ決まっていない。**
            // 河は並べ直しを次のフレームに回すので、呼ばれた瞬間に測ると
            // 牌のいる場所ではなく、並べる前の場所から光が出てしまう。
            // 1フレーム待ってから測り直す。
            var effect = root.GetComponent<VoltageTileFlightEffect>();
            effect._onArrive = onArrive;
            effect.StartCoroutine(BuildNextFrame(root, canvasRect, sourceTile, targetGauge, start, end));
            return true;
        }

        /// <summary>
        /// 1フレーム待ってから、牌とゲージの位置を測り直して光を組む。
        /// 牌が消えていたら、呼ばれた時に測った位置をそのまま使う。
        /// </summary>
        private static System.Collections.IEnumerator BuildNextFrame(
            GameObject root, RectTransform canvasRect, RectTransform sourceTile,
            RectTransform targetGauge, Vector2 fallbackStart, Vector2 fallbackEnd)
        {
            yield return null;

            if (root == null) yield break;

            Vector2 start = sourceTile != null ? ToCanvasPosition(canvasRect, sourceTile) : fallbackStart;
            Vector2 end = targetGauge != null ? ToCanvasPosition(canvasRect, targetGauge) : fallbackEnd;

            var effect = root.GetComponent<VoltageTileFlightEffect>();
            if (effect != null) effect.Build(start, end);
        }

        private static RectTransform EnsureCanvas()
        {
            var existing = GameObject.Find(CanvasName);
            if (existing != null) return existing.transform as RectTransform;

            var canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Follow the existing tile-animation layer so the light is visible from the tile to the gauge.
            canvas.sortingOrder = UISortingOrders.TileAnimationLayer;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvasObject.transform as RectTransform;
        }

        private static Vector2 ToCanvasPosition(RectTransform canvasRect, RectTransform element)
        {
            var sourceCanvas = element.GetComponentInParent<Canvas>();
            Camera sourceCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? sourceCanvas.worldCamera
                : null;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                sourceCamera, element.TransformPoint(element.rect.center));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out var point);
            return point;
        }

        private void Build(Vector2 start, Vector2 end)
        {
            _particles = new Particle[ParticleCount];
            Vector2 direction = end - start;
            Vector2 perpendicular = direction.sqrMagnitude > 0.01f
                ? new Vector2(-direction.y, direction.x).normalized
                : Vector2.up;

            for (int i = 0; i < ParticleCount; i++)
            {
                var particleObject = new GameObject("Light" + i, typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)particleObject.transform;
                rect.SetParent(transform, false);

                var image = particleObject.GetComponent<Image>();
                image.raycastTarget = false;
                // 炎と同じ円を借りる。縁が硬い四角だと、小さいうちは
                // ただの点にしか見えない。
                image.sprite = VoltageFlame.SharedBlob;
                image.color = new Color(1f, 0.78f, 0.20f, 0f);

                float side = Random.Range(-1f, 1f);
                float along = Random.Range(0.42f, 0.64f);
                float arc = Random.Range(40f, 80f) * (side >= 0f ? 1f : -1f);
                float size = Random.Range(ParticleMinSize, ParticleMaxSize);

                _particles[i] = new Particle
                {
                    Rect = rect,
                    Image = image,
                    Start = start + perpendicular * Random.Range(-7f, 7f),
                    Control = Vector2.Lerp(start, end, along) + perpendicular * arc + Vector2.up * Random.Range(10f, 28f),
                    End = end + perpendicular * Random.Range(-6f, 6f),
                    Delay = i * LaunchInterval + Random.Range(0f, 0.018f),
                    Size = size,
                    Rotation = Random.Range(0f, 90f)
                };
            }

            _arrivalSpark = CreateSpark("ArrivalSpark", end, 16f);
            _arrivalTime = FlightDuration + (ParticleCount - 1) * LaunchInterval;
            _totalDuration = _arrivalTime + FinishHold;
        }

        private Image CreateSpark(string name, Vector2 position, float size)
        {
            var sparkObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)sparkObject.transform;
            rect.SetParent(transform, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var image = sparkObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.sprite = VoltageFlame.SharedBlob;
            image.color = new Color(1f, 0.88f, 0.35f, 0f);
            return image;
        }

        private void Update()
        {
            // 組み上がる前（1フレーム待っている間）は、時計を進めない
            if (_particles == null) return;

            _elapsed += Time.unscaledDeltaTime;
            UpdateParticles();
            UpdateArrivalSpark();

            // **段が上がるのはここ。** 光が着いたのを見てからゲージが動く。
            if (!_arrived && _elapsed >= _arrivalTime)
            {
                _arrived = true;
                var callback = _onArrive;
                _onArrive = null;
                if (callback != null) callback();
            }

            if (_elapsed >= _totalDuration)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 局の切り替えなどで途中に消されても、**段は必ず上げる。**
        /// ここを落とすと、捨てたのにゲージが動かない牌が出る。
        /// </summary>
        private void OnDestroy()
        {
            if (_arrived) return;
            _arrived = true;
            var callback = _onArrive;
            _onArrive = null;
            if (callback != null) callback();
        }

        private void UpdateParticles()
        {
            if (_particles == null) return;

            foreach (var particle in _particles)
            {
                float normalizedTime = (_elapsed - particle.Delay) / FlightDuration;
                if (normalizedTime <= 0f)
                {
                    particle.Image.color = new Color(1f, 0.78f, 0.20f, 0f);
                    continue;
                }

                float t = Mathf.Clamp01(normalizedTime);
                float eased = t * t * (3f - 2f * t);
                Vector2 a = Vector2.Lerp(particle.Start, particle.Control, eased);
                Vector2 b = Vector2.Lerp(particle.Control, particle.End, eased);
                particle.Rect.anchoredPosition = Vector2.Lerp(a, b, eased);

                float taper = 1f - 0.35f * t;
                float size = particle.Size * (0.65f + 0.35f * Mathf.Sin(t * Mathf.PI)) * taper;
                particle.Rect.sizeDelta = new Vector2(size, size);
                particle.Rect.localRotation = Quaternion.Euler(0f, 0f, particle.Rotation + t * 180f);

                float fadeIn = Mathf.Clamp01(t / 0.16f);
                float fadeOut = 1f - Mathf.Clamp01((t - 0.62f) / 0.38f);
                // **赤い壁の前を飛ぶ。** オレンジのままだと背景に沈むので、
                // 進むほど白へ寄せて、光っているように見せる。
                float white = Mathf.Lerp(0.55f, 1f, t);
                particle.Image.color = new Color(1f, Mathf.Lerp(0.80f, 1f, t), Mathf.Lerp(0.25f, white, t), fadeIn * fadeOut);
            }
        }

        private void UpdateArrivalSpark()
        {
            if (_arrivalSpark == null) return;

            float normalizedTime = Mathf.Clamp01((_elapsed - _arrivalTime + 0.12f) / 0.22f);
            float alpha = normalizedTime <= 0f ? 0f : (1f - normalizedTime) * 0.95f;
            float size = Mathf.Lerp(16f, 72f, normalizedTime);

            var rect = _arrivalSpark.rectTransform;
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f + normalizedTime * 120f);
            _arrivalSpark.color = new Color(1f, 0.9f, 0.35f, alpha);
        }
    }
}
