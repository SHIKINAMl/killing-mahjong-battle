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
            
            handUI.SetHand(randomIds);
        }
        
        // Add a UI Button trigger if needed
        // [SerializeField] private Button debugButton;
        // ...
    }
}
