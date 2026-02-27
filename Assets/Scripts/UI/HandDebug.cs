using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class HandDebug : MonoBehaviour
    {
        [SerializeField] private HandUI handUI;
        [SerializeField] private int tileCount = 20;

        private void Start()
        {
            // Optional: Auto-generate on start
            GenerateRandomHand();
        }

        public void GenerateRandomHand()
        {
            if (handUI == null) return;

            List<int> randomIds = new List<int>();
            for (int i = 0; i < tileCount; i++)
            {
                // IDs 0-28
                randomIds.Add(Random.Range(0, 29));
            }
            
            // handUI.SetHand(randomIds);
            
            // HandUIのSetHandがなくなり、AddTileToHandになったため、自分でダミー生成して渡します。
            // 注: ここは本当にデバッグ用だけのコードです。本来はGameUIManager経由で実体Prefabを持ちます。
            GameObject tempPrefab = new GameObject("DebugTile");
            tempPrefab.AddComponent<RectTransform>();
            tempPrefab.AddComponent<TileInteraction>();
            
            foreach(var id in randomIds)
            {
                var clone = Instantiate(tempPrefab).transform as RectTransform;
                if (clone != null) {
                    handUI.AddTileToHand(clone, id);
                }
            }
            Destroy(tempPrefab);
        }
        
        // Add a UI Button trigger if needed
        // [SerializeField] private Button debugButton;
        // ...
    }
}
