using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// HP低下時の「生きるか死ぬか」の緊張感を演出するエフェクトクラス。
    ///
    /// 明滅は目が疲れるため行わない。赤ビネットはじんわり浮かび上がって
    /// そのまま濃さを保ち、HPの段階が変わったときだけ滑らかに濃度を変える。
    /// 鼓動音だけは一定間隔で鳴らして緊張感を出す。
    /// </summary>
    public class HeartbeatEffect : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("HP30%以下でじんわり出る赤いビネット")]
        [SerializeField] private Image redVignette;
        [Tooltip("HP10%以下で追加される黒いビネット（絶望感演出）")]
        [SerializeField] private Image blackVignette;

        [Header("Thresholds")]
        [Tooltip("エフェクトが開始されるHPの割合（デフォルト30%）")]
        [SerializeField] private float warningThreshold = 0.3f;
        [Tooltip("絶望エフェクトが追加されるHPの割合（デフォルト10%）")]
        [SerializeField] private float dangerThreshold = 0.1f;

        [Header("Vignette")]
        [Tooltip("HP30%以下で維持する赤ビネットの濃さ")]
        [SerializeField, Range(0f, 1f)] private float warningAlpha = 0.28f;
        [Tooltip("HP10%以下で維持する赤ビネットの濃さ")]
        [SerializeField, Range(0f, 1f)] private float dangerAlpha = 0.45f;
        [Tooltip("黒ビネットの濃さ（赤ビネットに対する倍率）")]
        [SerializeField, Range(0f, 1f)] private float blackVignetteRatio = 0.6f;
        [Tooltip("濃さが変わるときにかける時間（秒）。長いほどじんわり")]
        [SerializeField] private float fadeDuration = 1.5f;

        [Header("Heartbeat Sound")]
        [Tooltip("通常時の鼓動音の間隔（秒）")]
        [SerializeField] private float basePulseDuration = 1.2f;
        [Tooltip("ピンチ時の鼓動音の間隔（秒）")]
        [SerializeField] private float dangerPulseDuration = 0.6f;

        private Coroutine vignetteCoroutine;
        private Coroutine soundCoroutine;
        private bool isEffectActive = false;
        private bool isDanger = false;

        // 現在表示している赤ビネットの濃さ。フェード中の値を引き継ぐために保持する
        private float currentAlpha = 0f;

        private void Awake()
        {
            // 初期状態では透明にしておく
            InitVignette(redVignette);
            InitVignette(blackVignette);
        }

        private void InitVignette(Image img)
        {
            if (img == null) return;

            SetAlpha(img, 0f);

            // ビネットは画面全体を覆うので、Raycast Target が有効なままだと
            // HPが閾値を割った瞬間に打牌のタッチを全て奪ってしまう。
            // インスペクタの設定漏れを防ぐためコード側でも必ず落とす。
            img.raycastTarget = false;
        }

        /// <summary>
        /// 外部からHPの状態を受け取り、エフェクトのON/OFFや濃さを更新する
        /// </summary>
        /// <param name="currentHp">現在のHP</param>
        /// <param name="maxHp">最大HP</param>
        public void UpdateHeartbeat(int currentHp, int maxHp)
        {
            if (maxHp <= 0) return;

            float hpRatio = (float)currentHp / maxHp;

            // HPが閾値以下、かつ生存している場合のみエフェクトを有効化
            if (hpRatio <= warningThreshold && currentHp > 0)
            {
                bool nextDanger = hpRatio <= dangerThreshold;

                // 既に有効で段階も変わっていなければ何もしない（毎フレーム呼ばれるため）
                if (isEffectActive && nextDanger == isDanger) return;

                isDanger = nextDanger;
                isEffectActive = true;

                StartVignetteFade(isDanger ? dangerAlpha : warningAlpha);
                RestartSoundLoop();
            }
            else
            {
                // HPが回復した、または死亡した場合はエフェクトを停止
                if (isEffectActive)
                {
                    StopHeartbeat();
                }
            }
        }

        /// <summary>
        /// ロン演出時やクリア時などに強制的にエフェクトを消す
        /// </summary>
        public void StopEffect()
        {
            StopHeartbeat();
        }

        private void StopHeartbeat()
        {
            isEffectActive = false;
            isDanger = false;

            if (soundCoroutine != null)
            {
                StopCoroutine(soundCoroutine);
                soundCoroutine = null;
            }

            // 消えるときもぶつ切りにせず、フェードアウトさせる
            StartVignetteFade(0f);
        }

        private void StartVignetteFade(float targetAlpha)
        {
            if (vignetteCoroutine != null) StopCoroutine(vignetteCoroutine);

            if (!gameObject.activeInHierarchy)
            {
                // 非アクティブではコルーチンを回せないので即座に反映する
                ApplyAlpha(targetAlpha);
                return;
            }

            vignetteCoroutine = StartCoroutine(FadeVignetteRoutine(targetAlpha));
        }

        private IEnumerator FadeVignetteRoutine(float targetAlpha)
        {
            float startAlpha = currentAlpha;

            if (fadeDuration <= 0f)
            {
                ApplyAlpha(targetAlpha);
                yield break;
            }

            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float progress = Mathf.Clamp01(t / fadeDuration);

                // SmoothStep でじんわり立ち上げる
                float eased = progress * progress * (3f - 2f * progress);
                ApplyAlpha(Mathf.Lerp(startAlpha, targetAlpha, eased));

                yield return null;
            }

            ApplyAlpha(targetAlpha);
            vignetteCoroutine = null;
        }

        private void ApplyAlpha(float alpha)
        {
            currentAlpha = alpha;

            if (redVignette != null) SetAlpha(redVignette, alpha);

            // 黒ビネットは絶望状態のときだけ薄く重ねる
            if (blackVignette != null)
            {
                SetAlpha(blackVignette, isDanger ? alpha * blackVignetteRatio : 0f);
            }
        }

        private void RestartSoundLoop()
        {
            if (soundCoroutine != null) StopCoroutine(soundCoroutine);
            if (!gameObject.activeInHierarchy) return;

            soundCoroutine = StartCoroutine(HeartbeatSoundRoutine());
        }

        private IEnumerator HeartbeatSoundRoutine()
        {
            while (isEffectActive)
            {
                PlayHeartbeatSound();
                yield return new WaitForSeconds(isDanger ? dangerPulseDuration : basePulseDuration);
            }

            soundCoroutine = null;
        }

        private void PlayHeartbeatSound()
        {
            if (KillingMahjong.Managers.AudioManager.Instance != null)
            {
                // 心臓の鼓動（低くて丸い音）
                // SynthWaveType は AudioSynth.cs と同じ KillingMahjong.Managers 名前空間にある
                KillingMahjong.Managers.AudioManager.Instance.PlaySynthSoundDual(
                    KillingMahjong.Managers.SynthWaveType.Sine,
                    KillingMahjong.Managers.SynthWaveType.Triangle,
                    150f, 60f, 0.2f, 1.0f);
            }
        }

        private void SetAlpha(Image img, float alpha)
        {
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }
    }
}
