using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class YakuListUI : MonoBehaviour
    {
        [Header("Yaku List Panel (役Canvas2)")]
        [SerializeField] private GameObject yakuListPanel;
        [SerializeField] private Button toggleButton; // 役Canvas側の開くボタン
        [SerializeField] private Button closeButton;  // 役Canvas2側の閉じるボタン(xボタン)

        [Header("Yaku Items")]
        [SerializeField] private YakuItemUI itemPrefab;
        [SerializeField] private Transform contentContainer;
        private List<YakuItemUI> spawnedItems = new List<YakuItemUI>();

        [Header("Active Boosts (Always Visible)")]
        [SerializeField] private TextMeshProUGUI[] localActiveBoostTexts = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI[] enemyActiveBoostTexts = new TextMeshProUGUI[3];

        private void Start()
        {
            if (yakuListPanel != null)
                yakuListPanel.SetActive(false); // 最初は非表示

            if (toggleButton != null)
                toggleButton.onClick.AddListener(OpenYakuList);
                
            if (closeButton != null)
                closeButton.onClick.AddListener(CloseYakuList);
            
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

        public void OpenYakuList()
        {
            if (yakuListPanel != null)
            {
                yakuListPanel.SetActive(true);
            }
        }

        public void CloseYakuList()
        {
            if (yakuListPanel != null)
            {
                yakuListPanel.SetActive(false);
            }
        }

        private void InitializeItems()
        {
            if (itemPrefab == null || contentContainer == null) return;
            
            foreach (var yaku in allYakus)
            {
                var item = Instantiate(itemPrefab, contentContainer);
                spawnedItems.Add(item);
            }
        }

        public void UpdateBoostData(Dictionary<string, int> localBoost, Dictionary<string, int> enemyBoost)
        {
            if (spawnedItems.Count == 0) InitializeItems();

            if (localBoost == null) localBoost = new Dictionary<string, int>();
            if (enemyBoost == null) enemyBoost = new Dictionary<string, int>();

            for (int i = 0; i < allYakus.Length; i++)
            {
                string yaku = allYakus[i];
                int lBoost = localBoost.ContainsKey(yaku) ? localBoost[yaku] : 0;
                int eBoost = enemyBoost.ContainsKey(yaku) ? enemyBoost[yaku] : 0;

                if (i < spawnedItems.Count && spawnedItems[i] != null)
                {
                    spawnedItems[i].Setup(yaku, lBoost, eBoost);
                }
            }

            UpdateActiveBoosts(localBoost, localActiveBoostTexts);
            UpdateActiveBoosts(enemyBoost, enemyActiveBoostTexts);
        }

        private void UpdateActiveBoosts(Dictionary<string, int> boostDict, TextMeshProUGUI[] textArray)
        {
            if (textArray == null || textArray.Length == 0) return;

            List<KeyValuePair<string, int>> activeBoosts = new List<KeyValuePair<string, int>>();
            if (boostDict != null)
            {
                foreach (var kvp in boostDict)
                {
                    if (kvp.Value > 0)
                    {
                        activeBoosts.Add(kvp);
                    }
                }
            }

            for (int i = 0; i < textArray.Length; i++)
            {
                if (textArray[i] == null) continue;

                GameObject targetObj = textArray[i].gameObject;
                // 背景画像（Image）が親オブジェクトに設定されている場合、親ごと表示・非表示を切り替える
                if (textArray[i].transform.parent != null && 
                    textArray[i].transform.parent.gameObject != yakuListPanel &&
                    textArray[i].transform.parent.GetComponent<UnityEngine.UI.Image>() != null)
                {
                    targetObj = textArray[i].transform.parent.gameObject;
                }

                if (i < activeBoosts.Count)
                {
                    textArray[i].text = $"{activeBoosts[i].Key}+{activeBoosts[i].Value}";
                    targetObj.SetActive(true);
                    textArray[i].gameObject.SetActive(true); // テキスト自体も確実にONにする
                }
                else
                {
                    targetObj.SetActive(false);
                }
            }
        }

        private readonly string[] allYakus = {
            "断么九", "平和", "一盃口", "東", "西", "一発", "河底撈魚",
            "三色同順", "三色同刻", "三暗刻", "対々和", "混老頭", "混全帯么九", "七対子",
            "二盃口", "混一色", "純全帯么九",
            "清一色",
            "九蓮宝燈", "緑一色", "清老頭", "四暗刻", "純正九蓮宝燈"
        };
    }
}
