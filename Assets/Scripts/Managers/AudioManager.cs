using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource seSource;
        [SerializeField] private AudioSource voiceSource;
        private AudioSource discardSeSource; // 打牌専用のAudioSource

        private AudioLowPassFilter bgmLowPassFilter; // BGMのこもり（重低音）表現用フィルター
        private Coroutine filterFadeCoroutine;

        [Header("Discard Pitch Settings")]
        [Tooltip("打牌1回あたり何半音ピッチを上げるか。1.0=半音ずつ、0.5=四分音ずつ（ゆっくり）")]
        [SerializeField, Range(0.1f, 2f)] private float discardPitchStepSemitones = 0.5f;

        private float currentDiscardPitch = 1.0f;
        private float lastDiscardTime = 0f;
        private const float PITCH_RESET_TIME = 2.5f; // 2.5秒間打牌がなければピッチリセット
        private const float MAX_PITCH = 2.0f; // 1オクターブ上まで

        [Header("Volume Settings")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 1f;
        [Range(0f, 1f)] public float seVolume = 1f;
        [Range(0f, 1f)] public float voiceVolume = 1f;

        [Header("Audio Clips - Setup in Inspector")]
        [Tooltip("デフォルトのBGM（タイトルや日常会話など）")]
        public AudioClip defaultBgm;
        [Tooltip("対局中のBGM")]
        public AudioClip battleBgm;
        [Tooltip("打牌した時のSE")]
        public AudioClip discardSE;
        [Tooltip("牌を選択・移動した時のSE（旧）")]
        public AudioClip selectTileSE;
        [Tooltip("ロンした時のボイス（声）")]
        public AudioClip ronVoice;

        [Header("ASMR Sound Clips")]
        [Tooltip("牌やボタンにカーソルを乗せた時の音（スッ…）")]
        public AudioClip hoverSE;
        [Tooltip("牌を選択（クリック）した時の音（カチャッ）")]
        public AudioClip pickTileSE;
        [Tooltip("山から牌をツモってきた時の音（スチャッ）")]
        public AudioClip drawTileSE;
        [Tooltip("UIやダイアログがポップアップした時の音（ポポンッ）")]
        public AudioClip uiPopupSE;
        
        [Header("Paper UI Sound Clips")]
        [Tooltip("紙のUIがスライドしてくる/戻る時の音")]
        public AudioClip paperSlideSE;

        // 今後の「音ハメ」拡張用プロパティ
        public float CurrentBgmTime => bgmSource != null ? bgmSource.time : 0f;
        public int CurrentBgmTimeSamples => bgmSource != null ? bgmSource.timeSamples : 0;
        public bool IsBgmPlaying => bgmSource != null && bgmSource.isPlaying;

        [Header("シーン遷移時のBGM")]
        [Tooltip("このシーンへ移ったら BGM を自動で止める。タイトルへ戻ったとき対局のBGMが鳴り続けるのを防ぐ")]
        [SerializeField] private string[] stopBgmOnScenes = { "タイトルシーン" };

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSources();
                // AudioManager は DontDestroyOnLoad で生き残るため、
                // シーンを変えても BGM は鳴り続ける。タイトルへ戻る導線は
                // TutorialManager / InGameMenuUI / OptionUI / TitleUIManager と複数あるので、
                // 各所で止めると必ず漏れる。ここで一括して面倒を見る。
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (stopBgmOnScenes == null) return;
            foreach (var n in stopBgmOnScenes)
            {
                if (!string.IsNullOrEmpty(n) && scene.name == n)
                {
                    StopBGM();
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
                // 破棄済みオブジェクトを指したままにしない
                Instance = null;
            }
        }

        private void Start()
        {
            // ゲーム開始時にBGMが設定されていれば再生する
            if (defaultBgm != null && !IsBgmPlaying)
            {
                PlayBGM(defaultBgm);
            }
        }

        private AudioSynth synth; // 追加

        public void ApplyVolumes()
        {
            if (bgmSource != null) bgmSource.volume = bgmVolume * masterVolume;
            if (seSource != null) seSource.volume = seVolume * masterVolume;
            if (voiceSource != null) voiceSource.volume = voiceVolume * masterVolume;
            if (discardSeSource != null) discardSeSource.volume = seVolume * masterVolume;
        }

        private void InitializeSources()
        {
            // BGM専用の子オブジェクトを作成（ローパスフィルターがSEやボイスに影響しないように分離）
            if (bgmSource == null || bgmSource.gameObject == this.gameObject)
            {
                GameObject bgmObj = new GameObject("BGM_Source");
                bgmObj.transform.SetParent(this.transform);
                AudioSource newBgmSource = bgmObj.AddComponent<AudioSource>();
                
                if (bgmSource != null)
                {
                    newBgmSource.volume = bgmSource.volume;
                    newBgmSource.loop = bgmSource.loop;
                    newBgmSource.playOnAwake = bgmSource.playOnAwake;
                    newBgmSource.clip = bgmSource.clip;
                    Destroy(bgmSource); // 元のAudioSourceを削除
                }
                bgmSource = newBgmSource;
            }

            if (seSource == null) seSource = gameObject.AddComponent<AudioSource>();
            if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();
            if (discardSeSource == null) discardSeSource = gameObject.AddComponent<AudioSource>();

            // BGM用のローパスフィルター設定
            if (bgmLowPassFilter == null)
            {
                bgmLowPassFilter = bgmSource.gameObject.GetComponent<AudioLowPassFilter>();
                if (bgmLowPassFilter == null) bgmLowPassFilter = bgmSource.gameObject.AddComponent<AudioLowPassFilter>();
            }
            // 初期状態はフィルターをかけておく（重低音のみ）
            bgmLowPassFilter.cutoffFrequency = 1000f; // こもった音の周波数
            bgmLowPassFilter.enabled = true;

            // AudioSynth用の専用GameObjectを作成
            if (synth == null)
            {
                GameObject synthObj = new GameObject("AudioSynthEngine");
                synthObj.transform.SetParent(this.transform);

                synth = synthObj.AddComponent<AudioSynth>();
            }


            bgmSource.loop = true;
            ApplyVolumes();

            PrewarmSynthSounds();
        }

        /// <summary>
        /// シンセSEを先に生成しておく。
        ///
        /// 生成は 48kHz で 1秒あたり 6〜7ms かかるメインスレッド処理なので、
        /// スキル発動やダメージ表示のタイミングで初めて作るとそこでフレームが飛ぶ。
        /// 鳴る場所が決まっている音はここで作っておき、実プレイ中はキャッシュだけを使う。
        ///
        /// ダメージ音・打撃音はHP割合で周波数が変わるので、代表的な数段だけ用意しておく
        /// （AudioSynth 側でキーを丸めているため、近い値はこれに吸収される）。
        /// </summary>
        private void PrewarmSynthSounds()
        {
            if (synth == null) return;

            // スキル発動音（PlaySkillSE と同じパラメータ）
            synth.Prewarm(SynthWaveType.Triangle, SynthWaveType.Square, true, 880f, 1174f, 0.18f);
            synth.Prewarm(SynthWaveType.Sine, SynthWaveType.Noise, true, 300f, 1800f, 0.55f);
            synth.Prewarm(SynthWaveType.Sawtooth, SynthWaveType.Square, true, 160f, 640f, 0.6f);
            synth.Prewarm(SynthWaveType.Sawtooth, SynthWaveType.Noise, true, 220f, 1760f, 0.28f);
            synth.Prewarm(SynthWaveType.Square, SynthWaveType.Noise, true, 1200f, 90f, 0.9f);

            // 賭け金確定・回復
            synth.Prewarm(SynthWaveType.Square, SynthWaveType.Sine, true, 523f, 1046f, 0.28f);
            synth.Prewarm(SynthWaveType.Sine, SynthWaveType.Triangle, true, 440f, 1320f, 0.35f);

            // ダメージ音・打撃音は残HP割合で周波数と長さが変わるので、代表的な5段を用意する。
            // PlayDamageSE / PlayHitSE と同じ式で作ること（式を変えたらここも合わせる）。
            for (int i = 0; i <= 4; i++)
            {
                float ratio = i * 0.25f;

                float dmgStart = Mathf.Lerp(260f, 600f, ratio);
                float dmgDuration = Mathf.Lerp(0.42f, 0.28f, ratio);
                synth.Prewarm(SynthWaveType.Sawtooth, SynthWaveType.Noise, true, dmgStart, dmgStart * 0.25f, dmgDuration);

                float hitStart = Mathf.Lerp(520f, 900f, ratio);
                synth.Prewarm(SynthWaveType.Square, SynthWaveType.Noise, true, hitStart, hitStart * 0.35f, 0.22f);
            }
        }

        // --- BGM Filter Control ---
        public void SetBgmFilter(bool isMuffled, float fadeDuration = 1.0f)
        {
            if (bgmLowPassFilter == null) return;
            if (filterFadeCoroutine != null) StopCoroutine(filterFadeCoroutine);
            
            float targetFreq = isMuffled ? 1000f : 22000f; // 22000fは事実上のフィルターオフ（全帯域）
            filterFadeCoroutine = StartCoroutine(FilterFadeRoutine(targetFreq, fadeDuration));
        }

        private System.Collections.IEnumerator FilterFadeRoutine(float targetFreq, float duration)
        {
            float startFreq = bgmLowPassFilter.cutoffFrequency;
            bgmLowPassFilter.enabled = true; // 確実にオンにする
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                bgmLowPassFilter.cutoffFrequency = Mathf.Lerp(startFreq, targetFreq, elapsed / duration);
                yield return null;
            }

            bgmLowPassFilter.cutoffFrequency = targetFreq;
            // 全帯域まで開いたらフィルター自体をオフにして負荷軽減＆音質劣化防止
            if (targetFreq >= 22000f)
            {
                bgmLowPassFilter.enabled = false;
            }
        }

        /// <summary>
        /// プロシージャル音声（シンセサイザー）を1つの波形で再生する
        /// </summary>
        public void PlaySynthSound(SynthWaveType type, float startFreq, float endFreq, float duration, float volume = 1.0f)
        {
            if (synth != null)
            {
                synth.Play(type, startFreq, endFreq, duration, volume);
            }
        }

        /// <summary>
        /// プロシージャル音声（シンセサイザー）を2つの波形でミックスして再生する
        /// </summary>
        public void PlaySynthSoundDual(SynthWaveType type1, SynthWaveType type2, float startFreq, float endFreq, float duration, float volume = 1.0f)
        {
            if (synth != null)
            {
                synth.PlayDual(type1, type2, true, startFreq, endFreq, duration, volume);
            }
        }

        // --- BGM Control ---
        // 将来的にBGMのBPM（テンポ）同期やビート検知処理をここに追加できます
        public void PlayBGM(AudioClip clip = null, bool restartIfSame = false)
        {
            if (bgmSource == null) return;
            if (clip == null) clip = defaultBgm;
            if (clip == null) return;

            if (bgmSource.clip == clip && bgmSource.isPlaying && !restartIfSame) return;

            bgmSource.clip = clip;
            bgmSource.Play();
        }

        public void StopBGM()
        {
            if (bgmSource != null) bgmSource.Stop();
        }

        // --- SE Control ---
        public void PlaySE(AudioClip clip)
        {
            if (clip != null && seSource != null)
            {
                seSource.PlayOneShot(clip, seVolume * masterVolume);
            }
        }

        // --- Discard SE Control ---
        public void PlayDiscardSE(AudioClip clip = null)
        {
            if (clip == null) clip = discardSE;
            if (clip == null || discardSeSource == null) return;

            // 一定時間経過していればピッチをリセット
            if (Time.time - lastDiscardTime > PITCH_RESET_TIME)
            {
                currentDiscardPitch = 1.0f;
            }
            else
            {
                // discardPitchStepSemitones 半音ぶん上げる（1オクターブ = 12半音）
                currentDiscardPitch *= Mathf.Pow(2f, discardPitchStepSemitones / 12f);
                if (currentDiscardPitch > MAX_PITCH)
                {
                    currentDiscardPitch = MAX_PITCH;
                }
            }

            discardSeSource.pitch = currentDiscardPitch;
            discardSeSource.volume = seVolume * masterVolume;
            discardSeSource.PlayOneShot(clip);

            lastDiscardTime = Time.time;
        }

        // --- ASMR Specific SE Control ---
        public void PlayHoverSE()
        {
            if (hoverSE != null) PlaySE(hoverSE);
        }

        public void PlayPickTileSE()
        {
            if (pickTileSE != null) PlaySE(pickTileSE);
            else if (selectTileSE != null) PlaySE(selectTileSE); // フォールバック
        }

        public void PlayDrawTileSE()
        {
            if (drawTileSE != null) PlaySE(drawTileSE);
        }

        public void PlayUIPopupSE()
        {
            if (uiPopupSE != null) PlaySE(uiPopupSE);
        }

        public void PlayPaperSlideSE()
        {
            if (paperSlideSE != null) PlaySE(paperSlideSE);
        }

        // --- ゲームプレイ用の合成SE ---
        // 「削り合い」の要である被弾・スキル発動・賭け金操作がいずれも無音だったため、
        // 音声アセットを増やさずに AudioSynth の合成音で埋めている。
        // 差し替えたくなったら各メソッドの中身を PlaySE(clip) に置き換えるだけで済む。

        /// <summary>
        /// 被弾音。残HPの割合が低いほど低く歪んだ音になり、追い詰められている感を出す。
        /// </summary>
        /// <param name="hpRatio">被弾後の残HP割合（0〜1）</param>
        public void PlayDamageSE(float hpRatio)
        {
            hpRatio = Mathf.Clamp01(hpRatio);

            // 瀕死ほど開始周波数を下げる（600Hz → 260Hz）
            float startFreq = Mathf.Lerp(260f, 600f, hpRatio);
            float endFreq = startFreq * 0.25f;
            float duration = Mathf.Lerp(0.42f, 0.28f, hpRatio); // 瀕死ほど長く尾を引く

            PlaySynthSoundDual(SynthWaveType.Sawtooth, SynthWaveType.Noise,
                startFreq, endFreq, duration, 1.0f);
        }

        /// <summary>
        /// 相手にダメージを与えた時の音。被弾音（PlayDamageSE）と違って手応えのある打撃音にする。
        /// とどめに近いほど低く重くなる。
        /// </summary>
        /// <param name="targetHpRatio">攻撃後の相手の残HP割合（0〜1）</param>
        public void PlayHitSE(float targetHpRatio)
        {
            targetHpRatio = Mathf.Clamp01(targetHpRatio);

            float startFreq = Mathf.Lerp(520f, 900f, targetHpRatio);
            PlaySynthSoundDual(SynthWaveType.Square, SynthWaveType.Noise,
                startFreq, startFreq * 0.35f, 0.22f, 1.0f);
        }

        /// <summary>回復（＝相手から奪った）音。上昇するきらびやかな音。</summary>
        public void PlayHealSE()
        {
            PlaySynthSoundDual(SynthWaveType.Sine, SynthWaveType.Triangle,
                440f, 1320f, 0.35f, 0.9f);
        }

        /// <summary>スキル発動音。スキルごとに音色を変えて聴き分けられるようにする。</summary>
        public void PlaySkillSE(string skillType)
        {
            switch (skillType)
            {
                case KillingMahjong.Common.SkillNames.Mulligan:
                    // 牌を入れ替える：短くカラッとした二段の音
                    PlaySynthSoundDual(SynthWaveType.Triangle, SynthWaveType.Square,
                        880f, 1174f, 0.18f, 0.8f);
                    break;

                case KillingMahjong.Common.SkillNames.Perspective:
                    // 透視：ノイズ混じりの上昇音で「覗かれた」感じを出す
                    PlaySynthSoundDual(SynthWaveType.Sine, SynthWaveType.Noise,
                        300f, 1800f, 0.55f, 0.85f);
                    break;

                case KillingMahjong.Common.SkillNames.BoostHand:
                    // 役強化：どっしり上昇する重い音
                    PlaySynthSoundDual(SynthWaveType.Sawtooth, SynthWaveType.Square,
                        160f, 640f, 0.6f, 1.0f);
                    break;

                case KillingMahjong.Common.SkillNames.Assault:
                    // 強襲：獲得を捨てて殴りにいく。短く鋭い、刺すような上昇音
                    PlaySynthSoundDual(SynthWaveType.Sawtooth, SynthWaveType.Noise,
                        220f, 1760f, 0.28f, 1.0f);
                    break;

                case KillingMahjong.Common.SkillNames.SpecialVictory:
                    // 特殊勝利：一番長く不穏な下降音
                    PlaySynthSoundDual(SynthWaveType.Square, SynthWaveType.Noise,
                        1200f, 90f, 0.9f, 1.0f);
                    break;

                default:
                    PlaySynthSound(SynthWaveType.Triangle, 660f, 990f, 0.2f, 0.8f);
                    break;
            }
        }

        /// <summary>
        /// 賭け金の増減音。賭け金が上限に近いほど高い音になり、額の大きさが耳でも分かるようにする。
        /// </summary>
        /// <param name="betRatio">現在の賭け金 ÷ 賭けられる上限（0〜1）</param>
        public void PlayBetTickSE(float betRatio)
        {
            betRatio = Mathf.Clamp01(betRatio);
            float freq = Mathf.Lerp(420f, 1100f, betRatio);
            PlaySynthSound(SynthWaveType.Triangle, freq, freq * 1.15f, 0.07f, 0.7f);
        }

        /// <summary>賭け金確定音。コインを置くような二段の決定音。</summary>
        public void PlayBetConfirmSE()
        {
            PlaySynthSoundDual(SynthWaveType.Square, SynthWaveType.Sine,
                523f, 1046f, 0.28f, 0.95f);
        }

        // --- Voice Control ---
        public void PlayVoice(AudioClip clip)
        {
            if (clip != null && voiceSource != null)
            {
                voiceSource.PlayOneShot(clip, voiceVolume * masterVolume);
            }
        }

        // --- 役名ボイス・ランクボイス ---
        private Dictionary<string, AudioClip> yakuVoiceClips;
        private Dictionary<string, AudioClip> rankVoiceClips;
        private AudioClip ronVoiceClip;

        /// <summary>
        /// 役名→ファイル名のマッピング辞書を初期化し、Audio/Voice/ からロードする
        /// </summary>
        private void InitializeYakuVoices()
        {
            // 役名 → ファイル名（拡張子なし）
            var yakuFileMap = new Dictionary<string, string>
            {
                // 1飜
                { "立直", "yaku_riichi" },
                { "断么九", "yaku_tanyao" },
                { "平和", "yaku_pinfu" },
                { "一盃口", "yaku_iipeikou" },
                { "東", "yaku_ton" },
                { "西", "yaku_shaa" },
                { "ドラ", "yaku_dora" },
                { "赤ドラ", "yaku_akadora" },
                { "一発", "yaku_ippatsu" },
                { "河底撈魚", "yaku_houtei" },
                // 2飜
                { "三色同順", "yaku_sanshoku" },
                { "三色同刻", "yaku_sanshoku_doukou" },
                { "三暗刻", "yaku_sanankou" },
                { "対々和", "yaku_toitoi" },
                { "混老頭", "yaku_honroutou" },
                { "混全帯么九", "yaku_chanta" },
                { "七対子", "yaku_chiitoi" },
                { "一気通貫", "yaku_ikkitsuukan" },
                // 3飜
                { "二盃口", "yaku_ryanpeikou" },
                { "混一色", "yaku_honitsu" },
                { "純全帯么九", "yaku_junchan" },
                // 6飜
                { "清一色", "yaku_chinitsu" },
                // 役満
                { "九蓮宝燈", "yaku_chuuren" },
                { "緑一色", "yaku_ryuuiisou" },
                { "清老頭", "yaku_chinroutou" },
                { "四暗刻", "yaku_suuankou" },
                // ダブル役満
                { "純正九蓮宝燈", "yaku_junsei_chuuren" },
            };

            yakuVoiceClips = new Dictionary<string, AudioClip>();
            foreach (var kvp in yakuFileMap)
            {
                // Audio/Voice/Yaku/ はResourcesフォルダ外なので、事前にロードしておく必要がある
                // → Addressables等が無い場合は手動でInspectorからセットするか、
                //    ファイルをResourcesフォルダに移動する必要がある。
                //    ここではResourcesに無い前提で、パスベースのロードを試みる。
                var clip = Resources.Load<AudioClip>("Voice/Yaku/" + kvp.Value);
                if (clip != null)
                {
                    yakuVoiceClips[kvp.Key] = clip;
                }
            }

            // ランク名 → ファイル名
            var rankFileMap = new Dictionary<string, string>
            {
                { "満貫", "rank_mangan" },
                { "跳満", "rank_haneman" },
                { "倍満", "rank_baiman" },
                { "三倍満", "rank_sanbaiman" },
                { "役満", "rank_yakuman" },
                { "ダブル役満", "rank_double_yakuman" },
            };

            rankVoiceClips = new Dictionary<string, AudioClip>();
            foreach (var kvp in rankFileMap)
            {
                var clip = Resources.Load<AudioClip>("Voice/rank/" + kvp.Value);
                if (clip != null)
                {
                    rankVoiceClips[kvp.Key] = clip;
                }
            }

            // ロンボイス
            ronVoiceClip = Resources.Load<AudioClip>("Voice/Yaku/ron");
        }

        /// <summary>
        /// ロン宣言ボイスを再生する
        /// </summary>
        public void PlayRonVoice()
        {
            if (yakuVoiceClips == null) InitializeYakuVoices();

            // 新しいronボイスがあればそちら、なければInspectorの旧ronVoice
            var clip = ronVoiceClip != null ? ronVoiceClip : ronVoice;
            if (clip != null) PlayVoice(clip);
        }

        /// <summary>
        /// 役名ボイスを再生する（例：「タンヤオ」「ピンフ」等）
        /// </summary>
        public void PlayYakuVoice(string yakuName)
        {
            if (yakuVoiceClips == null) InitializeYakuVoices();

            if (!string.IsNullOrEmpty(yakuName) && yakuVoiceClips.TryGetValue(yakuName, out AudioClip clip))
            {
                PlayVoice(clip);
            }
        }

        /// <summary>
        /// ランク名ボイスを再生する（例：「満貫！」「跳満！」等）
        /// </summary>
        public void PlayRankVoice(string rankName)
        {
            if (rankVoiceClips == null) InitializeYakuVoices();

            if (!string.IsNullOrEmpty(rankName) && rankVoiceClips.TryGetValue(rankName, out AudioClip clip))
            {
                PlayVoice(clip);
            }
        }
    }
}
