using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;
using UnityEngine.UI;
using TMPro;

namespace KillingMahjong.UI
{
    [RequireComponent(typeof(GameUIManager))]
    public class GameUISkillController : MonoBehaviour
    {
        private GameUIManager uiManager;
        private YakuSelectionUI yakuSelectionUI;

        [Header("Yaku Selection")]
        [SerializeField] private Font yakuSelectionFont;

        public bool IsMulliganSelection { get; private set; }

        public void Setup(GameUIManager manager)
        {
            this.uiManager = manager;
        }

        public void CancelSkillSelection()
        {
            IsMulliganSelection = false;
            DestroyMulliganDimmer();
        }

        private GameObject mulliganDimmer;
        private GameObject mulliganTextCanvas;
        private System.Collections.Generic.List<GameObject> hiddenUIs = new System.Collections.Generic.List<GameObject>();

        public void StartMulliganSelection()
        {
            IsMulliganSelection = true;
            CreateMulliganDimmer();
        }

        public void OnMulliganTileSelected(int tileId)
        {
            IsMulliganSelection = false;
            DestroyMulliganDimmer();
            
            var wallTiles = BoardStateManager.Instance.OriginalWallTiles;
            if (wallTiles != null)
            {
                int targetIndex = wallTiles.IndexOf(tileId);
                if (targetIndex != -1)
                {
                    uiManager.SendActionToServer("skill", new Network.ActionPayload { skill_type = "mulligan", target_hand_index = targetIndex });
                }
                else
                {
                    Debug.LogWarning("Mulligan failed: Selected tile not found in wall tiles.");
                }
            }
        }

        private void CreateMulliganDimmer()
        {
            if (mulliganDimmer != null) return;
            
            mulliganDimmer = new GameObject("MulliganDimmer");
            var rt = mulliganDimmer.AddComponent<RectTransform>();
            
            // Parent to the root Canvas to ensure it covers the whole screen
            Canvas rootCanvas = uiManager.HandUI != null ? uiManager.HandUI.GetComponentInParent<Canvas>() : null;
            if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;
            Transform parentTransform = rootCanvas != null ? rootCanvas.transform : uiManager.transform;
            
            mulliganDimmer.transform.SetParent(parentTransform, false);
            
            Canvas dimmerCanvas = mulliganDimmer.AddComponent<Canvas>();
            dimmerCanvas.overrideSorting = true;
            dimmerCanvas.sortingOrder = 32000; // Extremely high to cover most UI
            if (rootCanvas != null) dimmerCanvas.sortingLayerID = rootCanvas.sortingLayerID;
            
            mulliganDimmer.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // 1. Dimmer Background (Nested, ScreenSpaceCamera to allow Hand/Wall on top)
            Image bg = mulliganDimmer.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.75f);
            
            // Stretch to fill root canvas
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            // 2. Text Canvas (Root, ScreenSpaceOverlay to guarantee it is on top of ALL UI)
            mulliganTextCanvas = new GameObject("MulliganTextCanvas");
            mulliganTextCanvas.transform.SetParent(null);
            Canvas textCanvas = mulliganTextCanvas.AddComponent<Canvas>();
            textCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            textCanvas.sortingOrder = 1000; // Always on top
            
            var scaler = mulliganTextCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800, 600);

            GameObject textObj = new GameObject("PromptText");
            textObj.transform.SetParent(mulliganTextCanvas.transform, false);
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            
            // Ensure font is set by copying from DialogueUI
            if (uiManager.DialogueUI != null)
            {
                var dialogueTmp = uiManager.DialogueUI.GetComponentInChildren<TextMeshProUGUI>(true);
                if (dialogueTmp != null) tmp.font = dialogueTmp.font;
            }
            if (tmp.font == null && TMPro.TMP_Settings.defaultFontAsset != null) 
            {
                tmp.font = TMPro.TMP_Settings.defaultFontAsset;
            }

            tmp.text = "手牌か山牌から交換する牌を選んでください";
            tmp.fontSize = 36;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            
            UnityEngine.UI.Shadow shadow = textObj.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(4, -4);

