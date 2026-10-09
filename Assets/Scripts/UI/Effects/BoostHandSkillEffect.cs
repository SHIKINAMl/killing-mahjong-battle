using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.Managers;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 役強化を使ったあとの演出（2026-10-09 に作り直した）。
    ///
    /// **牌交換と同じ絵の作りにしてある。** 画面を暗く落とし、その上に文字だけを出す。
    ///
    ///   1つ目の版 … 刻む動き・黒い帯・金色の光の柱・「+1翻」の刻印。
    ///                「雰囲気が合っていない。透視や牌交換と合わせて」と言われた
    ///   2つ目の版 … 透視の舞台（青い画面の重ね・集中線）を借りた。
    ///                「透視では周りのやつがいい感じだったが、役強化と強襲には合わない」と言われた
    ///   いまの版   … 周りの重ねと集中線をやめ、牌交換と同じ暗転だけにした
    ///
    ///   幕   … 牌交換と同じ黒い暗転
    ///   中身 … 牌交換の文字の出し方に合わせる。役名が横から滑り込み、
    ///           新しい翻数が上から降りてくる（牌交換の IN と同じ動き・同じ水色）。
    ///           降りきったら一度ふくらむ
    ///   ボード … 対局中、画面の右上に役強化の一覧（<see cref="YakuListUI"/> の強化の枠）がある。
    ///           役名と新しい翻数が小さくなりながらその枠へ飛んでいき、収まった瞬間に白く光って、
    ///           枠が一度ふくらみ、黄色く明滅する（「ここに積まれた」を見せる。ユーザーの指示）。
    ///           枠は演出が届くまで伏せておく（<see cref="YakuListUI.HoldLocalBoostChip"/>）
    ///   音   … 透視と同じ段取り（<see cref="SkillTranceAudio"/>）。BGM が水の中のように沈み、
    ///           白く光って、心音とともに戻る
    ///
    /// 流れ（約3.2秒。うち白く光るまでが約2.1秒）
    ///   ① 役名   … 暗転し、強める役の名前と、いまの翻数が出る
    ///   ② 上がる … 新しい翻数が上から降りてきて、ふくらむ
    ///   ③ 積む   … 役名と新しい翻数が、右上のボードの枠へ飛んでいく
    ///   ④ 解除   … 収まった瞬間に白く光って暗転が消える。ボードの枠がふくらみ、黄色く明滅する
    ///
    /// ボードが無い・枠が見つからないとき（試写の古い舞台など）は、③を飛ばし、
    /// その場で白く光って、役名と新しい翻数が黄色く明滅して消える。
    ///
    /// シーンには置かない。呼ばれるたびに自前の入れ物を作り、終わったら自分を消す。
    /// 出しているのは文字だけ。**AIで作った絵は使っていない。**
    /// </summary>
    public class BoostHandSkillEffect : MonoBehaviour
    {
        // 牌交換（MulliganSwapAnimator）と同じ秒数・同じ色にしてある
        private const float DimAlpha = 0.85f;
        private const float SlideSeconds = 0.30f;
        private const float DropSeconds = 0.30f;
        private static readonly Color InColor = new Color(0.2f, 0.8f, 1f, 1f);

        // 透視（ExposedTileEffectPlayer）と同じ秒数・同じ色にしてある
        private const float PopSeconds = 0.15f;
        private const float HoldBeforeFlash = 0.45f;
        private const float GlowSeconds = 0.9f;
        private static readonly Color GlowColor = new Color(1.0f, 0.8f, 0.2f, 1f);

        private static readonly Color OldHanColor = new Color(0.85f, 0.85f, 0.90f, 1f);

        /// <summary>新しい翻数が出たあとの、古い翻数の濃さ。</summary>
        private const float OldHanFaded = 0.6f;

        /// <summary>ボードの枠へ飛んでいく秒数。</summary>
        private const float FlySeconds = 0.35f;
        /// <summary>収まってから、枠がふくらみ始めるまで（白い光が引くのを待つ）と、ふくらむ秒数。</summary>
        private const float ChipPopDelay = 0.2f;
        private const float ChipPopSeconds = 0.2f;

        private SkillTranceAudio _trance;
        private SkillEffectStage _stage;

        // 途中で打ち切られたときに戻す物
        private YakuListUI _heldBoard;
        private RectTransform _poppedChip;
        private Vector3 _chipScale;

        public static BoostHandSkillEffect Create()
        {
            if (!Application.isPlaying) return null;
            var go = new GameObject("BoostHandSkillEffect");
            return go.AddComponent<BoostHandSkillEffect>();
        }

        /// <summary>途中で止めたいときに。幕も音も片付ける。</summary>
        public void Dispose()
        {
            if (this == null || gameObject == null) return;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // 途中で打ち切られたら、沈めた音はここで戻す（残すと音が沈んだままになる）
            if (_trance != null && !_trance.IsReleased) _trance.Dispose();
            _trance = null;

            // ボードの枠を伏せたまま・ふくらんだままにしない
            if (_heldBoard != null) _heldBoard.ReleaseLocalBoostChip();
            _heldBoard = null;
            if (_poppedChip != null) _poppedChip.localScale = _chipScale;
            _poppedChip = null;
        }

        /// <param name="yakuName">強めた役の名前（サーバーの表記）。分からなければ空でよい</param>
        /// <param name="board">右上の役強化の一覧。演出の最後に、役名と翻数がここの枠へ飛んでいく。無ければ null</param>
        public IEnumerator Play(string yakuName, YakuListUI board = null)
        {
            // 枠は、演出が届くまで伏せておく（呼ぶ側がもう伏せていても、同じことをするだけ）
            if (board != null && !string.IsNullOrEmpty(yakuName))
            {
                board.HoldLocalBoostChip(yakuName);
                _heldBoard = board;
            }

            // いくつからいくつへ上がったか。翻数の表と、いま積んである強化の数から出す
            int baseHan = string.IsNullOrEmpty(yakuName) ? -1 : GameRules.GetBaseHan(yakuName);
            int bonus = 1;
            var state = BoardStateManager.Instance;
            if (state != null && state.LocalBoostHandBonus != null && !string.IsNullOrEmpty(yakuName))
            {
                int stored;
                if (state.LocalBoostHandBonus.TryGetValue(yakuName, out stored) && stored > 0) bonus = stored;
            }
            bool hasNumbers = baseHan > 0;
            int after = baseHan + bonus;
            int before = after - 1;

            // 「么」はフォントに無く □ になるので、対局中の画面と同じ関数を通す
            string shown = string.IsNullOrEmpty(yakuName)
                ? "役強化"
                : YakuNameUtil.ToDisplayText(new YakuNameUtil.Entry { BaseName = yakuName, Boost = 0, Count = 1 });

            _trance = SkillTranceAudio.Begin(SlideSeconds);

            _stage = new SkillEffectStage("Stage", UISortingOrders.SkillEffectContent, transform);
            Image dim = _stage.AddDim("Dim", new Color(0f, 0f, 0f, 0f));

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

            // ---- ① 暗転しながら、役名が横から滑り込む（牌交換の OUT の文字と同じ動き）----
            for (float t = 0f; t < SlideSeconds; t += Time.deltaTime)
            {
                float p = Mathf.Sin(Mathf.Clamp01(t / SlideSeconds) * Mathf.PI * 0.5f);
                dim.color = new Color(0f, 0f, 0f, DimAlpha * p);
                nameText.rectTransform.anchoredPosition = Vector2.Lerp(namePos + new Vector2(-50f, 0f), namePos, p);
                nameText.alpha = p;
                oldText.alpha = p;
                yield return null;
            }
            dim.color = new Color(0f, 0f, 0f, DimAlpha);
            nameText.rectTransform.anchoredPosition = namePos;
            nameText.alpha = 1f;
            oldText.alpha = 1f;

            yield return new WaitForSeconds(0.25f);

            // ---- ② 新しい翻数が上から降りてくる（牌交換の IN と同じ動き）----
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

            // ---- ③ 役名と新しい翻数が、右上のボードの枠へ飛んでいく ----
            RectTransform chip = board != null ? board.LocalBoostChipOf(yakuName) : null;
            if (chip != null)
            {
                Rect chipAt = _stage.LocalRectOf(chip);
                Vector2 nameTo = chipAt.center + new Vector2(0f, chipAt.height * 0.18f);
                Vector2 newTo = chipAt.center - new Vector2(0f, chipAt.height * 0.25f);
                // 枠に収まる大きさまで縮める
                float nameShrink = Mathf.Clamp(chipAt.width * 0.9f / Mathf.Max(1f, nameText.preferredWidth), 0.08f, 0.6f);
                float newShrink = Mathf.Clamp(chipAt.height * 0.5f / Mathf.Max(1f, newText.fontSize), 0.08f, 0.6f);

                for (float t = 0f; t < FlySeconds; t += Time.deltaTime)
                {
                    float p = Mathf.Clamp01(t / FlySeconds);
                    float eased = p * p;                       // 出だしはゆっくり、吸い込まれるように速く
                    nameText.rectTransform.anchoredPosition = Vector2.Lerp(namePos, nameTo, eased);
                    nameText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, nameShrink, eased);
                    newText.rectTransform.anchoredPosition = Vector2.Lerp(newPos, newTo, eased);
                    newText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, newShrink, eased);
                    float fade = 1f - Mathf.Clamp01(p * 3f);   // 古い翻数と矢印は、その場で先に消える
                    oldText.alpha = OldHanFaded * fade;
                    arrowText.alpha = fade;
                    yield return null;
                }
                nameText.alpha = 0f;
                newText.alpha = 0f;
                oldText.alpha = 0f;
                arrowText.alpha = 0f;

                // ---- ④ 収まった。枠を出して、白く光る ----
                board.ReleaseLocalBoostChip();
                _heldBoard = null;
                if (audio != null) audio.PlaySynthSound(SynthWaveType.Sine, 780f, 1170f, 0.14f, 0.5f);
                ReleaseTrance(dim);

                _poppedChip = chip;
                _chipScale = chip.localScale;
                var glow = _stage.AddShapes("ChipGlow");
                for (float t = 0f; t < GlowSeconds; t += Time.deltaTime)
                {
                    if (t >= 0.12f) dim.color = Color.clear;

                    // 枠が一度ふくらむ（透視で牌を返した瞬間と同じ）。
                    // 白い光が引いてからでないと見えないので、少し遅らせる
                    float popAt = t - ChipPopDelay;
                    float pop = popAt >= 0f && popAt < ChipPopSeconds ? Mathf.PingPong(popAt * (1f / (ChipPopSeconds / 2f)), 1f) : 0f;
                    chip.localScale = _chipScale * Mathf.Lerp(1f, 1.4f, pop);

                    // 黄色く明滅する（透視の「見えた牌の明滅」と同じ色・同じ速さ）
                    Rect at = SkillEffectStage.Expand(_stage.LocalRectOf(chip), 2f);
                    float blink = Mathf.PingPong(t * 3f, 1f);
                    Color edge = GlowColor;
                    Color face = GlowColor;
                    face.a = 0.45f * blink;
                    glow.Begin();
                    glow.Box(at, face);
                    glow.Box(at.xMin - 2f, at.yMin - 2f, at.xMax + 2f, at.yMin, edge);
                    glow.Box(at.xMin - 2f, at.yMax, at.xMax + 2f, at.yMax + 2f, edge);
                    glow.Box(at.xMin - 2f, at.yMin, at.xMin, at.yMax, edge);
                    glow.Box(at.xMax, at.yMin, at.xMax + 2f, at.yMax, edge);
                    glow.End();
                    yield return null;
                }
                chip.localScale = _chipScale;
                _poppedChip = null;
                dim.color = Color.clear;

                Dispose();
                yield break;
            }

            // ---- ボードが無いとき。その場で白く光って暗転が消える ----
            ReleaseTrance(dim);
            yield return new WaitForSeconds(0.12f);
            dim.color = Color.clear;

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

        /// <summary>白く光って、沈んでいた音を抜く。心音と BGM の戻りは待たない。光が乗りきったら暗転を消す。</summary>
        private void ReleaseTrance(Image dim)
        {
            if (_trance != null)
            {
                var trance = _trance;
                _trance = null;            // ここから先は音の部品が自分で鳴らしきる
                trance.ReleaseDetached(() => { if (dim != null) dim.color = Color.clear; });
            }
            else
            {
                ScreenFlash.Play();
            }
        }
    }
}
