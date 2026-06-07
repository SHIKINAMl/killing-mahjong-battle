using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class UIRaycastDebugger : MonoBehaviour
    {
        void Update()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                PointerEventData pointerData = new PointerEventData(EventSystem.current)
                {
                    position = mouse.position.ReadValue()
                };

                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);

                if (results.Count > 0)
                {
                    string log = "[UIRaycastDebugger] Clicked UI Elements:\n";
                    foreach (var result in results)
                    {
                        log += $"- {result.gameObject.name} (Parent: {result.gameObject.transform.parent?.name})\n";
                    }
                    Debug.LogWarning(log);
                }
                else
                {
                    Debug.Log("[UIRaycastDebugger] No UI element hit.");
                }
            }
        }
    }
}
