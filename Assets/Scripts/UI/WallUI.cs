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
        [SerializeField] private Vector2 discardContainerPos = new Vector2(0, -100);   // 打牌フェイズ時のコンテナ位置
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

        private List<RectTransform> wallSlots = new List<RectTransform>();
        public List<RectTransform> GetWallSlots() => wallSlots;

        public void LayoutWallTiles(List<RectTransform> generatedTiles, List<int> tileIds, List<int> waitTiles, bool isDiscardPhase)
        {
            // Clear existing tracking list (but DO NOT destroy, GameUIManager manages their lifecycle)
            wallSlots.Clear();

            // コンテナ自体の位置をフェイズに応じて移動する
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

            // 3. Sort Categories logic (Count DESC -> Souzu > Manzu > Pinzu > Honor)
            var categoryLists = new List<List<TileData>> { souzu, manzu, pinzu, honors };
            
            categoryLists.Sort((a, b) =>
            {
                int countCompare = b.Count.CompareTo(a.Count);
                if (countCompare != 0) return countCompare;

                int priorityA = GetCategoryPriority(a);
                int priorityB = GetCategoryPriority(b);
                return priorityA.CompareTo(priorityB);
            });

            // 4. Layout
            float currentY = startPosition.y;
            float currentX = startPosition.x;

            int currentSlot = 0;
            int maxSlotsPerRow = 20;
            int tileIndex = 0; // generatedTilesを上から順に消費するためのインデックス

            for (int i = 0; i < categoryLists.Count; i++)
            {
                var list = categoryLists[i];
                if (list.Count == 0) continue;

                // Sort inside category
                list.Sort((a, b) => a.Id.CompareTo(b.Id));

                int j = 0;
                while (j < list.Count)
                {
                    // Detect Structure
                    bool isKoutsu = false;
                    bool isShuntsu = false;
                    
                    if (j + 2 < list.Count)
                    {
                        if (list[j].Id == list[j+1].Id && list[j].Id == list[j+2].Id)
                            isKoutsu = true;
                    }

                    if (!isKoutsu && j + 2 < list.Count) 
                    {
                        if (list[j].Category != TileCategory.Honor &&
                            list[j].Category == list[j+1].Category && 
                            list[j].Category == list[j+2].Category)
                        {
                            if (list[j].Number + 1 == list[j+1].Number && 
                                list[j].Number + 2 == list[j+2].Number)
                                isShuntsu = true;
                        }
                    }

                    int groupSize = (isKoutsu || isShuntsu) ? 3 : 1;
                    
                    if (isDiscardPhase)
                    {
                        // 新レイアウト: 1列は空きスペース含めて最大20牌まで
                        int currentRow = currentSlot / maxSlotsPerRow;
                        if ((currentSlot % maxSlotsPerRow) + groupSize > maxSlotsPerRow)
                        {
                            currentSlot = (currentRow + 1) * maxSlotsPerRow; // 改行
                        }
                    }
                    else
                    {
                        // 旧レイアウト: 描画する前に、このグループを描画したら maxWidthX を超えるかチェック
                        float expectedWidth = (groupSize - 1) * tileIntervalX;
                        if (currentX + expectedWidth > startPosition.x + maxWidthX)
                        {
                            currentX = startPosition.x;
                            currentY -= rowIntervalY;
                        }
                    }

                    // Render current group
                    for (int k = 0; k < groupSize; k++)
                    {
                        if (tileIndex >= generatedTiles.Count)
                        {
                            Debug.LogWarning("WallUI: Not enough generated tiles provided for layout.");
                            break;
                        }

                        RectTransform slot = generatedTiles[tileIndex++];
                        slot.SetParent(wallContainer, false);
                        
                        // Reset scaling and anchors properly to avoid overlap/squishing
                        slot.localScale = Vector3.one;
                        slot.anchorMin = new Vector2(0.5f, 0.5f);
                        slot.anchorMax = new Vector2(0.5f, 0.5f);
                        slot.pivot = new Vector2(0.5f, 0.5f);
                        
                        float targetX = startPosition.x;
                        float targetY = startPosition.y;

                        if (isDiscardPhase)
                        {
                            int r = currentSlot / maxSlotsPerRow;
                            int c = currentSlot % maxSlotsPerRow;
                            targetX = startPosition.x + c * tileIntervalX;
                            targetY = startPosition.y - r * rowIntervalY; // マイナスで下に配置する
                        }
                        else
                        {
                            targetX = currentX;
                            targetY = currentY;
                        }

                        Vector3 finalPos = new Vector3(targetX, targetY, 0);
                        slot.localPosition = finalPos;
                        slot.localRotation = Quaternion.identity;

                        var interaction = slot.GetComponent<TileInteraction>();
                        if (interaction != null)
                        {
                            interaction.OriginalWallPosition = finalPos;
                        }
                        
                        var visual = slot.GetComponent<TileVisual>();
                        if (visual != null && waitTiles != null)
                        {
                            // 待ち牌リストに含まれていれば、振聴アラート（赤枠）を出す (Discard phaseのみ)
                            visual.SetFuritenHighlight(isDiscardPhase && waitTiles.Contains(list[j + k].Id));
                        }

                        wallSlots.Add(slot);
                        slot.gameObject.SetActive(true);

                        if (isDiscardPhase)
                        {
                            currentSlot++;
                        }
                        else
                        {
                            if (k < groupSize - 1) currentX += tileIntervalX;
                        }
                    }

                    // グループ間やカテゴリ間のギャップ処理
                    if (j + groupSize < list.Count)
                    {
                        var lastTile = list[j + groupSize - 1];
                        var nextTile = list[j + groupSize];
                        
                        bool needGap = false;
                        if (isKoutsu || isShuntsu) needGap = true;
                        else if (lastTile.Category == nextTile.Category && lastTile.Category != TileCategory.Honor)
                        {
                            if (nextTile.Number - lastTile.Number > 1) needGap = true;
                        }

                        if (isDiscardPhase)
                        {
                            if (needGap) currentSlot++;
                        }
                        else
                        {
                            if (!isDiscardPhase) // currentX update is the last tile iteration above for intra-group
                            {
                                currentX += (needGap ? gapIntervalX : tileIntervalX);
                            }
                        }
                    }
                    else if (i < categoryLists.Count - 1)
                    {
                        if (isDiscardPhase) currentSlot++;
                        else currentX += gapIntervalX;
                    }

                    j += groupSize;
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
                
                // 本来の壁の座標に戻す
                tileTransform.localPosition = interaction.OriginalWallPosition;
            }
        }
    }
}
