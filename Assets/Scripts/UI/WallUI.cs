using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class WallUI : MonoBehaviour
    {
        [Header("Wall Configuration")]
        [SerializeField] private Transform wallContainer;
        
        [Header("Layout Settings")]
        [SerializeField] private Vector2 normalContainerPos = new Vector2(0, 0);       // 通常時のコンテナ位置
        [SerializeField] private Vector2 discardContainerPos = new Vector2(0, -350);   // 打牌フェイズ時のコンテナ位置 (より手前に)
        [SerializeField] private Vector2 startPosition = new Vector2(40, 150);         // コンテナ内での牌の基点
        [SerializeField] private float tileIntervalX = 55f;
        [SerializeField] private float gapIntervalX = 80f;
        [SerializeField] private float rowIntervalY = 95f;
        [SerializeField] private float maxWidthX = 1400f; // 画面端での折り返し幅

        private GameUIManager gameUIManager;

        public void Setup(GameUIManager manager)
        {
            this.gameUIManager = manager;
        }

        private Dictionary<RectTransform, Vector2> targetPositions = new Dictionary<RectTransform, Vector2>();
        [SerializeField] private float animationSpeed = 15f;

        private void Update()
        {
            foreach (var rt in wallSlots)
            {
                if (rt == null) continue;
                if (targetPositions.TryGetValue(rt, out Vector2 targetPos))
                {
                    rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, targetPos, Time.deltaTime * animationSpeed);
                }
            }
        }

        private List<RectTransform> wallSlots = new List<RectTransform>();
        public List<RectTransform> GetWallSlots() => wallSlots;

        /// <summary>
        /// wallSlotsからtileIdが一致するRectTransformを取り出し、スロットから削除して返す
        /// </summary>
        public RectTransform GrabTileById(int tileId)
        {
            for (int i = 0; i < wallSlots.Count; i++)
            {
                if (wallSlots[i] == null) continue;
                var interaction = wallSlots[i].GetComponent<TileInteraction>();
                if (interaction != null && interaction.TileId == tileId)
                {
                    if (currentActiveInteraction == interaction) SetActiveDiscardTile(null);
                    RectTransform t = wallSlots[i];
                    wallSlots.RemoveAt(i);
                    return t;
                }
            }
            return null;
        }


        public void UpdateContainerPosition(bool isDiscardPhase)
        {
            if (wallContainer != null)
            {
                if (wallContainer is RectTransform rectTransform)
                {
                    rectTransform.anchoredPosition = isDiscardPhase ? discardContainerPos : normalContainerPos;
                }
                else
                {
                    wallContainer.localPosition = isDiscardPhase ? new Vector3(discardContainerPos.x, discardContainerPos.y, 0) 
                                                                 : new Vector3(normalContainerPos.x, normalContainerPos.y, 0);
                }
            }

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = isDiscardPhase ? 2 : 1;
            }
        }


        public void LayoutWallTiles(List<RectTransform> generatedTiles, List<int> tileIds, List<int> waitTiles, bool isDiscardPhase)
        {
            // Clear existing tracking list (but DO NOT destroy, GameUIManager manages their lifecycle)
            wallSlots.Clear();

            // コンテナ自体の位置をフェイズに応じて移動する
            UpdateContainerPosition(isDiscardPhase);

            // 1. Convert to TileData
            List<TileData> allTiles = new List<TileData>();
            foreach (var id in tileIds)
            {
                allTiles.Add(new TileData(id));
            }

            // 2. Group by Category
            var manzu = new List<TileData>();
            var pinzu = new List<TileData>();
            var souzu = new List<TileData>();
            var honors = new List<TileData>();

            foreach (var t in allTiles)
            {
                switch (t.Category)
                {
                    case TileCategory.Manzu: manzu.Add(t); break;
                    case TileCategory.Pinzu: pinzu.Add(t); break;
                    case TileCategory.Souzu: souzu.Add(t); break;
                    case TileCategory.Honor: honors.Add(t); break;
                }
            }

            // 3. 固定順で並べる: 萬子→ピンズ→索子→字牌
            var categoryLists = new List<List<TileData>> { manzu, pinzu, souzu, honors };
            categoryLists.Sort((a, b) => GetCategoryPriority(a).CompareTo(GetCategoryPriority(b)));

            // 4. Layout
            float currentY = startPosition.y;
            float currentX = startPosition.x;


            // generatedTiles を消費していくためのリスト
            List<RectTransform> remainingGeneratedTiles = new List<RectTransform>(generatedTiles);

            for (int i = 0; i < categoryLists.Count; i++)
            {
                var list = categoryLists[i];
                if (list.Count == 0) continue;

                // Sort inside category (Number順でソート。赤ドラも正しい位置に並ぶように)
                list.Sort((a, b) =>
                {
                    if (a.Number != b.Number) return a.Number.CompareTo(b.Number);
                    return a.Id.CompareTo(b.Id); // 同じNumberならencodedIdでタイブレーク
                });

                int j = 0;
                while (j < list.Count)
                {
                    int targetId = list[j].Id;
                    
                    // 描画する前にmaxWidthX を超えるかチェック
                    if (currentX > startPosition.x + maxWidthX)
                    {
                        currentX = startPosition.x;
                        currentY -= rowIntervalY; // 元の下の段に切り返す設定に戻す
                    }

                    RectTransform slot = null;

                    // 該当のTileIdを持つオブジェクトを探す
                    for (int t = 0; t < remainingGeneratedTiles.Count; t++)
                    {
                        var tInteraction = remainingGeneratedTiles[t].GetComponent<TileInteraction>();
                        if (tInteraction != null && tInteraction.TileId == targetId)
                        {
                            slot = remainingGeneratedTiles[t];
                            remainingGeneratedTiles.RemoveAt(t);
                            break;
                        }
                    }

                    if (slot == null)
                    {
                        Debug.LogWarning("WallUI: Could not find generated tile for TileId: " + targetId);
                        if (remainingGeneratedTiles.Count > 0)
                        {
                            slot = remainingGeneratedTiles[0];
                            remainingGeneratedTiles.RemoveAt(0);
                        }
                        else
                        {
                            break;
                        }
                    }
                    slot.SetParent(wallContainer, false);
                    
                    // Reset scaling and anchors properly to avoid overlap/squishing
                    slot.localScale = Vector3.one;
                    slot.anchorMin = new Vector2(0.5f, 0.5f);
                    slot.anchorMax = new Vector2(0.5f, 0.5f);
                    slot.pivot = new Vector2(0.5f, 0.5f);
                    
                    float targetXPos = currentX;
                    float targetYPos = currentY;

                    Vector2 anchoredPos = new Vector2(targetXPos, targetYPos);
                    
                    // アニメーション用に目標位置を設定。初期配置の場合は即座に設定する
                    if (!targetPositions.ContainsKey(slot))
                    {
                        slot.anchoredPosition = anchoredPos;
                    }
                    targetPositions[slot] = anchoredPos;
                    
                    slot.localRotation = Quaternion.identity;

                    var interaction = slot.GetComponent<TileInteraction>();
                    if (interaction != null)
                    {
                        // anchoredPosition（アンカー基準座標）で保存する
                        interaction.OriginalWallPosition = new Vector3(anchoredPos.x, anchoredPos.y, 0);
                    }
                    
                    var visual = slot.GetComponent<TileVisual>();
                    if (visual != null)
                    {
                        // 待ち牌リストに含まれていても赤枠を出さないように修正（ホバー時のみ赤枠にする）
                        visual.SetFuritenHighlight(false);
                    }

                    wallSlots.Add(slot);
                    slot.gameObject.SetActive(true);

                    // 同一カテゴリ内なら隙間なし、最後の牌ならカテゴリ間の隙間を追加
                    if (j < list.Count - 1)
                    {
                        currentX += tileIntervalX;
                    }
                    else if (i < categoryLists.Count - 1)
                    {
                        currentX += gapIntervalX;
                    }

                    j++;
                }
            }

            // 安全網: 配置されずに余った generatedTiles があれば、確実にプールに返す（幽霊オブジェクトによる西増殖バグ防止）
            if (remainingGeneratedTiles.Count > 0)
            {
                var poolManager = UnityEngine.Object.FindFirstObjectByType<TilePoolManager>();
                if (poolManager != null)
                {
                    foreach (var leftover in remainingGeneratedTiles)
                    {
                        if (leftover != null) poolManager.ReturnTileToPool(leftover.gameObject);
                    }
                }
                else
                {
                    foreach (var leftover in remainingGeneratedTiles)
                    {
                        if (leftover != null) Destroy(leftover.gameObject);
                    }
                }
            }
        }

        private int GetCategoryPriority(List<TileData> list)
        {
            if (list.Count == 0) return 99;
            var cat = list[0].Category;
            switch (cat)
            {
                case TileCategory.Souzu: return 1;
                case TileCategory.Manzu: return 2;
                case TileCategory.Pinzu: return 3;
                case TileCategory.Honor: return 4;
                default: return 99;
            }
        }

        public RectTransform GrabTile(int tileId)
        {
            // WallSlotの中から該当IDを持つTransformを探し出して返す
            for (int i = 0; i < wallSlots.Count; i++)
            {
                if (wallSlots[i] == null) continue;
                var interaction = wallSlots[i].GetComponent<TileInteraction>();
                if (interaction != null && interaction.TileId == tileId)
                {
                    if (currentActiveInteraction == interaction) SetActiveDiscardTile(null);
                    RectTransform t = wallSlots[i];
                    wallSlots.RemoveAt(i);
                    return t; 
                }
            }
            return null;
        }

        public void ReturnTileToWall(RectTransform tileTransform, int tileId)
        {
            if (tileTransform == null) return;

            // 戻す（リストに追加して、親を設定）
            wallSlots.Add(tileTransform);
            tileTransform.SetParent(wallContainer, false);
            
            // Interactionリセットと初期座標の復元
            var interaction = tileTransform.GetComponent<TileInteraction>();
            if (interaction != null)
            {
                if (gameUIManager != null)
                {
                    Canvas canvas = GetComponentInParent<Canvas>();
                    interaction.Initialize(tileId, false, gameUIManager, canvas);
                }
                
                // アンカーとピボットをLayoutWallTilesと同じ基準（中心 0.5）に戻す
                tileTransform.anchorMin = new Vector2(0.5f, 0.5f);
                tileTransform.anchorMax = new Vector2(0.5f, 0.5f);
                tileTransform.pivot = new Vector2(0.5f, 0.5f);
                tileTransform.localScale = Vector3.one;
                tileTransform.localRotation = Quaternion.identity;
                
                // OriginalWallPositionを目標座標（targetPositions）に設定し、UpdateのLerpでアニメーションさせる
                Vector2 originalPos = new Vector2(interaction.OriginalWallPosition.x, interaction.OriginalWallPosition.y);
                targetPositions[tileTransform] = originalPos;
            }
        }

        public void UpdateWallHighlights(List<int> waitTiles, bool isDiscardPhase)
        {
            List<int> waitBaseIds = new List<int>();
            if (isDiscardPhase && waitTiles != null)
            {
                foreach (int waitId in waitTiles)
                {
                    waitBaseIds.Add(waitId & 0x1F);
                }
            }

            foreach (var slot in wallSlots)
            {
                if (slot == null) continue;
                var visual = slot.GetComponent<TileVisual>();
                var interaction = slot.GetComponent<TileInteraction>();
                if (visual != null && interaction != null)
                {
                    bool isFuritenAlert = waitBaseIds.Contains(interaction.TileId & 0x1F);
                    visual.SetFuritenHighlight(isFuritenAlert);
                }
            }
        }

        private RectTransform arrowIndicator;
        private TileInteraction currentActiveInteraction = null;

        public void SetActiveDiscardTile(TileInteraction interaction)
        {
            // 古いものを元に戻す
            if (currentActiveInteraction != null)
            {
                var oldSlot = currentActiveInteraction.GetComponent<RectTransform>();
                if (oldSlot != null)
                {
                    oldSlot.anchoredPosition = new Vector2(currentActiveInteraction.OriginalWallPosition.x, currentActiveInteraction.OriginalWallPosition.y);
                }
            }

            currentActiveInteraction = interaction;

            // 新しいものを持ち上げて矢印をつける
            if (currentActiveInteraction != null)
            {
                var newSlot = currentActiveInteraction.GetComponent<RectTransform>();
                if (newSlot != null)
                {
                    newSlot.anchoredPosition = new Vector2(currentActiveInteraction.OriginalWallPosition.x, currentActiveInteraction.OriginalWallPosition.y + 8f);

                    if (arrowIndicator == null) CreateArrowIndicator();
                    arrowIndicator.gameObject.SetActive(true);
                    // 矢印は常にwallContainerの子として保持し、牌スロットへの依存を断ちプール汚染を防ぐ
                    Transform arrowParent = wallContainer != null ? wallContainer : (Transform)this.transform;
                    arrowIndicator.SetParent(arrowParent, false);
                    arrowIndicator.anchoredPosition = new Vector2(
                        currentActiveInteraction.OriginalWallPosition.x,
                        currentActiveInteraction.OriginalWallPosition.y + 8f + 25f
                    );
                }
            }
            else
            {
                if (arrowIndicator != null) arrowIndicator.gameObject.SetActive(false);
            }
        }

        public void ClearActiveDiscardTile(TileInteraction interaction)
        {
            if (currentActiveInteraction == interaction)
            {
                SetActiveDiscardTile(null);
            }
        }

        public void UpdateDiscardTurnIndicator(bool isLocalTurn, bool isDiscardPhase)
        {
            if (isDiscardPhase && isLocalTurn)
            {
                // 現在の有効な牌がなければ、壁の最初の牌をターゲットにする
                if (currentActiveInteraction == null && wallSlots.Count > 0)
                {
                    var inter = wallSlots[0].GetComponent<TileInteraction>();
                    if (inter != null) SetActiveDiscardTile(inter);
                }
                else if (currentActiveInteraction != null)
                {
                    // 既にセットされていれば再適用
                    SetActiveDiscardTile(currentActiveInteraction);
                }
            }
            else
            {
                SetActiveDiscardTile(null);
            }
        }

        private void CreateArrowIndicator()
        {
            GameObject arrowObj = new GameObject("DiscardArrowIndicator");
            arrowIndicator = arrowObj.AddComponent<RectTransform>();
            arrowIndicator.sizeDelta = new Vector2(50, 50);

            var text = arrowObj.AddComponent<UnityEngine.UI.Text>();
            text.text = "▼";
            text.fontSize = (int)KillingMahjong.Common.UITypography.BodyLarge;
            text.color = Color.red;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var outline = arrowObj.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, -2);

            var canvas = arrowObj.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 16;

            // 生成直後にwallContainerの子として登録することで、牌のプール汚染を防ぐ
            Transform parent = wallContainer != null ? wallContainer : (Transform)this.transform;
            arrowIndicator.SetParent(parent, false);
            arrowIndicator.gameObject.SetActive(false);
        }
    }
}
