using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.EngineData;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// フェイズごとのBGM差し替え（2026-09-11）。
    ///
    /// **ドラム層（AudioManager.Drums.cs）の役割をこちらが引き継ぐ。**
    /// あちらは「曲が2本しか無いのでドラムだけ差し替える」ための仕組みだった。
    /// いまは曲がフェイズぶん揃っているので、曲そのものを差し替える。
    /// ドラムは各曲に最初から入っているため、`drumSource` を同時に鳴らすと打楽器が二重になる。
    /// `UsePhaseBgm` が true のあいだ `SetPhaseDrum` は何もしない（Drums.cs 側で見ている）。
    ///
    /// **差し替えても曲は頭から鳴り直さない。**
    /// `Resources/Bgm/` の曲はすべて 135.10 BPM・8小節・14.212 秒でそろえて作ってあり、
    /// 根音の進行も同じ系列にしてある。だから差し替えるとき再生位置（timeSamples）を
    /// そのまま引き継げば、**小節の途中から次の曲の同じ位置へ移る**。
    /// 聞こえ方は「曲が変わった」ではなく「場の温度が変わった」になる。
    /// 曲を足すときも、この 3 つ（テンポ・長さ・調）を必ずそろえること。
    /// </summary>
    public partial class AudioManager
    {
        /// <summary>フェイズBGMを使うか。false にすると従来のドラム層方式に戻る。</summary>
        [Header("Phase BGM")]
        [Tooltip("フェイズごとに曲を差し替える。切るとドラム層方式（旧）に戻る")]
        public bool UsePhaseBgm = true;

        /// <summary>
        /// フェイズごとの曲名。`Resources/Bgm/` から読む。
        /// **空文字は「場のBGM（濃度で選ぶ）を使う」という意味**で、鳴らさないという意味ではない。
        /// </summary>
        private static readonly Dictionary<RoundStatus, string> PhaseBgmNames = new Dictionary<RoundStatus, string>
        {
            { RoundStatus.None,          "bgm_field_1" },
            { RoundStatus.Dealing,       "bgm_prepare" },
            { RoundStatus.HandSelection, "bgm_prepare" },
            { RoundStatus.Betting,       "bgm_betting" },
            // 先行・後攻が決まる一瞬。根音が一切動かない曲を当てて、止まった感じを出す
            { RoundStatus.TurnDecision,  "bgm_tension" },
            { RoundStatus.Discard,       "" },
            { RoundStatus.Liquidation,   "bgm_ron" },
            { RoundStatus.Agari,         "bgm_ron" },
            { RoundStatus.Ron,           "bgm_ron" },
            { RoundStatus.Draw,          "bgm_draw" },
            { RoundStatus.Result,        "bgm_result" },
        };

        /// <summary>
        /// 打牌フェイズの濃さ。1=序盤の余裕 … 4=崖っぷち。
        /// 4本とも根音の並びが同じなので、途中で上げ下げしても和音は跳ねない。
        /// </summary>
        private int bgmIntensity = 2;

        private const int MinIntensity = 1;
        private const int MaxIntensity = 4;

        /// <summary>
        /// 曲ごとのテンポと拍子（2026-09-11）。
        ///
        /// **かつては全曲 135.10 BPM の決め打ちだった。** 曲がどれも同じに聞こえるという
        /// 指摘を受けてテンポ・調・旋法・音色・リズムを曲ごとに変えたので、
        /// 拍の位置は曲ごとに引かないと合わない。
        /// 曲を差し替えたら、ここも必ず更新すること。
        /// </summary>
        private struct Tempo
        {
            public float Bpm;
            public int BeatsPerBar;
            public Tempo(float bpm, int beats) { Bpm = bpm; BeatsPerBar = beats; }
        }

        private static readonly Dictionary<string, Tempo> Tempos = new Dictionary<string, Tempo>
        {
            { "bgm_title",        new Tempo(76f,  4) },
            { "bgm_tutorial",     new Tempo(104f, 4) },
            { "bgm_prepare",      new Tempo(92f,  4) },
            { "bgm_draw",         new Tempo(72f,  4) },
            { "bgm_lose",         new Tempo(60f,  4) },
            { "bgm_field_1",      new Tempo(118f, 4) },
            { "bgm_field_2",      new Tempo(118f, 4) },
            { "bgm_field_3",      new Tempo(118f, 4) },
            { "bgm_field_4",      new Tempo(118f, 4) },
            { "bgm_betting",      new Tempo(128f, 4) },
            { "bgm_tension",      new Tempo(144f, 4) },
            // **compose.py で作り直した（2026-09-16）。** 実測ではなく設計値で
            // 135.00 ちょうど・1拍目がサンプル0。以前は実測の 132 だった。
            { "bgm_discard",      new Tempo(135f, 4) },
            { "bgm_discard_hot",  new Tempo(160f, 4) },
            { "bgm_ron",          new Tempo(88f,  4) },
            { "bgm_result",       new Tempo(96f,  3) },   // ワルツ
            { "bgm_win",          new Tempo(132f, 4) },

            // **チュートリアルの曲。** ここに載っていないと拍の時計が動かず、
            // 揺れ（`FloatingAnimator`）も音に合わせられない。
            // `tut_lesson` は `km-docs/tools/compose.py` で作っていて、
            // **135.00 ちょうど・1拍目がサンプル0**。実測ではなく設計値。
            { "tut_lesson",       new Tempo(135f, 4) },

            // 層のステム。**ここに無いと拍が引けず、音ハメが効かない。**
            // 4本とも同じ編曲を分けたものなので当然おなじテンポ。
            { "field_base",       new Tempo(118f, 4) },
            { "field_melody",     new Tempo(118f, 4) },
            { "field_drums",      new Tempo(118f, 4) },
            { "field_sparkle",    new Tempo(118f, 4) },
        };

        /// <summary>拍の情報が無い曲のときの既定。すぐ切り替える方に倒す。</summary>
        private static readonly Tempo UnknownTempo = new Tempo(0f, 4);

        /// <summary>差し替えの前後で音量を落とす時間。クリックノイズを避けるためだけの短さ。</summary>
        private const float SwapDipSeconds = 0.05f;

        private readonly Dictionary<string, AudioClip> phaseBgmClips = new Dictionary<string, AudioClip>();
        private string currentPhaseBgmName;
        private RoundStatus currentBgmPhase = RoundStatus.None;
        private Coroutine bgmSwapCoroutine;

        /// <summary>差し替えの音量ディップ中か。ApplyVolumes に上書きさせないために見せている。</summary>
        public bool IsSwappingPhaseBgm { get { return bgmSwapCoroutine != null; } }

        private AudioClip GetPhaseBgmClip(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            AudioClip cached;
            if (phaseBgmClips.TryGetValue(name, out cached)) return cached;

            var clip = Resources.Load<AudioClip>("Bgm/" + name);
            phaseBgmClips[name] = clip;
            if (clip == null) Debug.LogWarning("[AudioManager] BGMが見つかりません: Resources/Bgm/" + name);
            return clip;
        }

        /// <summary>いまのフェイズと濃度から、鳴らすべき曲名を決める。</summary>
        private string ResolveBgmName(RoundStatus status)
        {
            string name;
            if (!PhaseBgmNames.TryGetValue(status, out name)) name = "";
            if (!string.IsNullOrEmpty(name)) return name;

            // 空文字は「場のBGM」。濃度で 4 段から選ぶ
            int tier = Mathf.Clamp(bgmIntensity, MinIntensity, MaxIntensity);
            return "bgm_field_" + tier;
        }

        /// <summary>
        /// フェイズに合わせてBGMを差し替える。**次の小節頭まで待つ。**
        /// 拍の途中で入れ替えるとリズムの位置がずれて気持ち悪くなる。
        /// </summary>
        public void SetPhaseBgm(RoundStatus status)
        {
            if (!CanPlay) return;
            if (!UsePhaseBgm) return;

            currentBgmPhase = status;
            ApplyPhaseBgm();
        }

        /// <summary>
        /// 打牌フェイズの濃さを変える。局が進むほど、体力が減るほど上げる想定。
        /// **フェイズをまたがなくても効く**ので、対局中に呼んでよい。
        /// </summary>
        public void SetBgmIntensity(int tier)
        {
            int clamped = Mathf.Clamp(tier, MinIntensity, MaxIntensity);
            if (clamped == bgmIntensity) return;

            bgmIntensity = clamped;

            // 層で鳴らしているときは**曲を差し替えない。** 層の音量だけを動かす。
            // 差し替えは、どれだけ丁寧に繋いでも「曲が変わった」と気づかれる。
            if (AreLayersRunning) { ApplyLayerMix(clamped, instant: false); return; }

            if (UsePhaseBgm) ApplyPhaseBgm();
        }

        public int BgmIntensity { get { return bgmIntensity; } }

        /// <summary>
        /// いまのフェイズと濃度を読み直して曲を当てる。
        /// **引数を取らないのは Drums.cs と同じ理由**で、保留から遅れて流れてきたときに
        /// 古い行き先へ切り替わらないようにするため。
        /// </summary>
        private void ApplyPhaseBgm()
        {
            string want = ResolveBgmName(currentBgmPhase);

            // **場のBGMは層で鳴らす。** 濃さが変わっても曲は変わらないので、
            // ここでは「場に入ったか / 場から出たか」だけを見る。
            if (UseBgmLayers)
            {
                bool isField = want != null && want.StartsWith("bgm_field_");
                if (isField)
                {
                    if (!AreLayersRunning) StartLayeredBgm(bgmIntensity);
                    else ApplyLayerMix(bgmIntensity, instant: false);
                    return;
                }
                if (AreLayersRunning) StopLayeredBgm();   // 場を離れたら層を畳む
            }

            if (want == currentPhaseBgmName) return;

            var clip = GetPhaseBgmClip(want);
            if (clip == null) return;   // 見つからないときは今の曲を鳴らし続ける

            currentPhaseBgmName = want;

            if (bgmSwapCoroutine != null) StopCoroutine(bgmSwapCoroutine);
            bgmSwapCoroutine = StartCoroutine(SwapBgmAtNextBar(clip));
        }

        private IEnumerator SwapBgmAtNextBar(AudioClip clip)
        {
            if (bgmSource == null) yield break;

            // まだ何も鳴っていないなら待たずに始める
            if (bgmSource.clip == null || !bgmSource.isPlaying)
            {
                bgmSource.clip = clip;
                bgmSource.loop = true;
                bgmSource.timeSamples = 0;
                bgmSource.Play();
                bgmSwapCoroutine = null;
                yield break;
            }

            float wait = SecondsToNextPhaseBgmBar();
            if (wait > SwapDipSeconds) yield return new WaitForSeconds(wait - SwapDipSeconds);

            float full = bgmVolume * masterVolume;

            // 落とす
            float t = 0f;
            while (t < SwapDipSeconds)
            {
                t += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(full, 0f, t / SwapDipSeconds);
                yield return null;
            }

            // **再生位置を引き継げるのは、テンポも長さも同じ曲どうしのときだけ。**
            //
            // 場のBGM(bgm_field_1〜4)は同テンポ・同長・同じ和音進行で作ってあるので、
            // 位置を引き継ぐと「曲が変わった」ではなく「場の温度が変わった」に聞こえる。
            // 一方、テンポの違う曲へ位置ごと飛ぶと拍の途中に着地して破綻する。
            // そういう相手には頭から入れる。
            var oldClip = bgmSource.clip;
            bool sameFamily = oldClip != null
                              && oldClip.samples == clip.samples
                              && SameTempo(oldClip.name, clip.name);
            int pos = bgmSource.timeSamples;

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.timeSamples = (sameFamily && clip.samples > 0) ? (pos % clip.samples) : 0;
            bgmSource.Play();

            // 戻す
            t = 0f;
            while (t < SwapDipSeconds)
            {
                t += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(0f, full, t / SwapDipSeconds);
                yield return null;
            }
            bgmSource.volume = full;

            bgmSwapCoroutine = null;
        }

        private static Tempo TempoOf(string clipName)
        {
            Tempo t;
            return Tempos.TryGetValue(clipName, out t) ? t : UnknownTempo;
        }

        private static bool SameTempo(string a, string b)
        {
            Tempo ta = TempoOf(a), tb = TempoOf(b);
            if (ta.Bpm <= 0f || tb.Bpm <= 0f) return false;
            return Mathf.Approximately(ta.Bpm, tb.Bpm) && ta.BeatsPerBar == tb.BeatsPerBar;
        }

        /// <summary>
        /// 次の小節頭までの秒数。曲は 1 拍目がサンプル 0 から始まるように作ってあるので、
        /// Drums.cs の `SecondsToNextBar` と違って先頭のずれを引く必要がない。
        ///
        /// **テンポは今鳴っている曲から引く。** 決め打ちにすると、
        /// テンポの違う曲を鳴らしているときに小節頭を取り違える。
        /// </summary>
        private float SecondsToNextPhaseBgmBar()
        {
            if (bgmSource == null || bgmSource.clip == null || !bgmSource.isPlaying) return 0f;

            Tempo t = TempoOf(bgmSource.clip.name);
            if (t.Bpm <= 0f) return 0f;   // 知らない曲は待たずに切り替える

            float bar = 60f / t.Bpm * t.BeatsPerBar;
            float intoBar = bgmSource.time % bar;
            return bar - intoBar;
        }

        /// <summary>
        /// 対局のBGMを開始する。`PlayBGM(battleBgm)` の置き換え。
        /// フェイズは呼び出し側が続けて `SetPhaseBgm` で伝えてくる。
        /// </summary>
        public void StartPhaseBgm(RoundStatus status = RoundStatus.None)
        {
            if (!CanPlay) return;
            if (!UsePhaseBgm)
            {
                PlayBGM(battleBgm);
                return;
            }

            currentBgmPhase = status;
            currentPhaseBgmName = null;   // 同じ曲でも鳴らし直せるようにする
            ApplyPhaseBgm();
        }

        /// <summary>タイトル・メニュー用。対局のBGMとは別系統。</summary>
        public void PlayTitleBgm()
        {
            PlayMenuBgm("bgm_title");
        }

        /// <summary>
        /// 部屋の待機画面の曲（2026-09-19 のユーザー指示「このシーンではチルい曲を」）。
        /// 追加曲の lo-fi。**ループ用の曲ではないので、終わりの約2秒でフェードしてから頭に戻る。**
        /// 継ぎ目を消したくなったら、ループ版を作って差し替えること。
        /// </summary>
        public const string RoomBgmName = "bgm_ex_lofi";

        public void PlayRoomBgm()
        {
            PlayMenuBgm(RoomBgmName);
        }

        /// <summary>タイトル・部屋など、対局の外で流す曲。すでに同じ曲が流れていれば頭に戻さない。</summary>
        private void PlayMenuBgm(string name)
        {
            if (!CanPlay) return;
            if (!UsePhaseBgm)
            {
                PlayBGM(defaultBgm);
                return;
            }

            var clip = GetPhaseBgmClip(name);
            if (clip == null) { PlayBGM(defaultBgm); return; }
            if (bgmSource.isPlaying && bgmSource.clip == clip) return;

            currentPhaseBgmName = name;
            currentBgmPhase = RoundStatus.None;
            if (bgmSwapCoroutine != null) { StopCoroutine(bgmSwapCoroutine); bgmSwapCoroutine = null; }

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.timeSamples = 0;
            bgmSource.volume = bgmVolume * masterVolume;
            bgmSource.Play();
        }

        // --- スティンガー（一発物） ---
        //
        // `Resources/Stingers/` に置いてある。BGMと同じ 135.10 BPM・同じ調で作ってあるので、
        // 鳴っているBGMの上に重ねても濁らない。
        //
        // **SE と同じ AudioSource に載せる。** 音量スライダーは SE 側に従う。
        // BGM 側に載せるとフェイズBGMの差し替えで途中から消える。

        private readonly Dictionary<string, AudioClip> stingerClips = new Dictionary<string, AudioClip>();

        /// <summary>スティンガーの音量。SE に対する相対値。</summary>
        [Tooltip("暗転などで鳴る一発物の音量。SEに対する相対値")]
        [Range(0f, 2f)] public float stingerVolume = 1.0f;

        public void PlayStinger(string name)
        {
            if (!CanPlay) return;
            if (string.IsNullOrEmpty(name) || seSource == null) return;

            AudioClip clip;
            if (!stingerClips.TryGetValue(name, out clip))
            {
                clip = Resources.Load<AudioClip>("Stingers/" + name);
                stingerClips[name] = clip;
                if (clip == null) Debug.LogWarning("[AudioManager] スティンガーが見つかりません: Resources/Stingers/" + name);
            }
            if (clip == null) return;

            // PlayOneShot なので重ねて鳴らせる。暗転の落ちと着地が近接しても切れない。
            seSource.PlayOneShot(clip, seVolume * masterVolume * stingerVolume);
        }

        /// <summary>結果画面のBGMを勝敗で分ける。`SetPhaseBgm(Result)` より後に呼ぶこと。</summary>
        public void SetResultBgm(bool isLocalWin)
        {
            if (!CanPlay) return;
            if (!UsePhaseBgm) return;

            var clip = GetPhaseBgmClip(isLocalWin ? "bgm_win" : "bgm_lose");
            if (clip == null) return;

            currentPhaseBgmName = isLocalWin ? "bgm_win" : "bgm_lose";
            if (bgmSwapCoroutine != null) StopCoroutine(bgmSwapCoroutine);
            bgmSwapCoroutine = StartCoroutine(SwapBgmAtNextBar(clip));
        }

        // --- チュートリアル用の経路（2026-09-11） ---
        //
        // **フェイズではなく台詞で音が動く。** 対局の `SetPhaseBgm` は
        //   ・次の小節頭まで待つ
        //   ・行き先を `PhaseBgmNames` から引く
        // という作りで、どちらもチュートリアルには合わない。
        // 台本の「ロン。」は小節を待っていては遅い（裏切りの間が死ぬ）ので、
        // 待たずに切る経路を別に用意する。
        //
        // なおチュートリアル中は `GameUIManager.ApplyBgmFilterForCurrentPhase` が
        // `IsTutorialMode` で早期 return するため、`SetPhaseBgm` は呼ばれない。
        // つまりこの経路と喧嘩しない。

        /// <summary>
        /// 台本の指示でBGMを差し替える。**小節頭を待たず、頭から鳴らす。**
        /// 曲ごとにテンポも長さも違うので、再生位置の引き継ぎはしない。
        /// </summary>
        public void PlayTutorialBgm(string name)
        {
            if (!CanPlay) return;
            if (bgmSource == null || string.IsNullOrEmpty(name)) return;

            var clip = GetPhaseBgmClip(name);
            if (clip == null) return;

            if (bgmSwapCoroutine != null) { StopCoroutine(bgmSwapCoroutine); bgmSwapCoroutine = null; }
            currentPhaseBgmName = name;

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.timeSamples = 0;
            bgmSource.volume = bgmVolume * masterVolume;
            bgmSource.Play();
        }

        /// <summary>
        /// **1フレームで断つ。** フェードもしないし小節も待たない。
        ///
        /// 第3局の「ロン。」で、温かい曲を途中で断ち切るために要る。
        /// フェードアウトさせると「終わった」に聞こえてしまい、
        /// 裏切りの不意打ちにならない。
        /// </summary>
        public void CutBgmImmediately()
        {
            if (bgmSwapCoroutine != null) { StopCoroutine(bgmSwapCoroutine); bgmSwapCoroutine = null; }
            if (bgmSource != null) bgmSource.Stop();
            StopLayeredBgm();             // 層で鳴っていたらそれも断つ
            currentPhaseBgmName = null;   // 同じ曲を鳴らし直せるようにしておく
        }

        /// <summary>止めたあと、次に同じフェイズで呼ばれても鳴らし直せるようにしておく。</summary>
        private void ResetPhaseBgmState()
        {
            currentPhaseBgmName = null;
            if (bgmSwapCoroutine != null) { StopCoroutine(bgmSwapCoroutine); bgmSwapCoroutine = null; }
        }
    }
}
