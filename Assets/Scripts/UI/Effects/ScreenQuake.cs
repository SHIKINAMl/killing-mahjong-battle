using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 画面全体を揺らす（2026-09-13）。
    ///
    /// **揺れは今まで `PhaseTransitionUI` と `VictoryUI` の中に直書きされていた。**
    /// あれは自分のUIだけを動かすもので、外から呼べず、役満と能力発動で
    /// 使い回せなかった。ここへ出して、どこからでも1行で呼べるようにしてある。
    ///
    /// **Overlay のキャンバスは、自分の transform を動かしても画面は動かない。**
    /// Unity が毎フレーム位置を作り直すため。そこで**キャンバスの直下の子**を
    /// ずらしている。孫より下は他のスクリプトが毎フレーム位置を書くことがあり
    /// （`FloatingAnimator` など）、そこを触ると取り合いになる。
    ///
    /// **1つだけ生きる。** 2回呼ばれたら強いほうで上書きする。重ねると
    /// ずれが足し算になって、画面が飛んでいってしまう。
    /// </summary>
    public class ScreenQuake : MonoBehaviour
    {
        private static ScreenQuake _instance;

        /// <summary>揺れ幅の上限[px]。これ以上はどんな役でも揺らさない。</summary>
        private const float MaxPower = 42f;

        /// <summary>1秒あたりの揺れ回数。低いとガタガタ、高いとブルブルになる。</summary>
        private const float Frequency = 38f;

        // **RectTransform ではなく Transform で持つ。**
        // 画面の本体である `Canvas` はただの入れ物で、RectTransform ですらない。
        // `localPosition` なら、どちらの種類でも同じように動かせる。
        private readonly List<Transform> _targets = new List<Transform>();
        private readonly List<Vector3> _origins = new List<Vector3>();
        private Coroutine _routine;
        private float _runningPower;

        private static ScreenQuake Ensure()
        {
            if (!Application.isPlaying) return null;
            if (_instance != null) return _instance;

            var go = new GameObject("ScreenQuake");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ScreenQuake>();
            return _instance;
        }

        /// <summary>
        /// 画面を揺らす。
        /// </summary>
        /// <param name="power">揺れ幅[px]。10 で軽い衝撃、30 を超えると災害級。</param>
        /// <param name="seconds">揺れている長さ。0.2 ほどで「ドン」と一発。</param>
        public static void Play(float power, float seconds = 0.35f)
        {
            var q = Ensure();
            if (q == null) return;
            if (power <= 0f || seconds <= 0f) return;

            // **弱い揺れで強い揺れを上書きしない。** 役満の最中に小さい揺れが
            // 来ても、役満のほうを最後まで見せる
            if (q._routine != null && power <= q._runningPower) return;

            q.StopAndRestore();
            q._runningPower = Mathf.Min(power, MaxPower);
            q._routine = q.StartCoroutine(q.Run(q._runningPower, seconds));
        }

        /// <summary>いま揺れているものを、その場で止めて元に戻す。</summary>
        public static void Stop()
        {
            if (_instance != null) _instance.StopAndRestore();
        }

        private void StopAndRestore()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            Restore();
            _runningPower = 0f;
        }

        /// <summary>
        /// 揺らす相手を集める。**呼ばれるたびに集め直す。**
        /// 局の途中でUIが作られたり消えたりするので、覚えておくと死んだ参照が残る。
        /// </summary>
        private void CollectTargets()
        {
            _targets.Clear();
            _origins.Clear();

            // **`Canvas.isRootCanvas` で拾う（2026-09-13 に2度直した）。**
            //
            // ここで2回間違えた。記録しておく。
            //   1回目: 「Canvas コンポーネントを持つ root の子」を揺らした。
            //          このゲームの画面の本体は `Canvas` という名前の
            //          **Canvas を持たないただの入れ物**なので、丸ごと漏れた。
            //   2回目: その入れ物自体を動かした。**これも効かない。**
            //          入れ物の下の `初期Canvas` などは、親に Canvas が無いため
            //          **それぞれが root canvas 扱い**になり、Unity が毎フレーム
            //          位置を上書きする。親をずらしても無視される。
            //
            // 正解は「root canvas の直下の子」。階層のどこに居ても
            // `isRootCanvas` で判別できる。
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                if (canvas == null || !canvas.isActiveAndEnabled) continue;
                if (!canvas.isRootCanvas) continue;                 // 入れ子は親が動く
                if (canvas.gameObject == gameObject) continue;      // 自分は揺らさない

                string n = canvas.gameObject.name;
                if (n == "ScreenTint" || n == "ScreenFlash") continue;

                for (int i = 0; i < canvas.transform.childCount; i++)
                {
                    var child = canvas.transform.GetChild(i);
                    if (child == null) continue;
                    _targets.Add(child);
                    _origins.Add(child.localPosition);
                }
            }
        }

        private void Restore()
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i] != null) _targets[i].localPosition = _origins[i];
            }
            _targets.Clear();
            _origins.Clear();
        }

        private IEnumerator Run(float power, float seconds)
        {
            CollectTargets();

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                // **1フレームで進める時間に上限を置く（2026-09-13 に直した）。**
                // `Time.unscaledDeltaTime` は、エディタが重いときや録画中に
                // 1秒近くになることがある。そのまま足すと、0.6秒の揺れが
                // **最初の1フレームで終わってしまい、画面には一度も出ない。**
                // 実際、録画したら揺れが1フレームも写っていなかった。
                // ここを抑えておけば、フレームが飛んでも必ず揺れて見える。
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

                // **終わりに向かって細くする。** 同じ幅で揺れ続けると、
                // 止まった瞬間が不自然になる
                float fade = 1f - Mathf.Clamp01(elapsed / seconds);
                float amount = power * fade * fade;

                // 正弦で揺らす。乱数だけだと荒れて安っぽく見えるので、
                // 横は正弦、縦はその倍の速さにして「跳ねる」感じを出す
                float phase = elapsed * Frequency;
                float x = Mathf.Sin(phase) * amount;
                float y = Mathf.Sin(phase * 2.1f) * amount * 0.6f;

                for (int i = 0; i < _targets.Count; i++)
                {
                    if (_targets[i] == null) continue;
                    _targets[i].localPosition = _origins[i] + new Vector3(x, y, 0f);
                }
                yield return null;
            }

            Restore();
            _routine = null;
            _runningPower = 0f;
        }

        private void OnDisable()
        {
            StopAndRestore();
        }
    }
}
