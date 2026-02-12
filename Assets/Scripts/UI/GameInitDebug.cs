using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class GameInitDebug : MonoBehaviour
    {
        [SerializeField] private GameUIManager gameUIManager;
        [SerializeField] private int initialCount = 34;

        private void Start()
        {
            InitializeGame();
        }

        private void InitializeGame()
        {
            if (gameUIManager == null)
            {
                gameUIManager = FindFirstObjectByType<GameUIManager>(); // Fallback
                if (gameUIManager == null)
                {
                    Debug.LogError("GameUIManager reference missing in GameInitDebug");
                    return;
                }
            }

            // Generate 34 random tiles (for now)
            List<int> initialTiles = new List<int>();
            for (int i = 0; i < initialCount; i++)
            {
                // IDs 0-33
                initialTiles.Add(Random.Range(0, 34));
            }
            
            // Calls manager to set state
            gameUIManager.InitializeGame(initialTiles);
            Debug.Log($"Initialized Game with {initialTiles.Count} tiles via Manager.");
        }
    }
}
