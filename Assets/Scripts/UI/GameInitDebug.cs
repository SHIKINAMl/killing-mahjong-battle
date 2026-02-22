using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public class GameInitDebug : MonoBehaviour
    {
        [SerializeField] private GameUIManager gameUIManager;
        [SerializeField] private int initialCount = 34;
        
        [Header("Debug Controls")]
        [SerializeField] private Toggle debugBettingToggle; // UI Checkbox to test betting phase
        [SerializeField] private Button triggerBettingButton; // Alternative button

        private void Start()
        {
            if (debugBettingToggle != null)
            {
                debugBettingToggle.onValueChanged.AddListener(OnDebugToggleChanged);
            }
            if (triggerBettingButton != null)
            {
                triggerBettingButton.onClick.AddListener(TestBettingPhase);
            }

            InitializeGame();
        }

        private void OnDebugToggleChanged(bool isOn)
        {
            if (isOn)
            {
                TestBettingPhase();
            }
        }

        public void TestBettingPhase()
        {
            if (gameUIManager == null) return;
            if (gameUIManager == null)
            {
                gameUIManager = FindFirstObjectByType<GameUIManager>(); // Fallback
                if (gameUIManager == null)
                {
                    Debug.LogError("GameUIManager reference missing in GameInitDebug");
                    return;
                }
            }

            // Generate dummy GameStateData matching mahjong_engine json output
            GameStateData dummyState = new GameStateData
            {
                status = 2, // 2 = Betting Phase (mock)
                round = 1,
                honba = 0,
                dora_id = Random.Range(0, 34),
                current_player = "Player1",
                players = new PlayerStateData[]
                {
                    new PlayerStateData
                    {
                        id = "Player1",
                        health = 20000,
                        hand = GenerateRandomTiles(13),
                        wall = GenerateRandomTiles(21),
                        wait = new int[0],
                        discards = new int[0]
                    },
                    new PlayerStateData
                    {
                        id = "Player2",
                        health = 20000,
                        hand = GenerateRandomTiles(13),
                        wall = GenerateRandomTiles(21),
                        wait = new int[0],
                        discards = new int[0]
                    }
                }
            };
            
            string jsonString = JsonUtility.ToJson(dummyState);
            Debug.Log($"Generated Dummy JSON from fake mahjong_engine:\n{jsonString}");

            gameUIManager.ApplyGameStateFromJSON(jsonString, "Player1");
            Debug.Log("Applied Dummy GameState to UI.");
        }

        private void InitializeGame()
        {
             // For standard Initialization with status 2 (Betting Phase) as the first phase
             if (gameUIManager == null)
            {
                gameUIManager = FindFirstObjectByType<GameUIManager>(); // Fallback
                if (gameUIManager == null)
                {
                    Debug.LogError("GameUIManager reference missing in GameInitDebug");
                    return;
                }
            }

            // Generate dummy GameStateData matching mahjong_engine json output
            GameStateData dummyState = new GameStateData
            {
                status = 2, // Start with Betting Phase
                round = 1,
                honba = 0,
                dora_id = Random.Range(0, 34),
                current_player = "Player1",
                players = new PlayerStateData[]
                {
                    new PlayerStateData
                    {
                        id = "Player1",
                        health = 20000,
                        hand = GenerateRandomTiles(13),
                        wall = GenerateRandomTiles(21),
                        wait = new int[0],
                        discards = new int[0]
                    },
                    new PlayerStateData
                    {
                        id = "Player2",
                        health = 20000,
                        hand = GenerateRandomTiles(13),
                        wall = GenerateRandomTiles(21),
                        wait = new int[0],
                        discards = new int[0]
                    }
                }
            };
            
            string jsonString = JsonUtility.ToJson(dummyState);
            gameUIManager.ApplyGameStateFromJSON(jsonString, "Player1");
        }

        private int[] GenerateRandomTiles(int count)
        {
            int[] tiles = new int[count];
            for (int i = 0; i < count; i++)
            {
                tiles[i] = Random.Range(0, 34);
            }
            return tiles;
        }
    }
}
