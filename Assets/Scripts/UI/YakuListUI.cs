using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class YakuListUI : MonoBehaviour
    {
        [Header("Yaku List Panel")]
        [SerializeField] private GameObject yakuListPanel;
        [SerializeField] private Button toggleButton;

        [Header("Boost Texts")]
        [SerializeField] private TextMeshProUGUI localBoostText;
        [SerializeField] private TextMeshProUGUI enemyBoostText;

        [Header("Slide Settings")]
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private float slideDistanceY = 200f; // パネルが降りてくる距離（ピクセル）
        [SerializeField] private float slideDuration = 0.3f;  // アニメーションの時間

        private bool isShown = false;
        private Coroutine slideCoroutine;
        private Vector2 hiddenPos;
        private Vector2 shownPos;

        private void Start()
        {
            if (toggleButton != null)
                toggleButton.onClick.AddListener(ToggleYakuList);
            
            if (panelRect != null)
            {
                hiddenPos = panelRect.anchoredPosition;
                shownPos = hiddenPos + new Vector2(0, -slideDistanceY);
            }

            // 初期化時に New Text のままにならないようにデータを更新
            if (Managers.BoardStateManager.Instance != null)
            {
                UpdateBoostData(Managers.BoardStateManager.Instance.LocalBoostHandBonus, Managers.BoardStateManager.Instance.EnemyBoostHandBonus);
            }
            else
            {
                UpdateBoostData(null, null);
            }
        }

        public void ToggleYakuList()
        {
            isShown = !isShown;
            if (slideCoroutine != null) StopCoroutine(slideCoroutine);

            if (panelRect != null)
            {
                slideCoroutine = StartCoroutine(SlideRoutine(isShown ? shownPos : hiddenPos));
            }
        }

        private System.Collections.IEnumerator SlideRoutine(Vector2 targetPos)
        {
            float time = 0;
            Vector2 startPos = panelRect.anchoredPosition;

            while (time < slideDuration)
            {
                time += Time.deltaTime;
                float t = time / slideDuration;
                t = t * (2f - t); // イーズアウト（減速）
                panelRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }
            panelRect.anchoredPosition = targetPos;
        }

        public void UpdateBoostData(Dictionary<string, int> localBoost, Dictionary<string, int> enemyBoost)
        {
            if (localBoostText != null)
            {
                localBoostText.text = FormatBoostDict(localBoost);
            }

            if (enemyBoostText != null)
            {
                enemyBoostText.text = FormatBoostDict(enemyBoost);
            }
        }

        private readonly string[] allYakus = {
            "断么九", "平和", "一盃口", "東", "西", "一発", "河底撈魚",
            "三色同順", "三色同刻", "三暗刻", "対々和", "混老頭", "混全帯么九", "七対子",
            "二盃口", "混一色", "純全帯么九",
            "清一色",
            "九蓮宝燈", "緑一色", "清老頭", "四暗刻", "純正九蓮宝燈"
        };

        private string FormatBoostDict(Dictionary<string, int> dict)
        {
            if (dict == null) dict = new Dictionary<string, int>();
            
            string result = "";
            foreach (var yaku in allYakus)
            {
                int level = dict.ContainsKey(yaku) ? dict[yaku] : 0;
                result += $"{yaku}: +{level}\n";
            }
            return result.TrimEnd('\n');
        }
    }
}
