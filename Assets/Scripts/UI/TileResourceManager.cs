using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    [CreateAssetMenu(fileName = "TileResourceManager", menuName = "Mahjong/TileResourceManager")]
    public class TileResourceManager : ScriptableObject
    {
        [Header("Tile Sprites (Order: Manzu 1-9, Pinzu 1-9, Souzu 1-9, Honors: East, West)")]
        [Tooltip("Ensure exactly 29 sprites are assigned in standard order.")]
        [SerializeField] private List<Sprite> tileSprites;

        [Header("Prefabs")]
        [SerializeField] private GameObject tilePrefab;

        public Sprite GetTileSprite(int id)
        {
            // ID mapping:
            // 0-8: Manzu 1-9
            // 9-17: Pinzu 1-9
            // 18-26: Souzu 1-9
            // 27-28: Honors (East, West)
            
            if (id < 0 || id >= tileSprites.Count)
            {
                Debug.LogWarning($"Tile ID {id} is out of range for Sprite list.");
                return null;
            }
            return tileSprites[id];
        }

        public GameObject GetTilePrefab()
        {
            return tilePrefab;
        }
    }
}
