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

        private int _lastMulliganOutTileId = -1;
        private int _lastMulliganTargetIndex = -1;

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

        private RectTransform _lastMulliganOutSlotRt;

        public void OnMulliganTileSelected(int tileId, RectTransform slotRt)
        {
            IsMulliganSelection = false;
            DestroyMulliganDimmer();
            
            // アニメーション中の不意なRebuildを防ぐ
            uiManager.SetIsTransitioning(true);
            
            var wallTiles = BoardStateManager.Instance.OriginalWallTiles;
            if (wallTiles != null)
            {
                int targetIndex = wallTiles.IndexOf(tileId);
                if (targetIndex != -1)
                {
                    _lastMulliganOutTileId = tileId;
                    _lastMulliganTargetIndex = targetIndex;
                    _lastMulliganOutSlotRt = slotRt;
                    
                    // クリック直後には透明にしない。アニメーション開始時に透明にする。

                    uiManager.SendActionToServer("skill", new Network.ActionPayload { skill_type = "mulligan", target_hand_index = targetIndex });
                }
                else
                {
                    Debug.LogWarning("Mulligan failed: Selected tile not found in wall tiles.");
                    uiManager.SetIsTransitioning(false);
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

        private class CanvasState
        {
            public bool WasAdded;
            public bool OriginalOverrideSorting;
            public int OriginalSortingOrder;
        }
        private System.Collections.Generic.Dictionary<GameObject, CanvasState> _canvasStates = new System.Collections.Generic.Dictionary<GameObject, CanvasState>();

        private void BringToFront(GameObject go, int order)
        {
            if (go == null) return;
            var canvas = go.GetComponent<Canvas>();
            CanvasState state = new CanvasState();
            if (canvas == null)
            {
                canvas = go.AddComponent<Canvas>();
                go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                state.WasAdded = true;
            }
            else
            {
                state.WasAdded = false;
                state.OriginalOverrideSorting = canvas.overrideSorting;
                state.OriginalSortingOrder = canvas.sortingOrder;
            }
            
            _canvasStates[go] = state;
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;
        }

        private void ResetSorting(GameObject go)
        {
            if (go == null) return;
            if (_canvasStates.TryGetValue(go, out CanvasState state))
            {
                if (state.WasAdded)
                {
                    var raycaster = go.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                    if (raycaster != null) Destroy(raycaster);
                    var canvas = go.GetComponent<Canvas>();
                    if (canvas != null) Destroy(canvas);
                }
                else
                {
                    var canvas = go.GetComponent<Canvas>();
                    if (canvas != null)
                    {
                        canvas.overrideSorting = state.OriginalOverrideSorting;
                        canvas.sortingOrder = state.OriginalSortingOrder;
                    }
                }
                _canvasStates.Remove(go);
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

            // --- プレ解析：透視スキルの場合はカットインと同時に演出を開始する ---
            List<int> newlyExposed = new List<int>();
            if (data.skillType == "perspective" && isLocalPlayer)
            {
                if (data.exposedHandIndexes != null && data.exposedHandIndexes.Count > 0)
                {
                    List<int> targetIndexes = data.exposedHandIndexes;
                    if (data.exposedHandIndexesByPlayer != null)
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

                    foreach (int val in targetIndexes)
                    {
                        int wallIdx = val;
                        if (wallIdx >= 0 && wallIdx < 34)
                        {
                            if (!Managers.BoardStateManager.Instance.ExposedEnemyHandWallIndexes.Contains(wallIdx))
                            {
                                newlyExposed.Add(wallIdx);
                                Managers.BoardStateManager.Instance.ExposedEnemyHandWallIndexes.Add(wallIdx);
                            }
                        }
                    }
                }
                
                if (newlyExposed.Count > 0 && uiManager.VisualController != null)
                {
                    // カットイン演出と並行して裏でめくる演出をスタート
                    StartCoroutine(uiManager.VisualController.PlayPerspectiveAnimation(newlyExposed));
                }
                else
                {
                    uiManager.VisualController?.RebuildAllTilesFromState();
                }
            }

            // 1. 以前の大迫力カットイン演出（血飛沫＋立ち絵＋巨大テキスト）を再生する
            if (uiManager.PhaseTransitionUI != null)
            {
                CharacterData cData = isLocalPlayer ? uiManager.PlayerInfoUI.CurrentCharacterData : uiManager.EnemyInfoUI.CurrentCharacterData;
                yield return uiManager.PhaseTransitionUI.PlaySkillCutinAnimationRoutine(skillName, isLocalPlayer, cData, 2.0f, null, subText);
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

            // --- 以降、実際のアビリティ効果（透視以外）を実行 ---

            if (data.skillType == "perspective")
            {
                // perspectiveのアニメーションは既に上部で並行スタートしているので、ここでは何もしない。
                // 敵プレイヤーの透視の場合は、UIの更新等のみ（対象インデックス自体は既知なのでサーバーからの同期に任せるかリビルド）
                if (!isLocalPlayer)
                {
                    List<int> targetIndexes = data.exposedHandIndexes;
                    if (data.exposedHandIndexesByPlayer != null && data.exposedHandIndexesByPlayer.ContainsKey(localPlayerId))
                    {
                        targetIndexes = data.exposedHandIndexesByPlayer[localPlayerId];
                    }

                    if (targetIndexes != null)
                    {
                        foreach (int val in targetIndexes)
                        {
                            int wallIdx = val; // Python sends wall indices
                            
                            if (wallIdx >= 0 && wallIdx < 34)
                            {
                                Managers.BoardStateManager.Instance.ExposedLocalHandWallIndexes.Add(wallIdx);
                            }
                        }
                    }
                    uiManager.VisualController?.RebuildAllTilesFromState();
                }
            }
            else if (data.skillType == "mulligan")
            {
                if (isLocalPlayer)
                {
                    uiManager.ClearSelection();
                    
                    if (_lastMulliganOutTileId != -1 && _lastMulliganTargetIndex != -1)
                    {
                        int oldTileId = _lastMulliganOutTileId;
                        int newTileId = -1;
                        
                        float timeout = 2.0f;
                        while (timeout > 0)
                        {
                            if (Managers.BoardStateManager.Instance.OriginalWallTiles != null &&
                                Managers.BoardStateManager.Instance.OriginalWallTiles.Count > _lastMulliganTargetIndex)
                            {
                                int currentAtIdx = Managers.BoardStateManager.Instance.OriginalWallTiles[_lastMulliganTargetIndex];
                                if (currentAtIdx != oldTileId)
                                {
                                    newTileId = currentAtIdx;
                                    break;
                                }
                            }
                            timeout -= Time.deltaTime;
                            yield return null;
                        }
                        
                        if (newTileId != -1)
                        {
                            yield return PlayMulliganAnimationRoutine(oldTileId, newTileId);
                        }
                        else
                        {
                            Debug.LogWarning("Mulligan animation failed: IN tile not received in time.");
                        }

                        _lastMulliganOutTileId = -1;
                        _lastMulliganTargetIndex = -1;
                    }
                }
            }
        }

        private System.Collections.IEnumerator PlayMulliganAnimationRoutine(int outTileId, int inTileId)
        {
            // 1. 元の牌のUIスロットの座標を取得（選択時に保存したものを使う）
            RectTransform originalSlotRt = _lastMulliganOutSlotRt;

            GameObject animContainer = new GameObject("MulliganAnimationContainer");
            Canvas canvas = animContainer.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            animContainer.AddComponent<UnityEngine.UI.CanvasScaler>().uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;

            Vector2 startPos = new Vector2(0, -400); // 見つからなかった場合のデフォルト
            Vector2 initialSize = new Vector2(120, 180);
            Vector3 initialScale = Vector3.one;

            if (originalSlotRt != null)
            {
                // Overlay Canvasの場合、positionはスクリーン座標
                RectTransform animCanvasRt = animContainer.GetComponent<RectTransform>();
                Vector2 localPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(animCanvasRt, originalSlotRt.position, null, out localPos);
                startPos = localPos;

                initialSize = originalSlotRt.rect.size;
                initialScale = originalSlotRt.localScale;

                // 元のスロットの画像を一時的に透明にする
                var cg = originalSlotRt.GetComponent<CanvasGroup>();
                if (cg == null) cg = originalSlotRt.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0;
            }

            // 背景の暗転
            GameObject bgObj = new GameObject("Bg");
            bgObj.transform.SetParent(animContainer.transform, false);
            var bgImg = bgObj.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0, 0, 0, 0.85f);
            bgImg.rectTransform.anchorMin = Vector2.zero;
            bgImg.rectTransform.anchorMax = Vector2.one;
            bgImg.rectTransform.offsetMin = Vector2.zero;
            bgImg.rectTransform.offsetMax = Vector2.zero;

            // テキストの用意
            GameObject outTextObj = new GameObject("OutText");
            outTextObj.transform.SetParent(animContainer.transform, false);
            var outText = outTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            outText.text = "OUT";
            outText.fontSize = 150;
            outText.color = new Color(1f, 0.2f, 0.2f, 0f);
            outText.alignment = TMPro.TextAlignmentOptions.Center;
            outText.fontStyle = TMPro.FontStyles.Bold;
            outText.rectTransform.sizeDelta = new Vector2(400, 200);
            outText.rectTransform.anchoredPosition = new Vector2(-280, 0); // 画面内に見える位置
            
            // テキストを牌の後ろにするために先に生成したが、Canvasのソートは無いので後でSiblingIndexを調整
            outText.transform.SetAsFirstSibling();
            bgObj.transform.SetAsFirstSibling();

            GameObject inTextObj = new GameObject("InText");
            inTextObj.transform.SetParent(animContainer.transform, false);
            var inText = inTextObj.AddComponent<TMPro.TextMeshProUGUI>();
            inText.text = "IN";
            inText.fontSize = 150;
            inText.color = new Color(0.2f, 0.8f, 1f, 0f);
            inText.alignment = TMPro.TextAlignmentOptions.Center;
            inText.fontStyle = TMPro.FontStyles.Bold;
            inText.rectTransform.sizeDelta = new Vector2(400, 200);
            inText.rectTransform.anchoredPosition = new Vector2(280, 0); // 画面内に見える位置
            inText.transform.SetSiblingIndex(1); // 背景の次

            // OUT Tile
            GameObject outObj = new GameObject("OutTile");
            outObj.transform.SetParent(animContainer.transform, false);
            var outRt = outObj.AddComponent<RectTransform>();
            outRt.sizeDelta = initialSize;
            outRt.localScale = initialScale;
            var outImg = outObj.AddComponent<UnityEngine.UI.Image>();
            var outVis = outObj.AddComponent<TileVisual>();
            if (uiManager.TileResourceManager != null)
            {
                outVis.SetTile(outTileId, uiManager.TileResourceManager.GetTileSprite(outTileId), uiManager.TileResourceManager);
            }

            // IN Tile (最初は非表示)
            GameObject inObj = new GameObject("InTile");
            inObj.transform.SetParent(animContainer.transform, false);
            var inRt = inObj.AddComponent<RectTransform>();
            inRt.sizeDelta = initialSize;
            inRt.localScale = initialScale;
            var inImg = inObj.AddComponent<UnityEngine.UI.Image>();
            inImg.color = new Color(1, 1, 1, 0); // 初期は透明
            var inVis = inObj.AddComponent<TileVisual>();
            if (uiManager.TileResourceManager != null)
            {
                inVis.SetTile(inTileId, uiManager.TileResourceManager.GetTileSprite(inTileId), uiManager.TileResourceManager);
            }

            // --- アニメーション開始 ---
            Vector3 targetScale = new Vector3(3.5f, 3.5f, 1f); // 牌をさらに大きく拡大
            Vector2 outCenterPos = new Vector2(-80, 0); // 牌をさらに中央に寄せる
            Vector2 inCenterPos = new Vector2(80, 0); // 牌をさらに中央に寄せる

            // 1. 元の場所から左のOUT位置へ飛んでいく
            outRt.anchoredPosition = startPos;
            float t = 0;
            while(t < 0.3f)
            {
                t += Time.deltaTime;
                float progress = Mathf.Sin((t / 0.3f) * Mathf.PI * 0.5f);
                outRt.anchoredPosition = Vector2.Lerp(startPos, outCenterPos, progress);
                outRt.localScale = Vector3.Lerp(initialScale, targetScale, progress);
                outText.color = new Color(1f, 0.2f, 0.2f, progress); // テキストフェードイン
                outText.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(-330, 0), new Vector2(-280, 0), progress); // 画面内に見える位置へ
                yield return null;
            }
            outRt.anchoredPosition = outCenterPos;
            outRt.localScale = targetScale;
            outText.color = new Color(1f, 0.2f, 0.2f, 1f);

            // 2. 右側に新しい牌(IN)が上空から降ってくる
            inRt.anchoredPosition = inCenterPos + new Vector2(0, 800);
            inRt.localScale = targetScale;
            t = 0;
            while(t < 0.3f)
            {
                t += Time.deltaTime;
                float progress = Mathf.Sin((t / 0.3f) * Mathf.PI * 0.5f);
                inRt.anchoredPosition = Vector2.Lerp(inCenterPos + new Vector2(0, 800), inCenterPos, progress);
                inImg.color = new Color(1, 1, 1, progress);
                inText.color = new Color(0.2f, 0.8f, 1f, progress);
                inText.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(330, 0), new Vector2(280, 0), progress); // 画面内に見える位置へ
                yield return null;
            }
            inRt.anchoredPosition = inCenterPos;
            inImg.color = new Color(1, 1, 1, 1f);
            inText.color = new Color(0.2f, 0.8f, 1f, 1f);
            
            // 少し待機（左右に並んだ状態を見せる）
            yield return new WaitForSeconds(0.6f);

            // 3. OUTは上へ消え、INは元の場所へ戻る
            t = 0;
            while(t < 0.4f)
            {
                t += Time.deltaTime;
                float progress = Mathf.Sin((t / 0.4f) * Mathf.PI * 0.5f);
                
                // OUT退場
                outRt.anchoredPosition = Vector2.Lerp(outCenterPos, outCenterPos + new Vector2(0, 800), progress);
                outImg.color = new Color(1, 1, 1, 1f - progress);
                outText.color = new Color(1f, 0.2f, 0.2f, 1f - progress);

                // IN帰還
                inRt.anchoredPosition = Vector2.Lerp(inCenterPos, startPos, progress);
                inRt.localScale = Vector3.Lerp(targetScale, initialScale, progress);
                inText.color = new Color(0.2f, 0.8f, 1f, 1f - progress);
                
                yield return null;
            }
            inRt.anchoredPosition = startPos;
            inRt.localScale = initialScale;

            // 4. 全体がフェードアウト
            t = 0;
            var canvasGroup = animContainer.AddComponent<CanvasGroup>();
            while(t < 0.2f)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1, 0, t / 0.2f);
                yield return null;
            }

            UnityEngine.Object.Destroy(animContainer);

            // アニメーション終了後にUIを差分更新し、透明化を解除する
            if (originalSlotRt != null)
            {
                var cg = originalSlotRt.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1;
            }
            
            uiManager.SetIsTransitioning(false); // ★ここで解除してRebuildを許可する
            uiManager.VisualController?.RebuildAllTilesFromState(null);
        }
    }
}
