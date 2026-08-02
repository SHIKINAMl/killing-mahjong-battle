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

        private void Awake()
        {
            if (yakuListPanel == null)
            {
                // Fallback to find the panel if unassigned
                Transform t = transform.Find("役Canvas2");
                if (t != null) yakuListPanel = t.gameObject;
                else if (gameObject.name == "役Canvas2") yakuListPanel = gameObject;
            }
        }

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

        /// <summary>役一覧が開いているか。チュートリアルの誘導待ちに使う。</summary>
        public bool IsOpen => yakuListPanel != null && yakuListPanel.activeSelf;

        /// <summary>役一覧を開くボタン。</summary>
        public RectTransform ToggleButtonRect =>
            toggleButton != null ? toggleButton.GetComponent<RectTransform>() : null;

        /// <summary>
        /// チュートリアルの誘導先。
        /// 開くボタンは画面右上の「役一覧」画像の上に重ねてあるだけで、それ単体を指しても
        /// どこを見ればよいか分からない。画像そのもの（親）を指して枠ごと見せる。
        /// </summary>
        public RectTransform GuideTargetRect
        {
            get
            {
                var buttonRect = ToggleButtonRect;
                if (buttonRect == null) return null;

                var parent = buttonRect.parent as RectTransform;
                return parent != null && parent.GetComponent<Image>() != null ? parent : buttonRect;
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

            // Dictionary の並び順は保証されないので、翻数の大きい順に並べ直す。
            // 枠に入りきらないときに「大きいものが落ちる」のを防ぐ意味もある。
            activeBoosts.Sort((a, b) =>
            {
                if (a.Value != b.Value) return b.Value.CompareTo(a.Value);
                return string.CompareOrdinal(a.Key, b.Key);
            });

            // 枠より多いときは、最後の枠を「あと何件あるか」に使う（要望22）。
            // 例）枠3・強化5件 → 上位2件を並べ、最後の枠に「+3」
            int slots = textArray.Length;
            bool overflow = activeBoosts.Count > slots;
            int shownCount = overflow ? slots - 1 : activeBoosts.Count;
            int hiddenCount = activeBoosts.Count - shownCount;

            for (int i = 0; i < textArray.Length; i++)
            {
                if (textArray[i] == null) continue;

                bool isOverflowSlot = overflow && i == slots - 1;

                if (i < shownCount || isOverflowSlot)
                {
                    textArray[i].text = isOverflowSlot
                        ? $"+{hiddenCount}"
                        : $"{activeBoosts[i].Key}+{activeBoosts[i].Value}";
                    textArray[i].gameObject.SetActive(true);
                    
                    // 背景画像（親オブジェクト）がある場合はそれも表示する
                    if (textArray[i].transform.parent != null && 
                        textArray[i].transform.parent.gameObject != yakuListPanel &&
                        !textArray[i].transform.parent.name.ToLower().Contains("panel") && // パネル全体を消さないように
                        textArray[i].transform.parent.GetComponent<UnityEngine.UI.Image>() != null)
                    {
                        textArray[i].transform.parent.gameObject.SetActive(true);
                    }
                }
                else
                {
                    textArray[i].gameObject.SetActive(false);
                    textArray[i].text = "";
                    
                    // 背景画像（親オブジェクト）がある場合は非表示にする
                    // ただし、それが親パネル（yakuListPanel）全体を包むものでないことを確認する
                    if (textArray[i].transform.parent != null && 
                        textArray[i].transform.parent.gameObject != yakuListPanel &&
                        !textArray[i].transform.parent.name.ToLower().Contains("panel") && // パネル全体を消さないように
                        textArray[i].transform.parent.GetComponent<UnityEngine.UI.Image>() != null)
                    {
                        textArray[i].transform.parent.gameObject.SetActive(false);
                    }
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
