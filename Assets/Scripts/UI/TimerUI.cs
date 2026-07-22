using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

namespace KillingMahjong.UI
{
    public class TimerUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI timeText;

        private float timeLimit;
        private float currentTime;
        private bool isRunning;
        private bool isTimeout;

        private Coroutine flashCoroutine;
        private Color originalTextColor = Color.black; // デフォルトの色
        private bool isInitialized = false;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (isInitialized) return;
            isInitialized = true;
            
            // 動的生成は廃止し、インスペクターからセットする仕様に変更
            if (timeText == null)
            {
                timeText = GetComponentInChildren<TextMeshProUGUI>();
            }
            
            if (timeText != null)
            {
                originalTextColor = timeText.color;
            }
            gameObject.SetActive(false);
        }

        public void SetSize(float size)
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(size, size);
            }
        }

        public void StartTimer(float duration)
        {
            timeLimit = duration;
            currentTime = duration;
            isRunning = true;
            isTimeout = false;
            
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
            }
            
            // 色をリセット（画像に色が設定されている前提）
            if (timeText != null) 
            {
                timeText.color = originalTextColor;
                timeText.text = Mathf.CeilToInt(currentTime).ToString();
            }

            gameObject.SetActive(true);
        }

        public void StopTimer()
        {
            isRunning = false;
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
            }
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!isRunning) return;

            if (currentTime > 0)
            {
                currentTime -= Time.deltaTime;
                
                if (timeText != null)
                {
                    timeText.text = Mathf.CeilToInt(currentTime).ToString();
                }

                if (currentTime <= 0)
                {
                    currentTime = 0;
                    isRunning = false;
                    isTimeout = true;
                    if (timeText != null) timeText.text = "0";
                    
                    // タイムアウト時の点滅を開始
                    flashCoroutine = StartCoroutine(FlashRoutine());
                }
            }
        }

        private IEnumerator FlashRoutine()
        {
            bool isRed = false;
            while (true)
            {
                if (isRed)
                {
                    if (timeText != null) timeText.color = originalTextColor;
                }
                else
                {
                    if (timeText != null) timeText.color = Color.red; // 赤色
                }
                isRed = !isRed;
                yield return new WaitForSeconds(0.2f);
            }
        }
    }
}
