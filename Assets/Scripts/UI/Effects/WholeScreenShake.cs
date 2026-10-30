using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 画面ぜんたいを揺らす（2026-10-30。プランナーに縦揺れの例を見せたら「どこが揺れとるんや」）。
    ///
    /// それまでのチュートリアルの揺れは <see cref="PhaseTransitionUI"/> 自身を動かしていて、
    /// その中身（黒帯・市松模様）が出ていない場面では何も動いて見えなかった。
    /// 画面は Canvas が40枚ほどに分かれているので、表示中の一番上の Canvas の**直下の子**を同じだけずらし、終わったら戻す。
    /// 揺れの間に位置を書き換える部品があっても、終わりに戻す位置は揺らす前の位置。
    /// </summary>
    public sealed class WholeScreenShake : MonoBehaviour
    {
        private static WholeScreenShake host;
        private readonly Dictionary<Transform, Vector3> origins = new Dictionary<Transform, Vector3>();
        private readonly HashSet<Transform> keepRightCovered = new HashSet<Transform>();
        private float until, magnitude;
        private Vector2 axis;
        private Coroutine running;

        /// <param name="duration">揺らす秒数</param>
        /// <param name="strength">振れ幅（画面の単位。800x600 基準）</param>
        /// <param name="direction">(0,1) なら縦だけ、(1,0) なら横だけ</param>
        public static void Play(float duration, float strength, Vector2 direction)
        {
            if (host == null)
            {
                var go = new GameObject("WholeScreenShake");
                DontDestroyOnLoad(go);
                host = go.AddComponent<WholeScreenShake>();
            }
            host.Begin(duration, strength, direction);
        }

        private void Begin(float duration, float strength, Vector2 direction)
        {
            until = Mathf.Max(running != null ? until : 0f, Time.unscaledTime + duration);
            magnitude = running != null ? Mathf.Max(magnitude, strength) : strength;
            axis = running != null ? Vector2.Max(axis, direction) : direction;
            if (running == null) running = StartCoroutine(Shake());
        }

        private IEnumerator Shake()
        {
            origins.Clear();
            keepRightCovered.Clear();
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas) continue;
                for (int i = 0; i < canvas.transform.childCount; i++)
                {
                    var child = canvas.transform.GetChild(i);
                    if (!origins.ContainsKey(child)) origins[child] = child.localPosition;
                    if (ScreenShakeBounds.KeepRightEdgeCovered(child)) keepRightCovered.Add(child);
                }
            }
            int frame = -1;
            try
            {
                while (Time.unscaledTime < until)
                {
                    // 毎フレームなめらかに動かさず、ドットの絵に合わせてコマ打ちで揺らす
                    if (PresentationTiming.IsNewFrame(ref frame, Time.unscaledTime))
                    {
                        // 端から端へ振る。ランダムより「ガタッ」と読める。2単位（1ドット）にそろえる
                        float sign = (frame & 1) == 0 ? 1f : -1f;
                        float fade = Mathf.Clamp01((until - Time.unscaledTime) / .2f);
                        var offset = new Vector3(axis.x, axis.y, 0f) * Mathf.Round(sign * magnitude * Mathf.Max(.4f, fade) / 2f) * 2f;
                        foreach (var pair in origins) if (pair.Key != null)
                            pair.Key.localPosition = pair.Value + ScreenShakeBounds.Constrain(offset, keepRightCovered.Contains(pair.Key));
                    }
                    yield return null;
                }
            }
            finally
            {
                foreach (var pair in origins) if (pair.Key != null) pair.Key.localPosition = pair.Value;
                origins.Clear();
                keepRightCovered.Clear();
                running = null;
            }
        }
    }
}
