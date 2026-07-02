using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    public class TilePoolManager : MonoBehaviour
    {
        [Header("Settings")]
        public int initialPoolSize = 136;
        public bool showDebugVisuals = false;

        private List<Transform> _poolSlots = new List<Transform>();
        private bool _isInitialized = false;
        [SerializeField] private Transform _container;

        public void InitializePool(GameUIManager uiManager)
        {
            if (_isInitialized) return;

            if (_container == null)
            {
                Debug.LogError("[TilePoolManager] _container is not assigned in the inspector!");
                return;
            }

            var containerObj = _container.gameObject;

            var canvas = containerObj.GetComponent<Canvas>();
            if (canvas == null) canvas = containerObj.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 999;

            var rt = containerObj.GetComponent<RectTransform>();
            if (rt == null) rt = containerObj.AddComponent<RectTransform>();

            var img = containerObj.GetComponent<Image>();
            if (img == null) img = containerObj.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.5f);
            img.raycastTarget = false;

            var grid = containerObj.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = containerObj.AddComponent<GridLayoutGroup>();

            if (uiManager != null && uiManager.TilePrefab != null)
            {
                var prefabRt = uiManager.TilePrefab.GetComponent<RectTransform>();
                if (prefabRt != null)
                {
                    grid.cellSize = prefabRt.rect.size;
                }
                else
                {
                    grid.cellSize = new Vector2(80, 120); // Fallback
                }
            }
            else
            {
                grid.cellSize = new Vector2(80, 120);
            }

            grid.spacing = new Vector2(5, 5);

            var cg = containerObj.GetComponent<CanvasGroup>();
            if (cg == null) cg = containerObj.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            
            cg.alpha = showDebugVisuals ? 1f : 0f;

            if (uiManager != null && uiManager.TilePrefab != null)
            {
                List<int> deck = GenerateDeck();
                for (int i = 0; i < deck.Count; i++)
                {
                    int tileId = deck[i];
                    
                    GameObject slotObj = new GameObject($"Slot_{i}");
                    var slotRt = slotObj.AddComponent<RectTransform>();
                    slotRt.SetParent(_container, false);
                    slotRt.sizeDelta = grid.cellSize;
                    _poolSlots.Add(slotRt);

                    GameObject obj = Instantiate(uiManager.TilePrefab, slotRt);
                    
                    var visual = obj.GetComponent<TileVisual>();
                    if (visual != null && uiManager.TileResourceManager != null)
                    {
                        visual.SetTile(tileId, uiManager.TileResourceManager.GetTileSprite(tileId));
                    }
                    
                    var interaction = obj.GetComponent<TileInteraction>();
                    if (interaction == null) interaction = obj.AddComponent<TileInteraction>();
                    interaction.Initialize(tileId, false, uiManager, canvas);

                    obj.SetActive(showDebugVisuals);
                }
            }
            else
            {
                Debug.LogWarning("[TilePoolManager] TilePrefab is missing. Cannot pre-allocate tiles.");
            }

            _isInitialized = true;
        }

        private List<int> GenerateDeck()
        {
            List<int> deck = new List<int>();
            for (int i = 0; i < 34; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    int id = i;
                    if (j == 0 && (i == 4 || i == 13 || i == 22))
                    {
                        id |= 0x40; // 赤ドラフラグ
                    }
                    deck.Add(id);
                }
            }
            return deck;
        }

        public GameObject GetTileById(int encodedId, Transform parent, GameUIManager uiManager)
        {
            if (!_isInitialized)
            {
                InitializePool(uiManager);
            }

            for (int i = 0; i < _poolSlots.Count; i++)
            {
                Transform slot = _poolSlots[i];
                if (slot.childCount > 0)
                {
                    Transform tileTransform = slot.GetChild(0);
                    var interaction = tileTransform.GetComponent<TileInteraction>();
                    if (interaction != null && interaction.TileId == encodedId)
                    {
                        GameObject obj = tileTransform.gameObject;
                        obj.transform.SetParent(parent, false);
                        obj.SetActive(true);
                        
                        var cg = obj.GetComponent<CanvasGroup>();
                        if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = true; }
                        obj.transform.localScale = Vector3.one;

                        var interactions = obj.GetComponentsInChildren<TileInteraction>(true);
                        foreach (var inter in interactions) inter.enabled = true;

                        var images = obj.GetComponentsInChildren<UnityEngine.UI.Image>(true);
                        foreach (var img in images) { img.raycastTarget = true; }

                        return obj;
                    }
                }
            }

            Debug.LogWarning($"[TilePoolManager] Could not find tile with ID {encodedId} in pool! Instantiating fallback.");
            
            GameObject fallbackObj;
            if (uiManager != null && uiManager.TilePrefab != null)
            {
                fallbackObj = Instantiate(uiManager.TilePrefab, parent);
                fallbackObj.SetActive(true);
            }
            else
            {
                fallbackObj = new GameObject("DummyTile", typeof(RectTransform));
                fallbackObj.transform.SetParent(parent, false);
                fallbackObj.SetActive(true);
            }
            return fallbackObj;
        }

        public void ReturnTileToPool(GameObject obj)
        {
            if (obj == null) return;
            
            foreach (var s in _poolSlots)
            {
                if (obj.transform.parent == s) return;
            }

            var interaction = obj.GetComponent<TileInteraction>();
            int tileId = interaction != null ? interaction.TileId : -1;

            Transform targetSlot = null;
            // 本来はTileIdに合致するスロットを探すべきだが、簡単な実装として空いているスロットの若い順に戻す
            for (int i = 0; i < _poolSlots.Count; i++)
            {
                if (_poolSlots[i].childCount == 0)
                {
                    targetSlot = _poolSlots[i];
                    break;
                }
            }

            if (targetSlot != null)
            {
                obj.transform.SetParent(targetSlot, false);
                
                var visual = obj.GetComponent<TileVisual>();
                var uiManager = FindFirstObjectByType<GameUIManager>();
                if (visual != null && uiManager != null && uiManager.TileResourceManager != null && tileId != -1)
                {
                    visual.SetTile(tileId, uiManager.TileResourceManager.GetTileSprite(tileId));
                }
                
                obj.SetActive(showDebugVisuals);
            }
            else
            {
                Destroy(obj);
            }
        }
    }
}
