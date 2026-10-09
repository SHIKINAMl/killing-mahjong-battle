using UnityEngine;
using System;

namespace KillingMahjong.Core
{
    /// <summary>
    /// ゲーム内の設定（音量・システム・ゲームプレイ）を管理し、保存・読み込みを行うクラス
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        // ------------------------------------------------------------
        //  どのシーンから始めても設定が効くようにする（2026-10-04）
        //
        //  **以前は OpeningScene にしか置いていなかった。**
        //  そのためタイトルから始めると `Instance` が null のままで、
        //  `OptionUI` 側は軒並み `if (Instance != null)` で守ってあるので、
        //  **設定をいじっても黙って何も起きない**状態になっていた
        //  （ユーザー報告「設定で対局BGMを変えてもBGMが変わりません」）。
        //  対局BGMだけでなく、音量・文字送り・画面サイズも同じく効かない。
        //
        //  `AudioManager` と同じく、プレハブ1つを本物にして、居なければ出す。
        //  どちらも `AfterSceneLoad` だが、`AudioManager` を見にいくのは `Start` なので
        //  並び順は問題にならない（`Start` は全ての `Awake` のあとに走る）。
        // ------------------------------------------------------------

        /// <summary>`Resources` から見たプレハブの場所。</summary>
        private const string BootstrapPrefabPath = "SettingsManager";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (Instance != null) return;

