using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>赤い敗北演出。コレクションの旧案とチュートリアルNo1で共用する。本編の勝敗経路には使用しない。</summary>
    public sealed class RedDefeatPrototypeUI : MonoBehaviour
    {
        public enum SequenceStage { Silhouette, EyesOpening, Laugh, Tilt, EyesClosing, Thud, Wait, Completed }
        public SequenceStage Stage { get; private set; }
        public float StartedAt { get; private set; }
        public float ThudFinishedAt { get; private set; }
        public float CompletedAt { get; private set; }

        // 目の範囲（800×600 の絵の上、左下原点）
        private static readonly Rect LeftEyePixels = new Rect(358, 393, 24, 13);
        private static readonly Rect RightEyePixels = new Rect(408, 396, 22, 14);

        // 倒れるときのカット。fall は傾きの進み（ApplyFall）、lids はまぶたの閉じ具合（1 で真っ暗）、hold は開いている秒数。
        // 後のカットほど傾きが進み、目の開きは狭く、見えている時間は短い
        private static readonly (float fall, float lids, float hold)[] FallCuts =
        {
            (.35f, .30f, .55f),
            (.75f, .55f, .45f),
            (1.1f, .80f, .30f),
        };
        private const float BlinkDarkSeconds = .15f;

        private RectTransform picture, leftEye, rightEye;
        private EyelidClosureGraphic eyelids;
        private AudioSource sound;
        private AudioClip thud;
        private Canvas cursorCanvas;
        private bool cursorWasEnabled;
        private System.Action onPreviewCompleted;
        private bool playAudio;

        public static RedDefeatPrototypeUI Play(System.Action onPreviewCompleted = null, bool playAudio = true)
        {
            var existing = FindFirstObjectByType<RedDefeatPrototypeUI>();
            if (existing != null) return existing;
            var closed = Resources.Load<Texture2D>("UnusedEndings/RedDefeat/closed");
            var background = Resources.Load<Texture2D>("UnusedEndings/RedDefeat/background");
            // 目は open.png の赤い形ではなく、通常の立ち絵の目の形だけを赤く塗った絵を使う
            // （2026-10-22 ユーザー指示。open.png の目は下へ垂れた形で、泣いているように見えた）
            var eyes = Resources.Load<Texture2D>("UnusedEndings/RedDefeat/eyes");
            if (closed == null || background == null || eyes == null)
            {
                Debug.LogError("[RedDefeatPrototypeUI] 没案の画像がありません。");
                return null;
            }
            var root = new GameObject("RedDefeatPrototype", typeof(RectTransform));
            var sequence = root.AddComponent<RedDefeatPrototypeUI>();
            sequence.onPreviewCompleted = onPreviewCompleted;
            sequence.playAudio = playAudio;
            sequence.Build(closed, eyes, background);
            sequence.StartCoroutine(sequence.Run());
            return sequence;
        }

        private void Build(Texture2D closed, Texture2D eyes, Texture2D background)
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
            // eyes.png は 通常時身体＋通常顔 をシルエットに重ねて（0.167 倍、左上 (100, 56)）、
            // 上まつげの線より下の白目と瞳のドットだけを赤く塗ったもの。範囲は目の外接矩形に1ドットの余白
            leftEye = Eye(eyes, LeftEyePixels, "LeftEye");
            rightEye = Eye(eyes, RightEyePixels, "RightEye");
            eyelids = new GameObject("Eyelids", typeof(RectTransform)).AddComponent<EyelidClosureGraphic>();
            eyelids.transform.SetParent(transform, false);
            Stretch(eyelids.rectTransform);
            eyelids.color = Color.black;
            eyelids.raycastTarget = false;
            sound = gameObject.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.spatialBlend = 0;
            var settings = KillingMahjong.Core.SettingsManager.Instance;
            sound.volume = playAudio ? (settings != null ? settings.SeVolume : .5f) : 0f;
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
                leftEye.sizeDelta = new Vector2(LeftEyePixels.width, LeftEyePixels.height * opening);
                rightEye.sizeDelta = new Vector2(RightEyePixels.width, RightEyePixels.height * opening);
            });
            Stage = SequenceStage.Laugh;
            // このパスに録音を置けば、間の代わりに笑い声を再生する。
            var laugh = Resources.Load<AudioClip>("UnusedEndings/RedDefeat/laugh");
            if (laugh != null) sound.PlayOneShot(laugh);
            yield return new WaitForSecondsRealtime(laugh != null ? laugh.length : 1.6f);
            // 倒れるところは滑らかに動かさず、まばたきの暗転で区切ったカットでつなぐ
            // （2026-10-22 ユーザー指示「ぬるっとしすぎ」「一カメ、二カメ、三カメという感じで」）。
            // 一瞬暗くなるあいだに視点が傾き、目は一瞬で開く。開くたびに目の開きが狭くなる。
            // 視点が下に落ちるので、雀卓と女の子は画面上方へ流れる。
            Stage = SequenceStage.Tilt;
            foreach (var cut in FallCuts)
            {
                eyelids.Amount = 1f;
                yield return new WaitForSecondsRealtime(BlinkDarkSeconds);
                ApplyFall(cut.fall);
                eyelids.Amount = cut.lids;
                yield return new WaitForSecondsRealtime(cut.hold);
            }
            // 最後は「ばたっ」と同時に暗くする
            Stage = SequenceStage.EyesClosing;
            ApplyFall(1.3f);
            eyelids.Amount = 1f;
            Stage = SequenceStage.Thud;
            sound.PlayOneShot(thud);
            yield return new WaitForSecondsRealtime(thud.length);
            ThudFinishedAt = Time.unscaledTime;
            Stage = SequenceStage.Wait;
            yield return new WaitForSecondsRealtime(2f);
            CompletedAt = Time.unscaledTime;
            Stage = SequenceStage.Completed;
            Debug.Log($"[RedDefeatPrototypeUI] 終了。着地音の終了から {CompletedAt - ThudFinishedAt:F2} 秒後にデバッグ部屋へ移ります。");
            if (onPreviewCompleted != null) onPreviewCompleted();
            else SceneManager.LoadScene(TutorialNavigation.DebugRoomSceneName); // タイトルではなく（2026-10-23 ユーザー指示「タイトルへは放置した時だけ行くようにして」）
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
