using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public class AbilityUI : MonoBehaviour
    {
        [Header("Ability Controls")]
        [SerializeField] private GameObject abilityButtonObject; // Reference to the 3D/2D Object
        [SerializeField] private Button activateButton; // Remains UI? Or change too? Assuming UI for now as user specified AbilityButton.
        [SerializeField] private GameObject abilityPanel; 

        private void Start()
        {
            // abilityButton listener removed as it is now a GameObject
            if (activateButton != null)
                activateButton.onClick.AddListener(OnActivateClicked);
            
            if (abilityPanel != null)
                abilityPanel.SetActive(false);
        }

        // Call this method from a click handler on the GameObject
        public void OnAbilityButtonClicked()
        {
            Debug.Log("Ability Object Clicked - Toggle Menu");
            if (abilityPanel != null)
                abilityPanel.SetActive(!abilityPanel.activeSelf);
        }

        private void OnActivateClicked()
        {
            Debug.Log("Activate Ability Clicked");
            // Logic to activate selected ability
        }
    }
}