            RectTransform txtRt = textObj.GetComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0, 0.45f);
            txtRt.anchorMax = new Vector2(1, 0.85f);
            txtRt.sizeDelta = Vector2.zero;
            txtRt.anchoredPosition = Vector2.zero;
            
            BringToFront(uiManager.HandUI?.gameObject, 32005);
            BringToFront(uiManager.WallUI?.gameObject, 32005);
            
            // Hide distracting/overlapping UI elements
            hiddenUIs.Clear();
            HideIfActive(uiManager.DialogueUI?.gameObject);
            HideIfActive(uiManager.PlayerInfoUI?.gameObject);
            HideIfActive(uiManager.EnemyInfoUI?.gameObject);
            HideIfActive(uiManager.YakuListUI?.gameObject);
        }

        private void HideIfActive(GameObject go)
        {
            if (go != null && go.activeSelf)
            {
                hiddenUIs.Add(go);
                go.SetActive(false);
            }
        }

        private System.Collections.Generic.Dictionary<GameObject, bool> addedCanvases = new System.Collections.Generic.Dictionary<GameObject, bool>();

        private void BringToFront(GameObject go, int order)
        {
            if (go == null) return;
            var canvas = go.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = go.AddComponent<Canvas>();
                go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                addedCanvases[go] = true;
            }
            else
            {
                addedCanvases[go] = false;
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;
        }

        private void ResetSorting(GameObject go)
        {
            if (go == null) return;
            if (addedCanvases.TryGetValue(go, out bool wasAdded))
            {
                var canvas = go.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.overrideSorting = false;
                }
                addedCanvases.Remove(go);
            }
        }

        private void DestroyMulliganDimmer()
        {
            if (mulliganDimmer != null)
            {
                Destroy(mulliganDimmer);
                mulliganDimmer = null;
            }
            if (mulliganTextCanvas != null)
            {
                Destroy(mulliganTextCanvas);
                mulliganTextCanvas = null;
            }
            if (uiManager != null)
            {
                ResetSorting(uiManager.HandUI?.gameObject);
                ResetSorting(uiManager.WallUI?.gameObject);
            }
            
            foreach (var go in hiddenUIs)
            {
                if (go != null) go.SetActive(true);
            }
            hiddenUIs.Clear();
        }

        public void StartBoostHandSelection()
        {
            if (yakuSelectionUI == null)
            {
                yakuSelectionUI = gameObject.AddComponent<YakuSelectionUI>();
                if (yakuSelectionFont != null)
                {
                    yakuSelectionUI.customFont = yakuSelectionFont;
                }
            }

            yakuSelectionUI.Show(
                onSelected: (yakuName) => {
                    uiManager.SendActionToServer("skill", new Network.ActionPayload { skill_type = "boost_hand", yaku_name = yakuName });
                },
                onCanceled: () => {
                    Debug.Log("Boost hand cancelled");
                }
            );
        }

        private string GetSkillName(string skillType)
        {
            switch (skillType)
            {
                case "mulligan": return "牌交換";
                case "perspective": return "透視";
                case "boost_hand": return "役強化";
                case "special_victory": return "特殊勝利";
                default: return skillType;
            }
        }

        public void HandleSkillCasted(SkillCastedData data)
        {
            StartCoroutine(HandleSkillCastedRoutine(data));
        }

        private System.Collections.IEnumerator HandleSkillCastedRoutine(SkillCastedData data)
        {
            string localPlayerId = KillingMahjong.Network.NetworkMessageHandler.Instance.LocalPlayerId;
            bool isLocalPlayer = (data.player_id == localPlayerId);
            string skillName = GetSkillName(data.skillType);
            string subText = null;

            if (data.skillType == "boost_hand")
            {
                var oldLocalBonus = Managers.BoardStateManager.Instance.LocalBoostHandBonus != null ? 
                    new Dictionary<string, int>(Managers.BoardStateManager.Instance.LocalBoostHandBonus) : new Dictionary<string, int>();
                var oldEnemyBonus = Managers.BoardStateManager.Instance.EnemyBoostHandBonus != null ? 
                    new Dictionary<string, int>(Managers.BoardStateManager.Instance.EnemyBoostHandBonus) : new Dictionary<string, int>();

                bool statusReceived = false;
                System.Action<KillingMahjong.EngineData.StatusData> onStatus = (statusData) => { statusReceived = true; };
                NetworkMessageHandler.Instance.OnStatusReceived += onStatus;

                float timeout = 2.0f;
                while (!statusReceived && timeout > 0)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }
                NetworkMessageHandler.Instance.OnStatusReceived -= onStatus;

                var newLocalBonus = Managers.BoardStateManager.Instance.LocalBoostHandBonus ?? new Dictionary<string, int>();
                var newEnemyBonus = Managers.BoardStateManager.Instance.EnemyBoostHandBonus ?? new Dictionary<string, int>();

                var targetOldBonus = isLocalPlayer ? oldLocalBonus : oldEnemyBonus;
                var targetNewBonus = isLocalPlayer ? newLocalBonus : newEnemyBonus;

                string boostedYakuName = "";
                foreach (var kvp in targetNewBonus)
                {
                    if (!targetOldBonus.ContainsKey(kvp.Key) || targetOldBonus[kvp.Key] < kvp.Value)
                    {
                        boostedYakuName = kvp.Key;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(boostedYakuName))
                {
                    subText = $"<color=yellow>{boostedYakuName}</color>";
                }
            }

            // 1. 以前の大迫力カットイン演出（血飛沫＋立ち絵＋巨大テキスト）を再生する
            if (uiManager.PhaseTransitionUI != null)
            {
                yield return uiManager.PhaseTransitionUI.PlaySkillCutinAnimationRoutine(skillName, isLocalPlayer, 2.0f, null, subText);
            }
            else if (uiManager.DialogueUI != null)
            {
                string castMessage = isLocalPlayer ? $"【あなた】がアビリティを発動！\n「{skillName}」" : $"【相手】がアビリティを発動！\n「{skillName}」";
                uiManager.DialogueUI.ShowText(castMessage);
                yield return new WaitForSeconds(2.0f);
            }

            // 2. HP（コスト）の支払い演出
            if (isLocalPlayer)
            {
                int currentLocalHp = Managers.BoardStateManager.Instance.LocalPlayerHp;
                int currentEnemyHp = Managers.BoardStateManager.Instance.EnemyPlayerHp;
                Managers.BoardStateManager.Instance.UpdateHp(currentLocalHp - data.cost, currentEnemyHp);
                if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetHP(Managers.BoardStateManager.Instance.LocalPlayerHp);
            }
            else
            {
                int currentLocalHp = Managers.BoardStateManager.Instance.LocalPlayerHp;
                int currentEnemyHp = Managers.BoardStateManager.Instance.EnemyPlayerHp;
                Managers.BoardStateManager.Instance.UpdateHp(currentLocalHp, currentEnemyHp - data.cost);
                if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetHP(Managers.BoardStateManager.Instance.EnemyPlayerHp);
            }

            // 体力が減る様子をしっかり見せるためのタメ（待機）
            yield return new WaitForSeconds(1.0f);

            // --- 以降、実際のアビリティ効果（透視など）を実行 ---

            if (data.skillType == "perspective")
            {
                if (data.exposedHandIndexes != null && data.exposedHandIndexes.Count > 0)
                {
                    // localPlayerId is already declared at the top of the method
                    List<int> targetIndexes = data.exposedHandIndexes;
                    
                    if (data.exposedHandIndexesByPlayer != null)
                    {
                        if (isLocalPlayer)
                        {
                            foreach (var kvp in data.exposedHandIndexesByPlayer)
                            {
                                if (kvp.Key != localPlayerId)
                                {
                                    targetIndexes = kvp.Value;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            if (data.exposedHandIndexesByPlayer.ContainsKey(localPlayerId))
                            {
                                targetIndexes = data.exposedHandIndexesByPlayer[localPlayerId];
                            }
                        }
                    }

                    if (isLocalPlayer)
                    {
                        List<int> newlyExposed = new List<int>();
                        foreach (int val in targetIndexes)
                        {
                            int wallIdx = val; // Python sends wall indices
                            
                            if (wallIdx >= 0 && wallIdx < 34)
                            {
                                if (!Managers.BoardStateManager.Instance.ExposedEnemyHandWallIndexes.Contains(wallIdx))
                                {
                                    newlyExposed.Add(wallIdx);
                                    Managers.BoardStateManager.Instance.ExposedEnemyHandWallIndexes.Add(wallIdx);
                                }
                            }
                        }
                        
                        Debug.Log($"[Skill] Local player cast perspective. targetIndexes: {string.Join(",", targetIndexes)}. newlyExposed: {string.Join(",", newlyExposed)}");
                        
                        if (newlyExposed.Count > 0)
                        {
                            if (uiManager.VisualController != null)
                            {
                                yield return uiManager.VisualController.PlayPerspectiveAnimation(newlyExposed);
                            }
                        }
                        else
                        {
                            uiManager.VisualController?.RebuildAllTilesFromState();
                        }
                    }
                    else
                    {
                        Debug.Log($"[Skill] Enemy player cast perspective. targetIndexes for local player: {string.Join(",", targetIndexes)}");
                        foreach (int val in targetIndexes)
                        {
                            int wallIdx = val; // Python sends wall indices
                            
                            if (wallIdx >= 0 && wallIdx < 34)
                            {
                                Managers.BoardStateManager.Instance.ExposedLocalHandWallIndexes.Add(wallIdx);
                            }
                        }
                        uiManager.VisualController?.RebuildAllTilesFromState();
                    }
                }
            }
            else if (data.skillType == "mulligan")
            {
                if (isLocalPlayer)
                {
                    uiManager.ClearSelection();
                }
            }
        }
    }
}
