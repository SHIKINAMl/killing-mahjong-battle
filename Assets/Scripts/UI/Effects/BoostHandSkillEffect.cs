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
    /// **悪魔（ベルル）を呼び出して、力を借りる**（ユーザーの指示）。
    /// ベルルは、スキル一覧の背景と、自分のスキルのカットインに出ている、板を持った白い悪魔。
    ///
    ///   1つ目の版 … 刻む動き・黒い帯・金色の光の柱・「+1翻」の刻印。
    ///                「雰囲気が合っていない。透視や牌交換と合わせて」と言われた
    ///   2つ目の版 … 透視の舞台（青い画面の重ね・集中線）を借りた。
    ///                「透視では周りのやつがいい感じだったが、役強化と強襲には合わない」と言われた
    ///   3つ目の版 … 牌交換と同じ暗転の上に文字だけ。最後に右上の一覧ボードへ飛んでいく
    ///   いまの版   … そこへベルルを足した。「その悪魔の力を借りてる風の演出を使いたい」と言われた
    ///
    ///   幕     … 牌交換と同じ黒い暗転
    ///   ベルル … **絵はもとからある物だけ。** スキル一覧が開くときのコマ（`Resources/UI_Anim1〜7`。
    ///             黄色い輪が描かれて、そこからベルルが出てくる）と、カットインの絵
    ///             （`skill_cutin_player`。同じ絵で、目が黄色く光っている）。
    ///             この2つは同じ大きさの同じ絵なので、重ねる位置を合わせて差し替えると「目が光った」に見える
    ///   文字   … 役名と今の翻数は牌交換の文字と同じ出し方。新しい翻数は、ベルルの板から降りてくる
    ///   ボード … 役名と新しい翻数が、右上の役強化の一覧（<see cref="YakuListUI"/>）の枠へ飛んでいく。
    ///             枠は演出が届くまで伏せておく（<see cref="YakuListUI.HoldLocalBoostChip"/>）。
    ///             強化が枠（3つ）より多くて「+2」（あと2件）にまとめられているときは、その枠へ飛び、
    ///             収まってからしばらくだけ、その枠に強めた役を名前で出す
    ///   音     … 透視と同じ段取り（<see cref="SkillTranceAudio"/>）
    ///
    /// 流れ（約4秒。うち白く光るまでが約2.9秒）
    ///   ① 役名   … 暗転し、強める役の名前と、いまの翻数が出る
    ///   ② 呼ぶ   … 黄色い輪が描かれ、ベルルが出てくる
    ///   ③ 借りる … ベルルの目が光り、板に「+1翻」が出る
    ///   ④ 上がる … 「+1翻」が板から降りてきて、翻数が1つ上がる
    ///   ⑤ 積む   … 役名と新しい翻数が右上のボードの枠へ飛んでいく。ベルルは輪へ帰る
    ///   ⑥ 解除   … 収まった瞬間に白く光って暗転が消える。ボードの枠がふくらみ、黄色く明滅する
    ///
    /// ベルルの絵が読めないときは②③を飛ばし、新しい翻数は上から降りてくる（3つ目の版と同じ）。
    /// ボードが無い・枠が見つからないときは⑤を飛ばし、その場で白く光って、役名と新しい翻数が黄色く明滅して消える。
    ///
    /// シーンには置かない。呼ばれるたびに自前の入れ物を作り、終わったら自分を消す。
    /// **AIで作った絵は使っていない。**
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
        /// <summary>
        /// 強化が枠より多いとき、明滅が終わったあとも「あと何件」の枠に役名を残しておく秒数
        /// （<see cref="YakuListUI.SpotlightLocalBoost"/>。戻すのはボードの側で、演出は待たない）。
        /// </summary>
        private const float SpotlightLinger = 0.9f;

        // ---- ベルル ----
        //
        // 絵の寸法（測った値。2026-10-09）
        //   コマ（UI_Anim1〜10）  … 1010x1570。ベルルが居るのは上のほうの (230,50)-(850,500)
        //   カットインの絵          … 660x510。ベルルが居るのは (30,60)-(650,510)
        // どちらも同じ絵（620x450）で、コマの (x+200, y-10) がカットインの (x, y) に当たる。

        /// <summary>コマの名前（Resources）。1〜4 で輪が描かれ、5 で転がり出て、6・7 で板を持って構える。</summary>
        private const string DemonFramePrefix = "UI_Anim";
        private const int DemonFrameCount = 7;
        /// <summary>1コマの秒数。スキル一覧が開くとき（AbilityUI）と同じ。</summary>
        private const float DemonFrameSeconds = 0.06f;

        /// <summary>絵の1ドットを画面の何単位で出すか。ベルルの横幅が約340になる。</summary>
        private const float DemonScale = 0.55f;
        /// <summary>ベルルの真ん中を置く所（画面の中心が原点）。</summary>
        private static readonly Vector2 DemonCenter = new Vector2(0f, 150f);

        private static readonly Vector2 FramePixels = new Vector2(1010f, 1570f);
        private static readonly Vector2 PoweredPixels = new Vector2(660f, 510f);
        /// <summary>コマの中の、ベルルの真ん中（左上が原点のドット）。</summary>
        private static readonly Vector2 FrameFigureCenter = new Vector2(540f, 275f);
        /// <summary>カットインの絵の中の、ベルルの真ん中。</summary>
        private static readonly Vector2 PoweredFigureCenter = new Vector2(340f, 285f);
        /// <summary>カットインの絵の中の、板の空いている所（頭の下、両手のあいだ）の真ん中。</summary>
        private static readonly Vector2 PoweredSignCenter = new Vector2(340f, 340f);

        /// <summary>ベルルの目と輪の色に合わせた黄色。板の「+1翻」と、降りてくる光に使う。</summary>
        private static readonly Color DemonYellow = new Color(1f, 0.92f, 0.2f, 1f);

        private const float PowerHoldSeconds = 0.35f;
        private const float GrantSeconds = 0.28f;

        /// <summary>ベルルが出るとき、文字をこれだけ下へずらす（ベルルの下に役名と翻数を置くため）。</summary>
        private const float TextShiftWithDemon = -98f;

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
        /// <param name="demonPowered">
        /// 目が光っているベルルの絵（自分のスキルのカットインの絵。<c>PhaseTransitionUI.PlayerCutinSprite</c>）。
        /// 無ければ目は光らず、構えた絵のままで進む
        /// </param>
        public IEnumerator Play(string yakuName, YakuListUI board = null, Sprite demonPowered = null)
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

            // ---- ベルルの絵。暗転より手前、文字より奥 ----
            Sprite[] frames = LoadDemonFrames();
            Image demon = null;
            Image powered = null;
            if (frames != null)
            {
                // コマは縦に長い1枚（下は開いた板のための空き）。ベルルの真ん中が DemonCenter に来るように置く
                Vector2 frameCenter = DemonCenter - FromPixelCenter(FrameFigureCenter, FramePixels);
                demon = _stage.AddSprite("Demon", frames[0], frameCenter, FramePixels * DemonScale);
                demon.enabled = false;

                if (demonPowered != null)
                {
                    Vector2 poweredCenter = DemonCenter - FromPixelCenter(PoweredFigureCenter, PoweredPixels);
                    powered = _stage.AddSprite("DemonPowered", demonPowered, poweredCenter, PoweredPixels * DemonScale);
                    powered.enabled = false;
                }
            }
            bool hasDemon = demon != null;
            Vector2 signPos = DemonCenter - FromPixelCenter(PoweredFigureCenter, PoweredPixels)
                              + FromPixelCenter(PoweredSignCenter, PoweredPixels);

            var sparks = _stage.AddShapes("Sparks");

            float shift = hasDemon ? TextShiftWithDemon : 0f;
            float nameMax = hasDemon ? 84f : 96f;
            float nameSize = Mathf.Clamp(560f / Mathf.Max(shown.Length, 1), 52f, nameMax);
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
            // ベルルの板に出る「+1翻」。これが降りてきて、新しい翻数になる
            var plusText = _stage.AddText("Plus", "+1翻", 38f, DemonYellow);
            plusText.fontStyle = FontStyles.Bold;
            plusText.alpha = 0f;
            plusText.rectTransform.anchoredPosition = signPos;

            Vector2 namePos = new Vector2(0f, 52f + shift);
            Vector2 oldPos = new Vector2(-118f, -44f + shift);
            Vector2 arrowPos = new Vector2(-28f, -44f + shift);
            Vector2 newPos = hasNumbers ? new Vector2(86f, -44f + shift) : new Vector2(0f, -44f + shift);
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

            yield return new WaitForSeconds(hasDemon ? 0.15f : 0.25f);

            if (hasDemon)
            {
                // ---- ② 呼ぶ。黄色い輪が描かれて、ベルルが出てくる（スキル一覧が開くときと同じコマ）----
                if (audio != null) audio.PlaySynthSound(SynthWaveType.Sine, 880f, 1320f, 0.25f, 0.35f);
                demon.enabled = true;
                for (int i = 0; i < frames.Length; i++)
                {
                    demon.sprite = frames[i];
                    yield return new WaitForSeconds(DemonFrameSeconds);
                }
                yield return new WaitForSeconds(0.12f);

                // ---- ③ 借りる。目が光り、板に「+1翻」が出る ----
                Image figure = demon;
                if (powered != null)
                {
                    demon.enabled = false;
                    powered.enabled = true;
                    figure = powered;
                }
                if (audio != null) audio.PlaySynthSoundDual(SynthWaveType.Sine, SynthWaveType.Triangle, 330f, 990f, 0.3f, 0.6f);
                ScreenQuake.Play(6f, 0.15f);
                plusText.alpha = 1f;
                for (float t = 0f; t < 0.3f; t += Time.deltaTime)
                {
                    // ベルルと板の文字が一度ふくらむ。目の光が輪になって広がる
                    float pop = t < PopSeconds ? Mathf.PingPong(t * (1f / (PopSeconds / 2f)), 1f) : 0f;
                    if (figure == powered) figure.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, pop);
                    plusText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.3f, pop);

                    float q = Mathf.Clamp01(t / 0.3f);
                    Color ring = DemonYellow;
                    ring.a = (1f - q) * 0.9f;
                    sparks.Begin();
                    sparks.Ring(DemonCenter, 70f + q * 150f, 4f, ring);
                    sparks.End();
                    yield return null;
                }
                figure.rectTransform.localScale = Vector3.one;
                plusText.rectTransform.localScale = Vector3.one;
                sparks.Begin();
                sparks.End();

                yield return new WaitForSeconds(PowerHoldSeconds);

                // ---- ④ 上がる。「+1翻」が板から降りてきて、新しい翻数になる ----
                if (audio != null) audio.PlaySynthSound(SynthWaveType.Sine, 520f, 780f, 0.28f, 0.45f);
                float grow = newText.fontSize / plusText.fontSize;
                for (float t = 0f; t < GrantSeconds; t += Time.deltaTime)
                {
                    float p = Mathf.Clamp01(t / GrantSeconds);
                    float eased = p * p;                       // 出だしはゆっくり、落ちるように速く
                    plusText.rectTransform.anchoredPosition = Vector2.Lerp(signPos, newPos, eased);
                    plusText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, grow, eased);
                    arrowText.alpha = p;
                    oldText.alpha = Mathf.Lerp(1f, OldHanFaded, p);   // 古い値は一歩下がる

                    // 後ろに光の粒を引く
                    sparks.Begin();
                    for (int k = 1; k <= 6; k++)
                    {
                        float back = eased - k * 0.07f;
                        if (back <= 0f) continue;
                        Vector2 at = Vector2.Lerp(signPos, newPos, back);
                        at.x += (k % 2 == 0 ? 1f : -1f) * (6f + k * 3f);
                        Color c = DemonYellow;
                        c.a = 0.8f - k * 0.11f;
                        sparks.BoxCentered(SkillEffectStage.Snap(at), 6f, 6f, c);
                    }
                    sparks.End();
                    yield return null;
                }
                plusText.alpha = 0f;
                newText.rectTransform.anchoredPosition = newPos;
                newText.alpha = 1f;
                arrowText.alpha = 1f;
                oldText.alpha = OldHanFaded;

                // 着いた所から輪が広がり、新しい翻数が一度ふくらむ
                for (float t = 0f; t < 0.25f; t += Time.deltaTime)
                {
                    float pop = t < PopSeconds ? Mathf.PingPong(t * (1f / (PopSeconds / 2f)), 1f) : 0f;
                    newText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.3f, pop);

                    float q = Mathf.Clamp01(t / 0.25f);
                    Color ring = DemonYellow;
                    ring.a = (1f - q) * 0.9f;
                    sparks.Begin();
                    sparks.Ring(newPos, 30f + q * 60f, 4f, ring);
                    sparks.End();
                    yield return null;
                }
                newText.rectTransform.localScale = Vector3.one;
                sparks.Begin();
                sparks.End();
            }
            else
            {
                // ---- ベルルの絵が無いとき。新しい翻数が上から降りてくる（牌交換の IN と同じ動き）----
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
            }

            yield return new WaitForSeconds(HoldBeforeFlash);

            // ベルルは輪へ帰る。文字が飛んでいくのと同時に進める
            if (hasDemon) StartCoroutine(DemonLeaves(demon, powered, frames));

            // ---- ⑤ 役名と新しい翻数が、右上のボードの枠へ飛んでいく ----
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

                // ---- ⑥ 収まった。枠を出して、白く光る ----
                board.ReleaseLocalBoostChip();
                _heldBoard = null;
                // 強化が枠より多いと、飛んでいった先は「+2」（あと2件）の枠になる。
                // しばらくだけ、そこに強めた役を名前で出す（明滅が終わったあとも少し残して読ませる）
                board.SpotlightLocalBoost(yakuName, GlowSeconds + SpotlightLinger);
                if (audio != null) audio.PlaySynthSound(SynthWaveType.Sine, 780f, 1170f, 0.14f, 0.5f);
                ReleaseTrance(dim, demon, powered);

                _poppedChip = chip;
                _chipScale = chip.localScale;
                var glow = _stage.AddShapes("ChipGlow");
                for (float t = 0f; t < GlowSeconds; t += Time.deltaTime)
                {
                    if (t >= 0.12f) Clear(dim, demon, powered);

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
                Clear(dim, demon, powered);

                Dispose();
                yield break;
            }

            // ---- ボードが無いとき。ベルルが帰るのを待ってから、その場で白く光って暗転が消える ----
            if (hasDemon) yield return new WaitForSeconds(FlySeconds);
            ReleaseTrance(dim, demon, powered);
            yield return new WaitForSeconds(0.12f);
            Clear(dim, demon, powered);

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

        /// <summary>ベルルが輪へ帰る。出てきたときのコマを逆に送る。</summary>
        private IEnumerator DemonLeaves(Image demon, Image powered, Sprite[] frames)
        {
            if (demon == null || frames == null) yield break;

            if (powered != null) powered.enabled = false;
            demon.enabled = true;
            float step = FlySeconds / frames.Length;       // 文字が枠に着くのと同時に消える
            for (int i = frames.Length - 1; i >= 0; i--)
            {
                if (demon == null) yield break;
                demon.sprite = frames[i];
                yield return new WaitForSeconds(step);
            }
            if (demon != null) demon.enabled = false;
        }

        /// <summary>白く光って、沈んでいた音を抜く。心音と BGM の戻りは待たない。光が乗りきったら暗転とベルルを消す。</summary>
        private void ReleaseTrance(Image dim, Image demon, Image powered)
        {
            if (_trance != null)
            {
                var trance = _trance;
                _trance = null;            // ここから先は音の部品が自分で鳴らしきる
                trance.ReleaseDetached(() => Clear(dim, demon, powered));
            }
            else
            {
                ScreenFlash.Play();
            }
        }

        private static void Clear(Image dim, Image demon, Image powered)
        {
            if (dim != null) dim.color = Color.clear;
            if (demon != null) demon.enabled = false;
            if (powered != null) powered.enabled = false;
        }

        /// <summary>ベルルが出てくるコマを読む。1枚でも読めなければ null（ベルル無しで進める）。</summary>
        private static Sprite[] LoadDemonFrames()
        {
            var frames = new Sprite[DemonFrameCount];
            for (int i = 0; i < DemonFrameCount; i++)
            {
                frames[i] = Resources.Load<Sprite>(DemonFramePrefix + (i + 1));
                if (frames[i] == null)
                {
                    Debug.LogWarning("[BoostHandSkillEffect] " + DemonFramePrefix + (i + 1) + " が読めませんでした。ベルル無しで進めます");
                    return null;
                }
            }
            return frames;
        }

        /// <summary>
        /// 絵の中の1点（左上が原点のドット）を、絵の真ん中からのずれ（画面の単位。上が正）に直す。
        /// </summary>
        private static Vector2 FromPixelCenter(Vector2 pixel, Vector2 size)
        {
            return new Vector2(pixel.x - size.x * 0.5f, size.y * 0.5f - pixel.y) * DemonScale;
        }
    }
}
