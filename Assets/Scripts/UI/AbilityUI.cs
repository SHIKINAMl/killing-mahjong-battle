using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public class AbilityUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button triggerButton; // The Red Button on the right
        [SerializeField] private RectTransform abilityWindow; // The window on the left
        [SerializeField] private Vector2 hiddenPosition = new Vector2(-500, -500); // Off-screen or hidden
        [SerializeField] private Vector2 showPosition = new Vector2(100, 100); // On-screen
        [SerializeField] private float animationDuration = 0.5f;

        [Header("Window Components")]
        [SerializeField] private AbilityItemUI itemPrefab;
        [SerializeField] private Transform contentContainer;
        [SerializeField] private Button closeButton; // The ▼ button
        [SerializeField] private Button activateButton; // The Activate button
        
        [Header("Layout Settings")]
        [SerializeField] private float itemHeight = 100f;
        [SerializeField] private float itemSpacing = 5f;

        // Internal State
        private bool isWindowVisible = false;
        private Coroutine currentAnimationCoroutine;
        private System.Collections.Generic.List<AbilityItemUI> instantiatedItems = new System.Collections.Generic.List<AbilityItemUI>();
        private AbilityItemUI currentSelection;

        // Mock Data Class
        [System.Serializable]
        public class AbilityData
        {
            public string name;
            public int cost;
            public string description;
        }
        [SerializeField] private System.Collections.Generic.List<AbilityData> mockAbilities;

        private void Start()
        {
            if (triggerButton != null)
                triggerButton.onClick.AddListener(OnTriggerClicked);
            
            if (closeButton != null)
                closeButton.onClick.AddListener(CloseWindow);

            if (activateButton != null)
                activateButton.onClick.AddListener(OnActivateClicked);

            if (abilityWindow != null)
                abilityWindow.anchoredPosition = hiddenPosition;

            // Populate List
            PopulateList();
        }

        private void PopulateList()
        {
            if (itemPrefab == null || contentContainer == null) return;

            // clear existing
            foreach(Transform child in contentContainer) Destroy(child.gameObject);
            instantiatedItems.Clear();

            float currentY = 0;
            for (int i = 0; i < mockAbilities.Count; i++)
            {
                var data = mockAbilities[i];
                var itemObj = Instantiate(itemPrefab, contentContainer);
                itemObj.Setup(this, i, data.name, data.cost, data.description);
                
                // Manual Layout
                RectTransform rt = itemObj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // Force Top-Stretch Layout
                    rt.localRotation = Quaternion.identity;
                    rt.localScale = Vector3.one;

                    // Set Anchors to Top-Stretch (Min: 0,1 | Max: 1,1)
                    rt.anchorMin = new Vector2(0, 1);
                    rt.anchorMax = new Vector2(1, 1);
                    rt.pivot = new Vector2(0.5f, 1);

                    // Set SizeDelta (X=0 means stretch to fill width, Y=Height)
                    rt.sizeDelta = new Vector2(0, itemHeight);

                    // Set Position (Top goes down)
                    rt.anchoredPosition = new Vector2(0, -currentY);
                }

                instantiatedItems.Add(itemObj);
                currentY += itemHeight + itemSpacing;
            }

            // Resize Container
            RectTransform contentRect = contentContainer.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                 contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, currentY);
            }
        }

        private void OnTriggerClicked()
        {
            ToggleAbilityWindow();
            CancelOpponentReady();
        }

        public void CloseWindow()
        {
            if (isWindowVisible) ToggleAbilityWindow();
        }

        public void ToggleAbilityWindow()
        {
            isWindowVisible = !isWindowVisible;
            if (!isWindowVisible) DeselectAll(); // Deselect on close

            if (currentAnimationCoroutine != null) StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = StartCoroutine(AnimateWindow(isWindowVisible ? showPosition : hiddenPosition));
        }

        public void OnAbilitySelected(AbilityItemUI item)
        {
            if (currentSelection != null) currentSelection.Deselect();
            
            if (currentSelection == item)
            {
                // Clicked same item -> Deselect?? Or keep? User said: "Other ability click... or tile click... or close... deselects"
                // Usually clicking same keeps it selected.
                currentSelection = item;
                currentSelection.Select();
            }
            else
            {
                currentSelection = item;
                currentSelection.Select();
            }
        }

        public void DeselectAll()
        {
            if (currentSelection != null)
            {
                currentSelection.Deselect();
                currentSelection = null;
            }
        }

        private void OnActivateClicked()
        {
            if (currentSelection != null)
            {
                int index = currentSelection.AbilityIndex;
                if (index >= 0 && index < mockAbilities.Count)
                {
                    var data = mockAbilities[index];
                    Debug.Log($"Activting Ability: {data.name} (Cost: {data.cost})");
                    // TODO: Deduct cost, Apply effect
                }
                DeselectAll();
                // Optionally close window? User said "Activate and deselect". Didn't say close.
            }
            else
            {
                Debug.Log("No ability selected.");
            }
        }

        private System.Collections.IEnumerator AnimateWindow(Vector2 targetPos)
        {
            if (abilityWindow == null) yield break;

            Vector2 startPos = abilityWindow.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                t = t * t * (3f - 2f * t); 

                abilityWindow.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }
            abilityWindow.anchoredPosition = targetPos;
        }

        private void CancelOpponentReady()
        {
            Debug.Log("Ability Triggered: Opponent's Ready State Cancelled!");
        }
    }
}
