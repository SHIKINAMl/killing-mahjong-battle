using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class RiverUI : MonoBehaviour
    {
        [Header("River Configuration")]
        [SerializeField] private Transform riverContainer;
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private TileResourceManager tileResourceManager;

        [Header("Layout Settings")]
        [SerializeField] private float tileWidth = 80.0f;
        [SerializeField] private float tileDepth = 100.0f;
        [SerializeField] private int maxPerRow = 6;

        private List<Transform> discardedTiles = new List<Transform>();

        public void AddTile(int tileId)
        {
            if (tilePrefab == null || riverContainer == null) return;

            GameObject obj = Instantiate(tilePrefab, riverContainer);
            
            // Layout Logic (6 tiles per row)
            int index = discardedTiles.Count;
            int row = index / maxPerRow;
            int col = index % maxPerRow;

            float x = col * tileWidth;
            float z = -row * tileDepth; // Go towards camera or away? Usually River goes down/out.
            // Let's assume negative Z is "closer" to camera if Top-Down, or "down" the table.
            
            obj.transform.localPosition = new Vector3(x, 0, z);
            obj.transform.localRotation = Quaternion.identity;

            // Visual
            TileVisual visual = obj.GetComponent<TileVisual>();
            if (visual != null && tileResourceManager != null)
            {
                visual.SetTile(tileId, tileResourceManager.GetTileSprite(tileId));
            }

            discardedTiles.Add(obj.transform);
        }

        public void Clear()
        {
            foreach (var t in discardedTiles)
            {
                if (t != null) Destroy(t.gameObject);
            }
            discardedTiles.Clear();
        }

        public void SetRiver(List<int> tileIds)
        {
            Clear();
            if (tileIds != null)
            {
                foreach(int tileId in tileIds)
                {
                    AddTile(tileId);
                }
            }
        }
    }
}
