using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>コレクションの没案専用。対局の勝敗経路からは呼ばない。</summary>
    public sealed class RedDefeatPrototypeUI : MonoBehaviour
    {
        public enum SequenceStage { Silhouette, EyesOpening, Laugh, Tilt, EyesClosing, Thud, Wait, Completed }
        public SequenceStage Stage { get; private set; }
        public float StartedAt { get; private set; }
        public float ThudFinishedAt { get; private set; }
        public float CompletedAt { get; private set; }

        private RectTransform picture, leftEye, rightEye;
        private EyelidClosureGraphic eyelids;
        private AudioSource sound;
        private AudioClip thud;
        private Canvas cursorCanvas;
        private bool cursorWasEnabled;
        private System.Action onPreviewCompleted;

        public static RedDefeatPrototypeUI Play(System.Action onPreviewCompleted = null)
        {
            var existing = FindFirstObjectByType<RedDefeatPrototypeUI>();
            if (existing != null) return existing;
            var closed = Resources.Load<Texture2D>("UnusedEndings/RedDefeat/closed");
            var open = Resources.Load<Texture2D>("UnusedEndings/RedDefeat/open");
            var background = Resources.Load<Texture2D>("UnusedEndings/RedDefeat/background");
            if (closed == null || open == null || background == null)
            {
                Debug.LogError("[RedDefeatPrototypeUI] 没案の画像がありません。");
                return null;
            }
            var root = new GameObject("RedDefeatPrototype", typeof(RectTransform));
            var sequence = root.AddComponent<RedDefeatPrototypeUI>();
            sequence.onPreviewCompleted = onPreviewCompleted;
            sequence.Build(closed, open, background);
            sequence.StartCoroutine(sequence.Run());
            return sequence;
        }

        private void Build(Texture2D closed, Texture2D open, Texture2D background)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var cursor = GameObject.Find("UICursorCanvas");
            if (cursor != null && onPreviewCompleted == null)
            {
                cursorCanvas = cursor.GetComponent<Canvas>();
                if (cursorCanvas != null)
                {
                    cursorWasEnabled = cursorCanvas.enabled;
                    cursorCanvas.enabled = false;
                }
            }
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800, 600);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var cover = new GameObject("InputBlocker", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            cover.transform.SetParent(transform, false);
            cover.color = Color.black;
            Stretch(cover.rectTransform);
            picture = new GameObject("Picture", typeof(RectTransform)).GetComponent<RectTransform>();
            picture.SetParent(transform, false);
            picture.sizeDelta = new Vector2(800, 600);
            // 雀卓の手前を回転の支点にする。画像の大きさは変えない。
            picture.pivot = new Vector2(.5f, .35f);
            picture.anchoredPosition = new Vector2(0, -90);
            // 提供されたドット背景は再加工せず描画。人物・雀卓とは別レイヤーにする。
            var backdrop = new GameObject("Background", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            backdrop.transform.SetParent(picture, false);
            Stretch(backdrop.rectTransform);
            backdrop.texture = background;
            backdrop.raycastTarget = false;
            var image = new GameObject("Silhouette", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(picture, false);
            Stretch(image.rectTransform);
            image.texture = closed;
            image.raycastTarget = false;
            Effects.PixelToneUI.Attach(image, silhouetteOnly: true);
            leftEye = Eye(open, new Rect(359, 391, 27, 19), "LeftEye");
            rightEye = Eye(open, new Rect(405, 395, 24, 20), "RightEye");
            eyelids = new GameObject("Eyelids", typeof(RectTransform)).AddComponent<EyelidClosureGraphic>();
            eyelids.transform.SetParent(transform, false);
            Stretch(eyelids.rectTransform);
            eyelids.color = Color.black;
            eyelids.raycastTarget = false;
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0;
            var settings = KillingMahjong.Core.SettingsManager.Instance;
            sound.volume = settings != null ? settings.SeVolume : .5f;
            thud = CreateThud();
        }

        private RectTransform Eye(Texture2D texture, Rect pixels, string name)
        {
            var mask = new GameObject(name, typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            mask.SetParent(picture, false);
            mask.anchoredPosition = pixels.center - new Vector2(400, 300);
            mask.sizeDelta = new Vector2(pixels.width, 0);
            var image = new GameObject("EyePicture", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(mask, false);
            image.rectTransform.sizeDelta = pixels.size;
            image.texture = texture;
            image.uvRect = new Rect(pixels.x / 800, pixels.y / 600, pixels.width / 800, pixels.height / 600);
            image.raycastTarget = false;
            Effects.PixelToneUI.Attach(image);
            return mask;
        }

        private IEnumerator Run()
        {
            StartedAt = Time.unscaledTime;
            Stage = SequenceStage.Silhouette;
            yield return new WaitForSecondsRealtime(.7f);
            Stage = SequenceStage.EyesOpening;
            yield return Animate(.12f, t => {
                float opening = 1 - (1 - t) * (1 - t);
                leftEye.sizeDelta = new Vector2(27, 19 * opening);
                rightEye.sizeDelta = new Vector2(24, 20 * opening);
            });
            Stage = SequenceStage.Laugh;
            // このパスに録音を置けば、間の代わりに笑い声を再生する。
            var laugh = Resources.Load<AudioClip>("UnusedEndings/RedDefeat/laugh");
            if (laugh != null) sound.PlayOneShot(laugh);
            yield return new WaitForSecondsRealtime(laugh != null ? laugh.length : 1.6f);
            Stage = SequenceStage.Tilt;
            yield return Animate(1.1f, t => {
                // 視点が下に落ちるので、雀卓と女の子は画面上方へ流れる。
                ApplyFall(t * t);
                eyelids.Amount = .45f * Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .3f) / .7f));
            });
            Stage = SequenceStage.EyesClosing;
            yield return Animate(.65f, t => {
                ApplyFall(1 + .3f * t);
                eyelids.Amount = .45f + .55f * Mathf.SmoothStep(0, 1, t);
            });
            Stage = SequenceStage.Thud;
            sound.PlayOneShot(thud);
            yield return new WaitForSecondsRealtime(thud.length);
            ThudFinishedAt = Time.unscaledTime;
            Stage = SequenceStage.Wait;
            yield return new WaitForSecondsRealtime(2f);
            CompletedAt = Time.unscaledTime;
            Stage = SequenceStage.Completed;
            Debug.Log($"[RedDefeatPrototypeUI] 終了。着地音の終了から {CompletedAt - ThudFinishedAt:F2} 秒後にタイトルへ戻ります。");
            if (onPreviewCompleted != null) onPreviewCompleted();
            else SceneManager.LoadScene("タイトルシーン");
        }

        private IEnumerator Animate(float duration, System.Action<float> apply)
        {
            float elapsed = 0;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                apply(Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            apply(1);
        }

        private void ApplyFall(float amount)
        {
            picture.localRotation = Quaternion.Euler(0, 0, -26 * amount);
            picture.anchoredPosition = new Vector2(-80 * amount, -90 + 180 * amount);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static AudioClip CreateThud()
        {
            const int rate = 44100;
            var samples = new float[(int)(rate * .55f)];
            var random = new System.Random(607);
            float lowNoise = 0, phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                lowNoise += .12f * ((float)random.NextDouble() * 2 - 1 - lowNoise);
                phase += 2 * Mathf.PI * Mathf.Lerp(78, 38, Mathf.Clamp01(t / .25f)) / rate;
                float attack = Mathf.Clamp01(t / .006f);
                samples[i] = attack * (Mathf.Sin(phase) * .52f * Mathf.Exp(-t * 16)
                    + lowNoise * .65f * Mathf.Exp(-t * 10));
            }
            var clip = AudioClip.Create("RedDefeatThud", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (cursorCanvas != null) cursorCanvas.enabled = cursorWasEnabled;
            if (thud != null) Destroy(thud);
        }
    }
}