            var prefab = Resources.Load<GameObject>(BootstrapPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[SettingsManager] Resources/" + BootstrapPrefabPath +
                                 " が見つかりません。このシーンでは設定が効きません。");
                return;
            }

            var go = Instantiate(prefab);
            go.name = prefab.name;
        }

        // --- オーディオ設定 ---
        [Header("Audio Settings")]
        // 既定値が 0.0f だったため初回起動のプレイヤーはBGMが鳴らない状態から始まっていた。
        // SEより控えめな 0.35f を既定にする。無音に戻したい場合はここと LoadSettings の
        // GetFloat 第2引数を 0.0f に戻す。
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float seVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.5f;

        public float BgmVolume => bgmVolume;
        public float SeVolume => seVolume;
        public float VoiceVolume => voiceVolume;

        // --- ゲームプレイ設定 ---
        [Header("Game Settings")]
        [SerializeField] private bool isHighSpeedMode = false;
        public bool IsHighSpeedMode => isHighSpeedMode; // 打牌スピード（標準/高速）

        // --- 対局中のBGM ---
        //
        // **どちらの曲を鳴らすかを選べるようにした（2026-09-26）。**
        // 採用2曲を入れたとき、対局中のフェイズを全部そちらが受け持つようにしたため、
        // 従来のフェイズ別BGM（bgm_prepare / bgm_betting / 場の4層 / bgm_ron）が
        // 一切鳴らなくなっていた。リマスター版を入れても聞こえない、という状態だった。
        [Header("Match BGM")]
        [SerializeField] private int matchBgmSet = (int)MatchBgmSetKind.Proposal4;
        public int MatchBgmSet => matchBgmSet;

        /// <summary>対局中に鳴らすBGMの種類。</summary>
        public enum MatchBgmSetKind
        {
            /// <summary>
            /// 採用した2曲を、通常と盛り上がりでクロスフェードする。
            /// **曲は 2026-10-09 に消した**（WebGL のビルドが100MBを超えたため。ユーザーの判断）。
            /// 番号は保存データに残っているので欠番にしてある。選ばれていたら第4案へ読み替える。
            /// </summary>
            Pair = 0,
            /// <summary>従来のフェイズ別BGM。打牌中の場のBGMは4層のステムで鳴る</summary>
            PerPhase = 1,
            /// <summary>
            /// 従来のフェイズ別BGMだが、**場のBGMを層で鳴らさない。**
            /// こうしないと bgm_field_1〜4 は一度も鳴らない（層のステムが先に使われるため）。
            /// </summary>
            PerPhaseNoLayers = 2,
            /// <summary>
            /// 第3案（2026-10-08）。フルミックス16曲をフェイズと濃度で切り替える。
            /// **「第3案」は作品案の名前で、上の3番目を置き換えたものではない。**
            /// 既存の番号は保存データに残っているので動かさず、新しい番号を足した。
            /// **曲は 2026-10-09 に消した**（同上）。欠番。選ばれていたら第4案へ読み替える。
            /// </summary>
            Proposal3 = 3,
            /// <summary>
            /// 第4案（2026-10-08、「回路圧」16曲）。第3案と同じく、フェイズと濃度で切り替える。
            /// **「第4案」も作品案の名前。** 既存の 0〜3 は動かさず、末尾に足した。
            /// </summary>
            Proposal4 = 4,
            /// <summary>
            /// 夜卓の灯火（2026-10-09、18本）。第4案と同じ仕組みで、フェイズと濃度で切り替える。
            /// 既存の 0〜4 は動かさず、末尾に足した。
            /// </summary>
            ReturningTheme = 5,
        }

        /// <summary>
        /// 選べる種類。設定の選択欄には、この並びで出る。
        ///
        /// **番号（MatchBgmSetKind）は飛び飛び。** 「新2曲（切替）」(0) と「第3案」(3) は
        /// 2026-10-09 に曲ごと消したので、ここに無い。選択欄の何番目かと番号は別物なので、
        /// <see cref="MatchBgmChoiceIndexOf"/> と <see cref="MatchBgmChoiceKinds"/> で行き来すること。
        /// </summary>
        public static readonly int[] MatchBgmChoiceKinds =
        {
            (int)MatchBgmSetKind.Proposal4,
            (int)MatchBgmSetKind.ReturningTheme,
            (int)MatchBgmSetKind.PerPhase,
            (int)MatchBgmSetKind.PerPhaseNoLayers,
        };

        /// <summary>
        /// 選べる種類の表示名。並び順は <see cref="MatchBgmChoiceKinds"/> と合わせること。
        ///
        /// **全角7文字まで。** 選択欄の幅は解像度の欄と同じで、8文字を超えると
        /// 折り返して下が切れる（実機で「フェイズ別（層なし）」が切れた）。
        /// </summary>
        public static readonly string[] MatchBgmChoiceLabels =
        {
            "第4案",
            "夜卓の灯火",
            "従来（層あり）",
            "従来（層なし）",
        };

        /// <summary>種類の番号が、選択欄の何番目か。選べない番号なら 0（第4案）。</summary>
        public static int MatchBgmChoiceIndexOf(int kind)
        {
            int index = System.Array.IndexOf(MatchBgmChoiceKinds, kind);
            return index < 0 ? 0 : index;
        }

        /// <summary>
        /// 選べない番号（消した「新2曲」「第3案」や、範囲の外）を第4案へ読み替える。
        /// 前の版で保存された選択が残っていても、無い曲を鳴らしにいかないようにする。
        /// </summary>
        public static int NormalizeMatchBgmSet(int kind)
        {
            return System.Array.IndexOf(MatchBgmChoiceKinds, kind) >= 0 ? kind : (int)MatchBgmSetKind.Proposal4;
        }

        // --- 文字送りの速さ ---
        //
        // NEEDY GIRL OVERDOSE の設定を参考にした（2026-09-27）。あちらは設定項目が
        // BGM / SE / 解像度 / 進行の速さ / 言語 の5つだけで、かなり絞ってある。
        // じゃんぱいあも、読む所が無い設定（旧 High Speed Mode / Show Effects）を
        // 画面から下ろして、代わりに実際に効くものを置く。
        [Header("Text Speed")]
        [SerializeField] private int textSpeed = (int)TextSpeedKind.Normal;
        public int TextSpeed => textSpeed;

        public enum TextSpeedKind { Slow = 0, Normal = 1, Fast = 2 }

        /// <summary>選べる速さの表示名。並び順は TextSpeedKind と合わせること。</summary>
        public static readonly string[] TextSpeedLabels = { "ゆっくり", "ふつう", "はやい" };

        /// <summary>1文字あたりの秒数。**小さいほど速い。** 既定（ふつう）は従来と同じ 0.03。</summary>
        private static readonly float[] TextSpeedSeconds = { 0.055f, 0.030f, 0.014f };

        /// <summary>いまの設定での1文字あたりの秒数。</summary>
        public float SecondsPerCharacter
        {
            get { return TextSpeedSeconds[Mathf.Clamp(textSpeed, 0, TextSpeedSeconds.Length - 1)]; }
        }

        // --- セリフの吹き出し ---
        //
        // **既定は「出さない」（2026-09-27、プランナーの判断）。**
        // 吹き出しの枠は要らないが、有りと無しの感触を見比べたいので切り替えを残す。
        // 消すのは枠（DialoguePanel の Image と Shadow）だけで、文字とLOGボタンは残る。
        [Header("Dialogue Bubble")]
        [SerializeField] private int dialogueBubble = (int)DialogueBubbleKind.Hidden;
        public int DialogueBubble => dialogueBubble;

        public enum DialogueBubbleKind { Hidden = 0, Shown = 1 }

        /// <summary>選べる表示の名前。並び順は DialogueBubbleKind と合わせること。</summary>
        public static readonly string[] DialogueBubbleLabels = { "出さない", "出す" };

        /// <summary>吹き出しの枠を出すかどうか。</summary>
        public bool ShowDialogueBubble
        {
            get { return dialogueBubble == (int)DialogueBubbleKind.Shown; }
        }

        // --- 画面サイズ ---
        //
        // **既定を「画面に合わせる」にした（2026-09-30）。**
        // プランナーから「比率はいいがサイズが小さい」と言われたため。
        // 以前の既定は 800×600 の等倍で、いまどきの画面では小さすぎた。
        //
        // **整数倍だけに絞るのはやめた。** ドット絵は整数倍で拡大するのが本来だが、
        // 1920×1080 の画面には 2倍（1200px）が入らず、整数倍だと 1倍のまま
        // 何も変わらない。いちばん多い画面で効果が無いので、半端な倍率を許して
        // 大きくするほうを選んだ（ユーザーの判断）。
        // 半端な倍率ではドットの幅が 1px と 2px で混ざる。
        [Header("Screen")]
        [SerializeField] private int screenMode = (int)ScreenModeKind.Fit;
        public int ScreenMode => screenMode;

        /// <summary>
        /// 画面の出し方。**並び順は保存値なので入れ替えないこと**
        /// （`PlayerPrefs` に数値で入っている）。
        /// </summary>
        public enum ScreenModeKind { X1 = 0, Fit = 1, FullScreen = 2 }

        public static readonly string[] ScreenModeLabels = { "800×600　等倍", "画面に合わせて大きく", "全画面" };

        // --- 表示・システム設定 ---
        [Header("System Settings")]
        [SerializeField] private bool isEffectEnabled = true;
        public bool IsEffectEnabled => isEffectEnabled; // 背景エフェクトのON/OFF

        // --- ウィンドウ（解像度）設定 ---
        [Header("Window Settings")]
        [SerializeField] private int resolutionIndex = 4; // デフォルトは 800x600
        [SerializeField] private bool isFullScreen = false;

        public int ResolutionIndex => resolutionIndex;
        public bool IsFullScreen => isFullScreen;

        // 設定が変更された時に呼ばれるイベント（UI側で受け取る用）
        public event Action OnSettingsChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // シーンをまたいでも破棄されないようにする

                // **グローバル音量は必ず全開に戻す（2026-08-26）。**
                // 以前はここの ApplySettings が AudioManager 不在時に
                // `AudioListener.volume = bgmVolume` を代用していた。0 が入ると
                // BGMだけでなくSEもボイスも全部消え、スライダーをいくら上げても
                // 誰も戻さないので二度と鳴らなかった。エディタでは Play をまたいで
                // 値が残ることがあるため、既に 0 で固まっている環境をここで治す。
                AudioListener.volume = 1f;

                LoadSettings();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 設定を当て直す。
        ///
        /// **`Awake` の実行順は保証されない。** `AudioManager` が後に起きると
        /// `LoadSettings()` の時点では `AudioManager.Instance` が null で、
        /// 保存した音量がどこにも当たらないまま終わる。
        /// `Start` は全ての `Awake` の後に走るので、ここで必ず一度当て直す。
        /// </summary>
        private void Start()
        {
            ApplySettings();
        }

        private void OnDestroy()
        {
            // 破棄済みオブジェクトを指したままにしない
            if (Instance == this) Instance = null;
        }

        public void LoadSettings()
        {
            // 過去のセーブデータを引き継ぐ（通常の挙動に戻す）
            // 初回起動時（キーがない場合）のみ、デフォルト値（BGM: 0.35f, SE: 0.5f）が採用されます
            bgmVolume = PlayerPrefs.GetFloat("BgmVolume", 0.35f);
            seVolume = PlayerPrefs.GetFloat("SeVolume", 0.5f);
            voiceVolume = PlayerPrefs.GetFloat("VoiceVolume", 0.5f);
            
            isHighSpeedMode = PlayerPrefs.GetInt("IsHighSpeedMode", isHighSpeedMode ? 1 : 0) == 1;
            isEffectEnabled = PlayerPrefs.GetInt("IsEffectEnabled", isEffectEnabled ? 1 : 0) == 1;

            matchBgmSet = NormalizeMatchBgmSet(PlayerPrefs.GetInt("MatchBgmSet", matchBgmSet));
            textSpeed = PlayerPrefs.GetInt("TextSpeed", textSpeed);
            screenMode = PlayerPrefs.GetInt("ScreenMode", screenMode);
            dialogueBubble = PlayerPrefs.GetInt("DialogueBubble", dialogueBubble);

            resolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", resolutionIndex);
            isFullScreen = PlayerPrefs.GetInt("IsFullScreen", isFullScreen ? 1 : 0) == 1;

            ApplySettings();
        }

        /// <summary>
        /// 設定を保存し、ゲーム内に適用する
        /// </summary>
        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("BgmVolume", bgmVolume);
            PlayerPrefs.SetFloat("SeVolume", seVolume);
            PlayerPrefs.SetFloat("VoiceVolume", voiceVolume);
            
            PlayerPrefs.SetInt("IsHighSpeedMode", isHighSpeedMode ? 1 : 0);
            PlayerPrefs.SetInt("IsEffectEnabled", isEffectEnabled ? 1 : 0);

            PlayerPrefs.SetInt("MatchBgmSet", matchBgmSet);
            PlayerPrefs.SetInt("TextSpeed", textSpeed);
            PlayerPrefs.SetInt("ScreenMode", screenMode);
            PlayerPrefs.SetInt("DialogueBubble", dialogueBubble);

            PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
            PlayerPrefs.SetInt("IsFullScreen", isFullScreen ? 1 : 0);

            PlayerPrefs.Save();
            
            ApplySettings();
            OnSettingsChanged?.Invoke();
        }

        // --- 設定値の変更メソッド ---
        public void SetBgmVolume(float volume) 
        { 
            bgmVolume = volume; 
            if (KillingMahjong.Managers.AudioManager.Instance != null) 
            {
                KillingMahjong.Managers.AudioManager.Instance.bgmVolume = volume;
                KillingMahjong.Managers.AudioManager.Instance.ApplyVolumes();
            }
        }
        
        /// <summary>
        /// 対局中のBGMの種類を変える。**その場で切り替わる。**
        /// 設定画面で選んだのに次の対局まで変わらないと、選べた実感がない。
        /// </summary>
        public void SetMatchBgmSet(int kind)
        {
            matchBgmSet = NormalizeMatchBgmSet(kind);
            var am = KillingMahjong.Managers.AudioManager.Instance;
            if (am != null) am.ApplyMatchBgmSet(matchBgmSet);
        }

        /// <summary>文字送りの速さを変える。次に出るセリフから効く。</summary>
        public void SetTextSpeed(int kind)
        {
            textSpeed = Mathf.Clamp(kind, 0, TextSpeedLabels.Length - 1);
        }

        /// <summary>
        /// セリフの吹き出しの枠を出すかどうかを変える。**その場で切り替わる。**
        /// 感触を見比べるための設定なので、次のセリフまで待たされると比べにくい。
        /// </summary>
        public void SetDialogueBubble(int kind)
        {
            dialogueBubble = Mathf.Clamp(kind, 0, DialogueBubbleLabels.Length - 1);
            KillingMahjong.UI.DialogueUI.ApplyBubbleSettingToAll();
        }

        /// <summary>画面サイズを変える。**その場で切り替わる。**</summary>
        public void SetScreenMode(int kind)
        {
            screenMode = Mathf.Clamp(kind, 0, ScreenModeLabels.Length - 1);
            ApplyResolution();
        }

        public void SetSeVolume(float volume) 
        { 
            seVolume = volume; 
            if (KillingMahjong.Managers.AudioManager.Instance != null) 
            {
                KillingMahjong.Managers.AudioManager.Instance.seVolume = volume;
                KillingMahjong.Managers.AudioManager.Instance.ApplyVolumes();
            }
        }
        
        public void SetVoiceVolume(float volume) 
        { 
            voiceVolume = volume; 
            if (KillingMahjong.Managers.AudioManager.Instance != null) 
            {
                KillingMahjong.Managers.AudioManager.Instance.voiceVolume = volume;
                KillingMahjong.Managers.AudioManager.Instance.ApplyVolumes();
            }
        }
        public void SetHighSpeedMode(bool isHighSpeed) { isHighSpeedMode = isHighSpeed; }
        public void SetEffectEnabled(bool isEnabled) { isEffectEnabled = isEnabled; }
        public void SetResolutionIndex(int index) { resolutionIndex = index; }
        public void SetFullScreen(bool isFull) { isFullScreen = isFull; }

        private void OnValidate()
        {
            // インスペクターで値を変えた時に、即座に適用されるようにする
            if (Application.isPlaying)
            {
                ApplySettings();
            }
        }

        /// <summary>
        /// 設定値を実際のゲーム内要素（音量など）に反映させる
        /// </summary>
        private void ApplySettings()
        {
            if (KillingMahjong.Managers.AudioManager.Instance != null)
            {
                KillingMahjong.Managers.AudioManager.Instance.bgmVolume = bgmVolume;
                KillingMahjong.Managers.AudioManager.Instance.seVolume = seVolume;
                KillingMahjong.Managers.AudioManager.Instance.voiceVolume = voiceVolume;
                KillingMahjong.Managers.AudioManager.Instance.ApplyVolumes();
                // 保存した選択を当て直す。ここで当てないと、起動のたびに
                // AudioManager の既定（採用2曲）へ戻ってしまう
                KillingMahjong.Managers.AudioManager.Instance.ApplyMatchBgmSet(matchBgmSet);
            }
            // **`AudioListener.volume` を代用してはいけない。**
            // あれはゲーム全体のマスターで、BGMの値を入れるとSEもボイスも巻き添えになる。
            // 0 が入ると全ての音が消え、スライダー（SetBgmVolume）は AudioManager しか
            // 触らないので、上げ直しても二度と戻らなかった。
            // `AudioManager` がまだ居ないだけなら、`Start()` の当て直しで拾える。

            ApplyResolution();
            KillingMahjong.UI.DialogueUI.ApplyBubbleSettingToAll();
        }

        /// <summary>
        /// 画面サイズを当てる。
        ///
        /// **WebGL では `Screen.SetResolution` を呼ばない。** ブラウザの窓の大きさはページ側が決めるので、
        /// 呼んでも効かない。代わりに、ページの canvas の出し方を切り替える
        /// （`Assets/Plugins/WebGL/KmCanvas.jslib`）。
        ///   800×600 等倍       … canvas を 800×600 で出す
        ///   画面に合わせて大きく … 4:3 を保ったまま窓いっぱい（`index.html` の CSS のまま）
        ///   全画面             … Web 版では「画面に合わせて大きく」と同じ（全画面への切り替えはしない）
        /// 2026-10-09 までは Web 版では何もしておらず、「800×600 にしても最大ぐらい大きくなる」と言われた。
        /// </summary>
        private void ApplyResolution()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // **`Screen.fullScreen` には触らない。** WebGL でこれを立てると、全画面に入った瞬間に
            // 描く解像度が 80x60 ほどに落ちて、マウスの指の絵だけが画面いっぱいに映った
            // （2026-10-09、公開したページでユーザーが踏んだ）。
            // Web 版の「全画面」は「窓いっぱい」と同じに扱う（本当の全画面はブラウザの F11）
            KmSetCanvasMode(screenMode == (int)ScreenModeKind.X1 ? 0
                          : screenMode == (int)ScreenModeKind.FullScreen ? 2 : 1);
