using System.Collections;
using TMPro;
using UnityEngine;
using KillingMahjong.Common;
using KillingMahjong.Managers;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 役強化を使ったあとの演出（2026-10-09 に作り直した）。
    ///
    /// **透視・牌交換と同じ絵の作りにしてある。**
    /// 最初の版は、刻む動き・黒い帯・金色の光の柱・「+1翻」の刻印という別の作りで、
    /// 「雰囲気が合っていない。透視や牌交換と合わせて」と言われた（ユーザーの指摘）。
    ///
    ///   舞台 … 透視のものをそのまま借りる（<see cref="PerspectiveSkillEffect"/>）。
    ///           その瞬間の画面を青くして周りに重ね、暗く落とし、集中線を真ん中へ集める。
    ///           BGM が水の中のように沈み、終わりに白く光って、心音とともに戻る
    ///   中身 … 牌交換の文字の出し方に合わせる。役名が横から滑り込み、
    ///           新しい翻数が上から降りてくる（牌交換の IN と同じ動き・同じ水色）。
    ///           降りきったら透視の牌と同じように一度ふくらみ、白く光ったあと黄色く明滅する
    ///
    /// 流れ（約3.5秒。うち白く光るまでが約2.3秒）
    ///   ① 集中   … 舞台が出る
    ///   ② 役名   … 強める役の名前と、いまの翻数が出る
    ///   ③ 上がる … 新しい翻数が上から降りてきて、ふくらむ
    ///   ④ 解除   … 白く光って舞台が消える。役名と新しい翻数は残って黄色く明滅し、消える
    ///
    /// シーンには置かない。呼ばれるたびに自前の入れ物を作り、終わったら自分を消す。
    /// 出しているのは実機の画面の複製と、文字だけ。**AIで作った絵は使っていない。**
    /// </summary>
    public class BoostHandSkillEffect : MonoBehaviour
    {
        // 牌交換（MulliganSwapAnimator）と同じ秒数・同じ色にしてある
        private const float SlideSeconds = 0.30f;
        private const float DropSeconds = 0.30f;
        private static readonly Color InColor = new Color(0.2f, 0.8f, 1f, 1f);

        // 透視（ExposedTileEffectPlayer）と同じ秒数・同じ色にしてある
        private const float PopSeconds = 0.15f;
        private const float HoldBeforeFlash = 0.45f;
        private const float GlowSeconds = 0.9f;
        private static readonly Color GlowColor = new Color(1.0f, 0.8f, 0.2f, 1f);

        private static readonly Color OldHanColor = new Color(0.85f, 0.85f, 0.90f, 1f);

        /// <summary>文字の後ろ（集中線の集まる先）を暗くする量。</summary>
        private const float FocusDim = 0.55f;
        /// <summary>新しい翻数が出たあとの、古い翻数の濃さ。</summary>
        private const float OldHanFaded = 0.6f;

        private PerspectiveSkillEffect _backdrop;
        private SkillEffectStage _stage;

        public static BoostHandSkillEffect Create()
        {
            if (!Application.isPlaying) return null;
            var go = new GameObject("BoostHandSkillEffect");
            return go.AddComponent<BoostHandSkillEffect>();
        }

        /// <summary>途中で止めたいときに。舞台も音も片付ける。</summary>
        public void Dispose()
        {
            if (this == null || gameObject == null) return;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // 舞台は別の入れ物なので、一緒に消す（残すと画面が青いまま・音が沈んだままになる）
            if (_backdrop != null) _backdrop.Dispose();
            _backdrop = null;
        }

        /// <param name="yakuName">強めた役の名前（サーバーの表記）。分からなければ空でよい</param>
        public IEnumerator Play(string yakuName)
        {
            // いくつからいくつへ上がったか。翻数の表と、いま積んである強化の数から出す
            int baseHan = string.IsNullOrEmpty(yakuName) ? -1 : GameRules.GetBaseHan(yakuName);
            int bonus = 1;
            var board = BoardStateManager.Instance;
            if (board != null && board.LocalBoostHandBonus != null && !string.IsNullOrEmpty(yakuName))
            {
                int stored;
                if (board.LocalBoostHandBonus.TryGetValue(yakuName, out stored) && stored > 0) bonus = stored;
            }
            bool hasNumbers = baseHan > 0;
            int after = baseHan + bonus;
            int before = after - 1;

            // 「么」はフォントに無く □ になるので、対局中の画面と同じ関数を通す
            string shown = string.IsNullOrEmpty(yakuName)
                ? "役強化"
                : YakuNameUtil.ToDisplayText(new YakuNameUtil.Entry { BaseName = yakuName, Boost = 0, Count = 1 });

            // ---- ① 集中。舞台は透視と同じ ----
            // 集める先は画面の真ん中（役名と翻数を出す所）
            var focus = new Rect(Screen.width * 0.20f, Screen.height * 0.40f,
                                 Screen.width * 0.60f, Screen.height * 0.22f);
            _backdrop = PerspectiveSkillEffect.Create(PerspectiveSkillEffect.Tone.Blue);
            if (_backdrop != null)
            {
                // 文字は穴の中（相手の立ち絵と血袋の上）に出る。明るいままだと「清」が血袋に埋もれた
                _backdrop.FocusDim = FocusDim;
                yield return _backdrop.Enter(focus);
            }

            // 中身は舞台を撮ったあとに作る（先に作ると、重ねる画面に文字が写り込む）
            _stage = new SkillEffectStage("Content", UISortingOrders.SkillEffectContent, transform);

            float nameSize = Mathf.Clamp(560f / Mathf.Max(shown.Length, 1), 52f, 96f);
            var nameText = _stage.AddText("YakuName", shown, nameSize, Color.white);
            nameText.fontStyle = FontStyles.Bold;
            nameText.alpha = 0f;

            var oldText = _stage.AddText("OldHan", hasNumbers ? before + "翻" : "", 54f, OldHanColor);
            oldText.fontStyle = FontStyles.Bold;
            oldText.alpha = 0f;
            var arrowText = _stage.AddText("Arrow", hasNumbers ? "→" : "", 44f, OldHanColor);
            arrowText.alpha = 0f;
            var newText = _stage.AddText("NewHan", hasNumbers ? after + "翻" : "+1翻", 78f, InColor);
            newText.fontStyle = FontStyles.Bold;
            newText.alpha = 0f;

            Vector2 namePos = new Vector2(0f, 52f);
            Vector2 oldPos = new Vector2(-118f, -44f);
            Vector2 arrowPos = new Vector2(-28f, -44f);
            Vector2 newPos = hasNumbers ? new Vector2(86f, -44f) : new Vector2(0f, -44f);
            oldText.rectTransform.anchoredPosition = oldPos;
            arrowText.rectTransform.anchoredPosition = arrowPos;

            var audio = AudioManager.Instance;

            // ---- ② 役名が横から滑り込む（牌交換の OUT の文字と同じ動き）----
            for (float t = 0f; t < SlideSeconds; t += Time.deltaTime)
            {
                float p = Mathf.Sin(Mathf.Clamp01(t / SlideSeconds) * Mathf.PI * 0.5f);
                nameText.rectTransform.anchoredPosition = Vector2.Lerp(namePos + new Vector2(-50f, 0f), namePos, p);
                nameText.alpha = p;
                oldText.alpha = p;
                yield return null;
            }
            nameText.rectTransform.anchoredPosition = namePos;
            nameText.alpha = 1f;
            oldText.alpha = 1f;

            yield return new WaitForSeconds(0.25f);

            // ---- ③ 新しい翻数が上から降りてくる（牌交換の IN と同じ動き）----
            if (audio != null) audio.PlaySynthSound(SynthWaveType.Sine, 520f, 780f, 0.28f, 0.45f);
            for (float t = 0f; t < DropSeconds; t += Time.deltaTime)
            {
                float p = Mathf.Sin(Mathf.Clamp01(t / DropSeconds) * Mathf.PI * 0.5f);
                newText.rectTransform.anchoredPosition = Vector2.Lerp(newPos + new Vector2(0f, 420f), newPos, p);
                newText.alpha = p;
                arrowText.alpha = p;
                oldText.alpha = Mathf.Lerp(1f, OldHanFaded, p);   // 古い値は一歩下がる
                yield return null;
            }
            newText.rectTransform.anchoredPosition = newPos;
            newText.alpha = 1f;
            arrowText.alpha = 1f;

            // 降りきったら一度ふくらむ（透視で牌を返した瞬間と同じ）
            for (float t = 0f; t < PopSeconds; t += Time.deltaTime)
            {
                float s = Mathf.Lerp(1f, 1.3f, Mathf.PingPong(t * (1f / (PopSeconds / 2f)), 1f));
                newText.rectTransform.localScale = Vector3.one * s;
                yield return null;
            }
            newText.rectTransform.localScale = Vector3.one;

            yield return new WaitForSeconds(HoldBeforeFlash);

            // ---- ④ 白く光って舞台が消える。心音と BGM の戻りは待たない ----
            if (_backdrop != null)
            {
                var backdrop = _backdrop;
                _backdrop = null;          // ここから先は舞台が自分で片付く
                yield return backdrop.ReleaseQuick();
            }
            else
            {
                ScreenFlash.Play();
            }

            // 何が強まったかを、戻った画面の上でもう一度見せる（透視の「見えた牌の明滅」と同じ）
            for (float t = 0f; t < GlowSeconds; t += Time.deltaTime)
            {
                Color c = Color.Lerp(Color.white, GlowColor, Mathf.PingPong(t * 3f, 1f));
                nameText.color = c;
                newText.color = Color.Lerp(InColor, GlowColor, Mathf.PingPong(t * 3f, 1f));
                yield return null;
            }

            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                float a = 1f - Mathf.Clamp01(t / 0.25f);
                nameText.alpha = a;
                newText.alpha = a;
                arrowText.alpha = a;
                oldText.alpha = OldHanFaded * a;
                yield return null;
            }

            Dispose();
        }
    }
}
