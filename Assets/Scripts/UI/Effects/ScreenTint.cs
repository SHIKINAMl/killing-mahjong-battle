using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 画面全体に色を**かけ続ける**（2026-09-12）。
    ///
    /// **`ScreenFlash` との違いは「一瞬か、続くか」。**
    /// あちらは切り替わりの合図で、呼ばれたら減衰して消える。
    /// こちらは場面の空気そのもので、次に指示するまで掛かったままになる。
    ///
    /// 手法は Inscryption の `ScreenColorEffect` / `FlickerLight` を参考にした。
    /// あちらは小屋の灯りを揺らし続けることで、部屋そのものを演出の一部にしている。
    ///
    /// **シーンには置かない。** `ScreenFlash` と同じ理由で、対局シーンが
    /// `UIテストシーン` と `OpeningScene` の2つあるため、置くと片方に入れ忘れる。
    /// **1つだけ生きる。** 色を変えるたびに作り直すと重なって濃くなるので、
    /// 既にいるものを使い回す。
    ///
    /// `raycastTarget` は**わざと false**。掛かっている間ずっと
    /// 全画面のクリックを吸ってしまうと、その裏のボタンが永久に押せなくなる。
    /// </summary>
    public class ScreenTint : MonoBehaviour
    {
        private static ScreenTint _instance;

        private Image _image;
        private Coroutine _routine;
        private Color _current = new Color(0f, 0f, 0f, 0f);

        private static ScreenTint Ensure()
        {
            if (!Application.isPlaying) return null;
            if (_instance != null) return _instance;

            var go = new GameObject("ScreenTint");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ScreenTint>();
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 根拠は UISortingOrders.ScreenTintLayer のコメントを見ること。
            // 和了選択(97)と同値だが、この2つは同時に存在しないので衝突しない
            canvas.sortingOrder = UISortingOrders.ScreenTintLayer;

            var imageGo = new GameObject("Tint");
            imageGo.transform.SetParent(transform, false);

            _image = imageGo.AddComponent<Image>();
            _image.color = _current;
            _image.raycastTarget = false;

            var rt = _image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 画面に色をかける。`alpha` 0 で透明。
        /// **薄くかけること。** 0.2 を超えると盤面の牌が読みにくくなる。
        /// </summary>
        public static void Set(Color color, float alpha, float fadeSeconds = 0.6f)
        {
            var t = Ensure();
            if (t == null) return;
            t.Go(new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha)), fadeSeconds);
        }

        /// <summary>色を抜く。場面が変わったら必ず呼ぶこと。掛けっぱなしは事故のもと。</summary>
        public static void Clear(float fadeSeconds = 0.6f)
        {
            if (_instance == null) return;
            _instance.Go(new Color(_instance._current.r, _instance._current.g, _instance._current.b, 0f),
                         fadeSeconds);
        }

        /// <summary>
        /// 明滅させる。**灯りが壊れたように、不規則に。**
        /// 等間隔で点滅させると機械の警告灯に見えるので、間隔も濃さもばらす。
        /// 終わったら `toAlpha` に落ち着く。
        /// </summary>
        public static void Flicker(Color color, float seconds, float peakAlpha = 0.5f, float toAlpha = 0f)
        {
            var t = Ensure();
            if (t == null) return;

            if (t._routine != null) t.StopCoroutine(t._routine);
            t._routine = t.StartCoroutine(t.FlickerRoutine(color, seconds, peakAlpha, toAlpha));
        }

        private void Go(Color target, float fadeSeconds)
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }

            if (fadeSeconds <= 0f)
            {
                _current = target;
                if (_image != null) _image.color = target;
                return;
            }
            _routine = StartCoroutine(FadeRoutine(target, fadeSeconds));
        }

        private IEnumerator FadeRoutine(Color target, float seconds)
        {
            Color from = _current;
            float t = 0f;
            // 演出中にスローがかかっても見た目どおりの長さであってほしいので unscaled
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / seconds);
                u = u * u * (3f - 2f * u);
                _current = Color.Lerp(from, target, u);
                if (_image != null) _image.color = _current;
                yield return null;
            }
            _current = target;
            if (_image != null) _image.color = _current;
            _routine = null;
        }

        private IEnumerator FlickerRoutine(Color color, float seconds, float peakAlpha, float toAlpha)
        {
            float t = 0f;
            while (t < seconds)
            {
                // 点いている時間・消えている時間・濃さを毎回ばらす。
                // ここを固定にすると、壊れた灯りではなく点滅するランプになる
                float on = Random.Range(0.02f, 0.07f);
                float off = Random.Range(0.03f, 0.11f);
                float a = peakAlpha * Random.Range(0.45f, 1f);

                _current = new Color(color.r, color.g, color.b, a);
                if (_image != null) _image.color = _current;
                yield return new WaitForSecondsRealtime(on);
                t += on;

                _current = new Color(color.r, color.g, color.b, toAlpha);
                if (_image != null) _image.color = _current;
                yield return new WaitForSecondsRealtime(off);
                t += off;
            }

            _current = new Color(color.r, color.g, color.b, toAlpha);
            if (_image != null) _image.color = _current;
            _routine = null;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