#endif
#if !UNITY_WEBGL
            switch ((ScreenModeKind)screenMode)
            {
                case ScreenModeKind.Fit:
                    Vector2Int fit = LargestFittingSize();
                    Screen.SetResolution(fit.x, fit.y, false);
                    break;
                case ScreenModeKind.FullScreen:
                    Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, true);
                    break;
                default:
                    Screen.SetResolution(BaseWidth, BaseHeight, false);
                    break;
            }
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void KmSetCanvasMode(int mode);
#endif

        private const int BaseWidth = 800;
        private const int BaseHeight = 600;

        /// <summary>
        /// ディスプレイに収まる、いちばん大きい 4:3 の窓の大きさ。
        ///
        /// **画面の高さいっぱいには広げない。** タイトルバーとタスクバーのぶんが要る。
        /// 9割にしておくと、1080p で 972px 高（約1.62倍）になり、窓の枠が画面から出ない。
        ///
        /// **800×600 より小さくはしない。** 小さい画面で縮めると、UI の文字が読めなくなる。
        /// </summary>
        private static Vector2Int LargestFittingSize()
        {
            int screenW = Screen.currentResolution.width;
            int screenH = Screen.currentResolution.height;
            if (screenW <= 0 || screenH <= 0) return new Vector2Int(BaseWidth, BaseHeight);

            float usableW = screenW * 0.9f;
            float usableH = screenH * 0.9f;

            // 縦横の入るほうに合わせる
            float scale = Mathf.Min(usableW / BaseWidth, usableH / BaseHeight);
            if (scale < 1f) scale = 1f;

            return new Vector2Int(
                Mathf.RoundToInt(BaseWidth * scale),
                Mathf.RoundToInt(BaseHeight * scale));
        }
    }
}
