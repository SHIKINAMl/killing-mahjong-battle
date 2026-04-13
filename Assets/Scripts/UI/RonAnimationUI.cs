using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class RonAnimationUI : MonoBehaviour
    {
        [Header("UI Containers")]
        [SerializeField] private GameObject ronPanel; // The full-screen/modal panel for Ron
        [SerializeField] private GameObject yakuBackgroundPanel; // The panel containing hand/yaku info (役パネル)
        
        [Header("Step 1: Cut-in")]
        [SerializeField] private GameObject cutInContainer;
        [SerializeField] private Image cutInImage;
        [SerializeField] private float cutInDuration = 1.5f;

        [Header("Step 2: Hand Display")]
        [SerializeField] private GameObject handDisplayContainer;
        [SerializeField] private RectTransform handTilesParent;
        [SerializeField] private RectTransform ronTileSlot; // Separate slot visually decoupled but conceptually part of hand
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private TileResourceManager tileResourceManager;

        [Header("Hand Display Layout")]
        [Tooltip("The horizontal gap between each tile in the hand")]
        [SerializeField] private float tileSpacing = 68f;
        
        [Header("Step 3: Yaku Display")]
        [SerializeField] private GameObject yakuContainer;
        [SerializeField] private TextMeshProUGUI yakuTextTemplate; // Copy this for multiple yaku

        [Header("Step 4: Formula & Rank Display")]
        [SerializeField] private GameObject formulaContainer;
        [SerializeField] private TextMeshProUGUI formulaText;
        [SerializeField] private GameObject rankContainer;
        [SerializeField] private TextMeshProUGUI rankText; // e.g., "跳満"
        
        [Header("Timing Adjustments")]
        [SerializeField] private float delayAfterHandDisplay = 1.0f;
        [SerializeField] private float delayBetweenYakus = 0.3f;
        [SerializeField] private float delayBeforeFormula = 1.5f;
        [SerializeField] private float delayBeforeRank = 1.5f;
        [SerializeField] private float durationBeforeClosing = 3.0f;

        private void Start()
        {
            if (ronPanel != null) ronPanel.SetActive(false);
        }

        public void PlayRonSequence(List<int> handTiles, int ronTile, List<string> yakuList, string formula, string rankName, bool isLocalPlayerWin, System.Action onComplete)
        {
            if (ronPanel != null) ronPanel.SetActive(true);
            
            // Clean up old visuals
            ResetVisuals();

            StartCoroutine(SequenceRoutine(handTiles, ronTile, yakuList, formula, rankName, isLocalPlayerWin, onComplete));
        }

        private void ResetVisuals()
        {
            if (yakuBackgroundPanel != null) yakuBackgroundPanel.SetActive(false);
            if (cutInContainer != null) cutInContainer.SetActive(false);
            if (handDisplayContainer != null) handDisplayContainer.SetActive(false);
            if (yakuContainer != null) yakuContainer.SetActive(false);
            if (formulaContainer != null) formulaContainer.SetActive(false);
            if (rankContainer != null) rankContainer.SetActive(false);

            // Clean up instantiated hand tiles
            if (handTilesParent != null)
            {
                foreach (Transform child in handTilesParent)
                {
                    Destroy(child.gameObject);
                }
            }
            if (ronTileSlot != null)
            {
                foreach (Transform child in ronTileSlot)
                {
                    Destroy(child.gameObject);
                }
            }

            // Clean up instantiated yaku text
            if (yakuContainer != null)
            {
                foreach (Transform child in yakuContainer.transform)
                {
                    if (child.gameObject != yakuTextTemplate.gameObject)
                    {
                        Destroy(child.gameObject);
                    }
                }
                if (yakuTextTemplate != null) yakuTextTemplate.gameObject.SetActive(false);
            }
        }

        private IEnumerator SequenceRoutine(List<int> handTiles, int ronTile, List<string> yakuList, string formula, string rankName, bool isLocalPlayerWin, System.Action onComplete)
        {
            // --- Step 1: Cut-in ---
            Debug.Log("[RonAnimation] Step 1: Cut-in");
            if (cutInContainer != null)
            {
                cutInContainer.SetActive(true);
                // 実際はここでキャラクター画像を isLocalPlayerWin に応じて切り替えたりアニメーションしたりします
                // 現在はプレースホルダー待機
                yield return new WaitForSeconds(cutInDuration);
                cutInContainer.SetActive(false);
            }

            // --- Step 2: Hand and Ron Tile Display ---
            Debug.Log("[RonAnimation] Step 2: Hand Display");
            
            // Cut-in演出が終わったので、役パネル（背景等）を表示する
            if (yakuBackgroundPanel != null) yakuBackgroundPanel.SetActive(true);

            if (handDisplayContainer != null)
            {
                handDisplayContainer.SetActive(true);
                
                // 手牌の生成
                if (tilePrefab != null && tileResourceManager != null)
                {
                    int handCount = handTiles.Count;

                    for (int i = 0; i < handCount; i++)
                    {
                        GameObject obj = Instantiate(tilePrefab, handTilesParent);
                        InitializeTileVisual(obj, handTiles[i]);
                        
                        RectTransform rt = obj.GetComponent<RectTransform>();
                        ApplyTileRectSettings(rt);
                        
                        // 横に並べるためにX座標を計算 (要素がすべて中心に揃うようにハンド全体の幅から算出)
                        float offset_x = (i - (handCount - 1) / 2f) * tileSpacing;
                        rt.anchoredPosition3D = new Vector3(offset_x, 0, 0);
                    }
                    
                    // アガリ牌（ロン牌）の生成
                    if (ronTile > 0 && ronTileSlot != null)
                    {
                        GameObject obj = Instantiate(tilePrefab, ronTileSlot);
                        InitializeTileVisual(obj, ronTile);
                        
                        RectTransform rt = obj.GetComponent<RectTransform>();
                        ApplyTileRectSettings(rt);
                        rt.anchoredPosition3D = Vector3.zero;
                    }
                }
                
                yield return new WaitForSeconds(delayAfterHandDisplay);
            }

            // --- Step 3: Yaku Display ---
            Debug.Log("[RonAnimation] Step 3: Yaku Display");
            if (yakuContainer != null && yakuList != null && yakuList.Count > 0)
            {
                yakuContainer.SetActive(true);
                
                for (int i = 0; i < yakuList.Count; i++)
                {
                    GameObject yakuObj = Instantiate(yakuTextTemplate.gameObject, yakuContainer.transform);
                    yakuObj.SetActive(true);
                    TextMeshProUGUI tmp = yakuObj.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = yakuList[i];
                    
                    yield return new WaitForSeconds(delayBetweenYakus);
                }
                
                yield return new WaitForSeconds(delayBeforeFormula);
            }

            // --- Step 4: Formula Display ---
            Debug.Log("[RonAnimation] Step 4: Formula Display");
            if (formulaContainer != null)
            {
                formulaContainer.SetActive(true);
                if (formulaText != null) formulaText.text = formula;
                yield return new WaitForSeconds(delayBeforeRank);
            }

            // --- Step 5: Final Rank Display ---
            Debug.Log("[RonAnimation] Step 5: Rank Display");
            if (rankContainer != null)
            {
                rankContainer.SetActive(true);
                if (rankText != null) rankText.text = rankName;
                
                // 勝利側・敗北側のエフェクト再生はGameUIManagerのOnRonAnimationCompleteで行うためここは通過のみ
                // NotifyEffectsTrigger(isLocalPlayerWin);
                
                yield return new WaitForSeconds(durationBeforeClosing);
            }

            // --- Step 6: Cleanup and callback ---
            Debug.Log("[RonAnimation] Sequence Complete.");
            if (ronPanel != null) ronPanel.SetActive(false);
            onComplete?.Invoke();
        }

        private void InitializeTileVisual(GameObject tileObj, int tileId)
        {
            TileVisual visual = tileObj.GetComponent<TileVisual>();
            if (visual != null && tileResourceManager != null)
            {
                visual.SetTile(tileId, tileResourceManager.GetTileSprite(tileId));
            }
            
            // Interactionは不要なので消すか無効化する
            TileInteraction interaction = tileObj.GetComponent<TileInteraction>();
            if (interaction != null) Destroy(interaction);
        }

        private void ApplyTileRectSettings(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            
            // サイズは既存のPrefabのスケールを尊重し、スクリプトからは変更しないことで
            // エディタ側での細かいサイズ調整を可能にします
            rt.anchoredPosition3D = Vector3.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        // --- Tester Context Menu ---
        [ContextMenu("Test Ron Animation Local Win")]
        public void TestRonLocalWin()
        {
            List<int> dummyHand = new List<int> { 1, 2, 3, 5, 6, 7, 10, 11, 12, 19, 20, 21, 28 };
            List<string> dummyYaku = new List<string> { "立直 (1飜)", "一発 (1飜)", "門前清自摸和 (1飜)" };
            string dummyFormula = "30符 3飜";
            string dummyRank = "満貫";
            PlayRonSequence(dummyHand, 28, dummyYaku, dummyFormula, dummyRank, true, () => Debug.Log("Test Local Win complete"));
        }
        
        [ContextMenu("Test Ron Animation Enemy Win")]
        public void TestRonEnemyWin()
        {
            List<int> dummyHand = new List<int> { 9, 9, 9, 18, 18, 18, 27, 27, 27, 30, 30, 30, 33 };
            List<string> dummyYaku = new List<string> { "大三元 (役満)", "字一色 (役満)" };
            string dummyFormula = "ダブル役満";
            string dummyRank = "ダブル役満";
            PlayRonSequence(dummyHand, 33, dummyYaku, dummyFormula, dummyRank, false, () => Debug.Log("Test Enemy Win complete"));
        }
    }
}
