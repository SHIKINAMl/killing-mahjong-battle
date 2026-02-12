using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class WallUI : MonoBehaviour
    {
        [Header("Wall Configuration")]
        [SerializeField] private Transform wallContainer;
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private TileResourceManager tileResourceManager;
        
        [Header("Layout Settings")]
        [SerializeField] private Vector2 startPosition = new Vector2(40, 150);
        [SerializeField] private float tileIntervalX = 55f;
        [SerializeField] private float gapIntervalX = 80f;
        [SerializeField] private float rowIntervalY = 95f;

        private GameUIManager gameUIManager;

        public void Setup(GameUIManager manager)
        {
            this.gameUIManager = manager;
        }

        private List<Transform> wallSlots = new List<Transform>();

        public void SetWall(List<int> tileIds)
        {
            // Clear existing
            foreach (Transform t in wallSlots)
            {
                if (t != null) Destroy(t.gameObject);
            }
            wallSlots.Clear();

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
            for (int i = 0; i < categoryLists.Count; i++)
            {
                var list = categoryLists[i];
                if (list.Count == 0) continue;

                // Sort inside category
                list.Sort((a, b) => a.Id.CompareTo(b.Id));

                float currentX = startPosition.x;
                float currentY = startPosition.y - (i * rowIntervalY);

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
                    
                    // Render current group
                    for (int k = 0; k < groupSize; k++)
                    {
                        int currentIndex = j + k;
                        var tile = list[currentIndex];

                        // Instantiate Tile
                        GameObject obj = Instantiate(tilePrefab, wallContainer);
                        Transform slot = obj.transform;
                        
                        // Position
                        slot.localPosition = new Vector3(currentX, currentY, 0);
                        slot.localRotation = Quaternion.identity;

                        // Visual
                        if (tileResourceManager != null)
                        {
                            var visual = slot.GetComponent<TileVisual>();
                            if (visual != null) visual.SetTile(tile.Id, tileResourceManager.GetTileSprite(tile.Id));
                        }

                        // Interaction
                        var interaction = slot.GetComponent<TileInteraction>();
                        if (interaction == null) interaction = slot.gameObject.AddComponent<TileInteraction>();
                        Canvas canvas = GetComponentInParent<Canvas>();
                        if (gameUIManager != null) interaction.Initialize(tile.Id, false, gameUIManager, canvas);
                        
                        wallSlots.Add(slot);
                        
                        // Advance X Logic
                        if (k < groupSize - 1)
                        {
                            currentX += tileIntervalX;
                        }
                        else
                        {
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

                                currentX += (needGap ? gapIntervalX : tileIntervalX);
                            }
                        }
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

        // Logic to remove a tile when discarded?
        public void RemoveTile(int index)
        {
            if (index >= 0 && index < wallSlots.Count)
            {
                Destroy(wallSlots[index].gameObject);
                wallSlots.RemoveAt(index);
                // Re-layout? For now, just leaving a gap or simpler to not re-layout.
                // In real game, wall shrinks? 
                // "お互いにその21個から順番に捨てる" -> Usually specific order.
                // If specific order (left to right), we define which one is next.
            }
        }
    }
}
