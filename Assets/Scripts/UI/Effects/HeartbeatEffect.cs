using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// HP低下時の「生きるか死ぬか」の緊張感を演出するエフェクトクラス
    /// 赤ビネットや黒ビネットを脈動させ、鼓動音を再生する
    /// </summary>
    public class HeartbeatEffect : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("HP30%以下で明滅する赤いビネット")]
        [SerializeField] private Image redVignette;
        [Tooltip("HP10%以下で追加される黒いビネット（絶望感演出）")]
        [SerializeField] private Image blackVignette;
        
        [Header("Settings")]
        [Tooltip("エフェクトが開始されるHPの割合（デフォルト30%）")]
        [SerializeField] private float warningThreshold = 0.3f;
        [Tooltip("絶望エフェクトが追加されるHPの割合（デフォルト10%）")]
        [SerializeField] private float dangerThreshold = 0.1f;
        
        [Tooltip("通常の鼓動の長さ（秒）")]
        [SerializeField] private float basePulseDuration = 1.2f;
        [Tooltip("ピンチ時の鼓動の長さ（秒）")]
        [SerializeField] private float dangerPulseDuration = 0.6f;
        
        private Coroutine heartbeatCoroutine;
        private bool isEffectActive = false;
        private float currentPulseDuration = 1.0f;

        private void Awake()
        {
            // 初期状態では透明にしておく
            if (redVignette != null) SetAlpha(redVignette, 0f);
            if (blackVignette != null) SetAlpha(blackVignette, 0f);
        }

        /// <summary>
        /// 外部からHPの状態を受け取り、エフェクトのON/OFFや速度を更新する
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
                // ピンチ状態（10%以下）なら鼓動を速くする
                currentPulseDuration = hpRatio <= dangerThreshold ? dangerPulseDuration : basePulseDuration;
                
                if (!isEffectActive)
                {
                    StartHeartbeat();
                }
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

        private void StartHeartbeat()
        {
            if (isEffectActive) return;
            isEffectActive = true;
            heartbeatCoroutine = StartCoroutine(HeartbeatRoutine());
        }

        private void StopHeartbeat()
        {
            isEffectActive = false;
            
            if (heartbeatCoroutine != null)
            {
                StopCoroutine(heartbeatCoroutine);
                heartbeatCoroutine = null;
            }

            // 透明度をリセット
            if (redVignette != null) SetAlpha(redVignette, 0f);
            if (blackVignette != null) SetAlpha(blackVignette, 0f);
        }

        private IEnumerator HeartbeatRoutine()
        {
            while (isEffectActive)
            {
                PlayHeartbeatSound();

                float halfDuration = currentPulseDuration / 2f;
                float t = 0f;

                // Fade In (ドクン…と現れる)
                while (t < halfDuration)
                {
                    t += Time.deltaTime;
                    float progress = t / halfDuration;
                    
                    // イージングをかけて少し生々しい脈動感にする
                    float easeProgress = progress * progress * (3f - 2f * progress); // SmoothStep
                    
                    float alpha = Mathf.Lerp(0f, 0.5f, easeProgress);
                    
                    if (redVignette != null) SetAlpha(redVignette, alpha);
                    
                    // Danger状態なら黒ビネットも混ぜて絶望感を出す
                    if (currentPulseDuration <= dangerPulseDuration && blackVignette != null)
                    {
                        SetAlpha(blackVignette, alpha * 0.7f);
                    }
                    else if (blackVignette != null)
                    {
                        SetAlpha(blackVignette, 0f);
                    }
                    
                    yield return null;
                }

                // Fade Out (スッと消える)
                t = 0f;
                while (t < halfDuration)
                {
                    t += Time.deltaTime;
                    float progress = t / halfDuration;
                    float alpha = Mathf.Lerp(0.5f, 0f, progress);
                    
                    if (redVignette != null) SetAlpha(redVignette, alpha);
                    if (currentPulseDuration <= dangerPulseDuration && blackVignette != null)
                    {
                        SetAlpha(blackVignette, alpha * 0.7f);
                    }
                    
                    yield return null;
                }
                
                // 次の鼓動までのインターバル
                yield return new WaitForSeconds(currentPulseDuration * 0.2f);
            }
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
