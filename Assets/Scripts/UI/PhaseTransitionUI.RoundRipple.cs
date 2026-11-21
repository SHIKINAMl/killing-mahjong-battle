using System.Collections;
using TMPro;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 局頭の暗転の「第N局目」で、数字を送ってから波紋を出す（2026-11-21 のユーザー指示
    /// 「日付が変わった後数字から波紋が出る感じ…こんな感じの演出入れてほしい」。参考は画面録画の日付送り）。
    ///
    ///   0.00s  「第(N-1)局目」が、いつもどおりその場でぱっと出る
    ///   0.35s  数字だけが上へ抜けて消え、下から新しい数字が入って止まる（約0.3秒）
    ///   0.65s  止まった数字から、輪が3重に広がって薄れる（約0.8秒）
    ///
    /// 第1局は送る前の数字が無いので、波紋だけを出す。
    ///
    /// **本編の出し方（2026-11-21 に本番の流れで確認して直した）:**
    ///   ・第1局: 暗転の文字は「対局開始」で、配牌待ちの差し替えで「第1局進行中...」が出る → 差し替え（SwapDarkenText）で波紋
    ///   ・第2局以降: 暗転の「第N局...」が出た同じフレームに「第N局進行中...」への差し替えが来る →
    ///     止めずに受け（<see cref="TryDeferDarkenTextForRipple"/>）、「第N-1局進行中...」から送る
    /// 輪はドット絵の格子に寄せた点で描き、色は象牙色と血の赤（役名表示と同じ色）。
    ///
    /// 輪を描く板（<see cref="roundNumberRipple"/>）はシーンに置いてある。**置いていないシーン
    /// （チュートリアル）では、数字送りも波紋も出さず今までどおりにする。**
    /// 文字は 1 字ずつ浮かせない（2026-10-26 のユーザー指示「その場でぱっと出す」）。動くのは数字だけ。
    /// </summary>
    public partial class PhaseTransitionUI
    {
        [SerializeField, Tooltip("局頭の「第N局目」の数字から出る波紋を描く板。空なら数字送りも波紋も出さない")]
        private Effects.PixelShapeGraphic roundNumberRipple;

        private const float RoundFlipDelay = 0.35f;
        private const float RoundFlipOutSeconds = 0.12f;
        private const float RoundFlipInSeconds = 0.18f;
        /// <summary>数字が上下に動く距離（文字の高さに対する割合）。</summary>
        private const float RoundFlipTravel = 0.6f;

        private const int RippleCount = 3;
        private const float RippleInterval = 0.12f;
        private const float RippleSeconds = 0.8f;
        /// <summary>輪の広がり（数字の高さに対する倍率）。</summary>
        private const float RippleStartRadius = 0.6f;
        private const float RippleEndRadius = 2.6f;
        /// <summary>ドットの格子と、輪の太さ（Canvas の単位）。</summary>
        private const float RippleGrid = 4f;
        private const float RippleThicknessStart = 6f;
        private const float RippleThicknessEnd = 3f;

        private static readonly Color RippleBone = new Color(0.96f, 0.90f, 0.80f, 1f);
        private static readonly Color RippleBlood = new Color(0.80f, 0.05f, 0.10f, 1f);

        /// <summary>数字送りと波紋を出せるか（輪の板がシーンにあり、文字に数字があるか）。</summary>
        private bool CanPlayRoundNumberRipple(string text)
        {
            return roundNumberRipple != null && centerText != null && FindDigits(text, out _, out _);
        }

        /// <summary>
        /// 暗転の最初に出す文字。第2局以降は、ひとつ前の局の数字にしておく（あとで送る）。
        /// 出せないときは渡された文字のまま。
        /// </summary>
        private string RoundTitleBeforeFlip(string text)
        {
            if (!CanPlayRoundNumberRipple(text)) return text;
            FindDigits(text, out int start, out int length);
            int number = ParseDigits(text, start, length);
            if (number < 2) return text;
            bool fullWidth = text[start] >= '０' && text[start] <= '９';
            return text.Substring(0, start) + FormatDigits(number - 1, fullWidth) + text.Substring(start + length);
        }

        /// <summary>
        /// 数字送りと波紋の最中か。最中に来た文字の差し替え（<see cref="ChangeDarkenText"/>）は、
        /// 波紋を止めずに <see cref="TryDeferDarkenTextForRipple"/> で受ける。
        /// </summary>
        private bool _roundRippleRunning;
        /// <summary>数字を送ったあとの文字。送る前に差し替えが来たら、こちらを書き換える。</summary>
        private string _roundRippleTarget;
        /// <summary>もう新しい数字へ送り終えたか。</summary>
        private bool _roundRippleFlipped;
        /// <summary>送り終えたあとに来た差し替え。波紋が終わってから、いつもの差し替えで出す。</summary>
        private string _pendingDarkenText;

        /// <summary>
        /// 波紋の最中に来た文字の差し替えを受ける（受けたら true）。
        ///
        /// **本編は、局名を出した同じフレームで「第N局進行中...」へ差し替えに来る**（配牌待ちの表示、
        /// GameUIPhaseController.ShowDealingWaitUI）。以前はここで波紋のコルーチンを止めていたので、
        /// 本編では数字送りも波紋も一度も出ていなかった（2026-11-21「波紋が出ていないような気がします」）。
        ///   ・まだ数字を送る前: 差し替え後の文字の「ひとつ前の数字」に置き直し、そのまま差し替え後の文字へ送る
        ///   ・送り終えたあと: 波紋が終わってから、いつもの差し替え（薄くして出し直す）をする
        /// </summary>
        private bool TryDeferDarkenTextForRipple(string text)
        {
            if (!_roundRippleRunning) return false;
            if (!_roundRippleFlipped && CanPlayRoundNumberRipple(text))
            {
                centerText.text = RoundTitleBeforeFlip(text);
                centerText.ForceMeshUpdate();
                _roundRippleTarget = text;
            }
            else if (_roundRippleTarget != text)
            {
                _pendingDarkenText = text;
            }
            return true;
        }

        /// <summary>波紋の状態を忘れる（暗転の取り消しのとき）。</summary>
        private void ResetRoundRippleState()
        {
            _roundRippleRunning = false;
            _roundRippleFlipped = false;
            _roundRippleTarget = null;
            _pendingDarkenText = null;
        }

        /// <summary>数字送り（必要なら）と波紋。<paramref name="text"/> は送ったあとの文字（本来の局名）。</summary>
        private IEnumerator RoundNumberRippleRoutine(string text)
        {
            _roundRippleRunning = true;
            _roundRippleFlipped = false;
            _roundRippleTarget = text;
            _pendingDarkenText = null;

            // 同じフレームに来る差し替え（上の説明）を先に受けてから、送るかどうかを決める
            yield return null;
            bool flip = centerText.text != _roundRippleTarget;

            if (flip)
            {
                for (float w = 0f; w < RoundFlipDelay; w += Time.unscaledDeltaTime) yield return null;

                // 古い数字を上へ抜いて消す
                yield return MoveDigits(RoundFlipOutSeconds, u => new Vector2(u * u * RoundFlipTravel, 1f - u));

                // 新しい数字に差し替え、下から入れて止める（速く入って減速）
                centerText.text = _roundRippleTarget;
                _roundRippleFlipped = true;
                centerText.ForceMeshUpdate();
                yield return MoveDigits(RoundFlipInSeconds, u =>
                {
                    float e = 1f - (1f - u) * (1f - u) * (1f - u);
                    return new Vector2(-(1f - e) * RoundFlipTravel, Mathf.Clamp01(u * 2f));
                });
                centerText.ForceMeshUpdate();
            }
            _roundRippleFlipped = true;

            // ---- 波紋 ----
            if (!DigitBounds(out Vector2 center, out float height)) { FinishRoundRipple(); yield break; }

            roundNumberRipple.gameObject.SetActive(true);
            float total = RippleSeconds + RippleInterval * (RippleCount - 1);
            for (float t = 0f; t < total; t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f))
            {
                float st = PresentationTiming.Sample(t);
                roundNumberRipple.Begin();
                for (int k = 0; k < RippleCount; k++)
                {
                    float u = (st - k * RippleInterval) / RippleSeconds;
                    if (u < 0f || u > 1f) continue;
                    float e = 1f - (1f - u) * (1f - u);
                    float radius = height * Mathf.Lerp(RippleStartRadius, RippleEndRadius, e);
                    float thick = Mathf.Round(Mathf.Lerp(RippleThicknessStart, RippleThicknessEnd, u) / 1f);
                    var c = k == 1 ? RippleBlood : RippleBone;
                    c.a = Mathf.Pow(1f - u, 1.4f);
                    roundNumberRipple.PixelRing(center, radius, thick, RippleGrid, c);
                }
                roundNumberRipple.End();
                yield return null;
            }

            FinishRoundRipple();
        }

        private void FinishRoundRipple()
        {
            ClearRoundRipple();
            // 送り終えた数字を最低でも少し見せてから晴らす
            roundStartTextVisibleUntil = Mathf.Max(roundStartTextVisibleUntil, Time.unscaledTime + 0.25f);
            string pending = _pendingDarkenText;
            ResetRoundRippleState();
            // 送り終えたあとに来た差し替えは、ここで出す（晴らす側は darkenTextRoutine が終わるまで待つ）
            darkenTextRoutine = pending != null && centerText != null && centerText.text != pending && isActiveAndEnabled
                ? StartCoroutine(SwapDarkenText(pending))
                : null;
        }

        /// <summary>輪を消す（暗転の取り消し・文字の差し替え・明転のとき）。</summary>
        private void ClearRoundRipple()
        {
            if (roundNumberRipple == null) return;
            roundNumberRipple.Begin();
            roundNumberRipple.End();
        }

        /// <summary>
        /// いま出ている文字の数字だけを動かす。<paramref name="pose"/> は 0〜1 の進みから
        /// （上へのずれ［文字の高さに対する割合］, 濃さ）を返す。文字の形はそのたびに作り直して当てる。
        /// </summary>
        private IEnumerator MoveDigits(float seconds, System.Func<float, Vector2> pose)
        {
            for (float t = 0f; ; t += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f))
            {
                float u = Mathf.Clamp01(PresentationTiming.Sample(t) / seconds);
                ApplyDigitPose(pose(u));
                if (u >= 1f) yield break;
                yield return null;
            }
        }

        private void ApplyDigitPose(Vector2 pose)
        {
            centerText.ForceMeshUpdate();
            var info = centerText.textInfo;
            if (!FindDigits(centerText.text, out int start, out int length)) return;
            float lift = centerText.fontSize * pose.x;
            byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(pose.y));
            for (int i = start; i < start + length && i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                var mesh = info.meshInfo[ch.materialReferenceIndex];
                for (int v = 0; v < 4; v++)
                {
                    mesh.vertices[ch.vertexIndex + v] += new Vector3(0f, lift, 0f);
                    var col = mesh.colors32[ch.vertexIndex + v];
                    col.a = (byte)(col.a * alpha / 255);
                    mesh.colors32[ch.vertexIndex + v] = col;
                }
            }
            centerText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        /// <summary>数字の中心（輪の板の座標）と、数字の高さ。</summary>
        private bool DigitBounds(out Vector2 center, out float height)
        {
            center = Vector2.zero; height = 0f;
            var info = centerText.textInfo;
            if (!FindDigits(centerText.text, out int start, out int length)) return false;
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue), max = new Vector3(float.MinValue, float.MinValue);
            for (int i = start; i < start + length && i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                min = Vector3.Min(min, ch.bottomLeft); max = Vector3.Max(max, ch.topRight);
            }
            if (min.x > max.x) return false;

            var rippleRect = roundNumberRipple.rectTransform;
            Vector3 worldMin = centerText.rectTransform.TransformPoint(min);
            Vector3 worldMax = centerText.rectTransform.TransformPoint(max);
            Vector2 a = rippleRect.InverseTransformPoint(worldMin), b = rippleRect.InverseTransformPoint(worldMax);
            center = (a + b) * 0.5f;
            // 輪の点を格子に乗せるため、中心も格子に寄せる
            center = new Vector2(Mathf.Round(center.x / RippleGrid) * RippleGrid, Mathf.Round(center.y / RippleGrid) * RippleGrid);
            height = Mathf.Abs(b.y - a.y);
            return height > 0f;
        }

        /// <summary>文字の中の最初の数字の並び（半角・全角）。</summary>
        private static bool FindDigits(string text, out int start, out int length)
        {
            start = -1; length = 0;
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (!IsDigit(text[i])) { if (start >= 0) break; continue; }
                if (start < 0) start = i;
                length++;
            }
            return start >= 0;
        }

        private static bool IsDigit(char c) => (c >= '0' && c <= '9') || (c >= '０' && c <= '９');

        private static int ParseDigits(string text, int start, int length)
        {
            int n = 0;
            for (int i = start; i < start + length; i++)
            {
                char c = text[i];
                n = n * 10 + (c >= '０' ? c - '０' : c - '0');
            }
            return n;
        }

        private static string FormatDigits(int number, bool fullWidth)
        {
            string s = number.ToString();
            if (!fullWidth) return s;
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++) chars[i] = (char)('０' + (chars[i] - '0'));
            return new string(chars);
        }
    }
}
