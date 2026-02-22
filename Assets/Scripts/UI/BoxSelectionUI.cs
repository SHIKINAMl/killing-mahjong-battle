using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class BoxSelectionUI : MonoBehaviour
    {
        [SerializeField] private RectTransform selectionBox;
        [SerializeField] private GameUIManager gameUIManager;
        [SerializeField] private Canvas canvas;

        private Vector2 startPosition;
        private bool isSelecting = false;

        private void Start()
        {
            if (selectionBox != null) selectionBox.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Mouse.current == null) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // Start selection if we NOT clicking on a Tile or Button
                Vector2 mousePos = Mouse.current.position.ReadValue();
                if (!IsPointerOverInteractive(mousePos))
                {
                    isSelecting = true;
                    startPosition = mousePos;
                    if (selectionBox != null)
                    {
                        selectionBox.gameObject.SetActive(true);
                        UpdateSelectionBox(mousePos);
                    }
                }
            }

            if (isSelecting && Mouse.current.leftButton.isPressed)
            {
                UpdateSelectionBox(Mouse.current.position.ReadValue());
            }

            if (isSelecting && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                isSelecting = false;
                PerformSelection();
                if (selectionBox != null) selectionBox.gameObject.SetActive(false);
            }
        }

        private bool IsPointerOverInteractive(Vector2 mousePos)
        {
            if (EventSystem.current == null) return false;

            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = mousePos
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
                // Check if we hit a TileInteraction
                if (result.gameObject.GetComponentInParent<TileInteraction>() != null) return true;
                // Check if we hit a Button
                if (result.gameObject.GetComponentInParent<Button>() != null) return true;
            }

            return false;
        }
        
        private void UpdateSelectionBox(Vector2 currentPosition)
        {
            float width = currentPosition.x - startPosition.x;
            float height = currentPosition.y - startPosition.y;

            selectionBox.sizeDelta = new Vector2(Mathf.Abs(width), Mathf.Abs(height));
            
            // Pivot is center? Or corner? Assuming standard anchors
            // Let's set position to center
            Vector2 center = startPosition + new Vector2(width / 2, height / 2);
            
            // Convert to Local if needed, but if under same canvas, screen pos might work or need conversion
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(selectionBox.parent as RectTransform, center, canvas.worldCamera, out localPoint);
            selectionBox.localPosition = localPoint;
        }

        private void PerformSelection()
        {
            var tiles = FindObjectsByType<TileInteraction>(FindObjectsSortMode.None);
            List<int> selectedIds = new List<int>();

            // Calculate World Rect of selection box
            Vector3[] corners = new Vector3[4];
            selectionBox.GetWorldCorners(corners);
            // corners: 0=BL, 1=TL, 2=TR, 3=BR
            
            Rect worldRect = new Rect(corners[0], corners[2] - corners[0]); // Simple min/max check better?
            Vector2 min = Vector2.Min(corners[0], corners[2]);
            Vector2 max = Vector2.Max(corners[0], corners[2]);
            Rect selectionWorldRect = new Rect(min, max - min);

            foreach (var tile in tiles)
            {
                // Check if tile center is inside rect
                if (selectionWorldRect.Contains(tile.transform.position))
                {
                    selectedIds.Add(tile.TileId);
                }
            }

            if (gameUIManager != null)
            {
                gameUIManager.SelectTiles(selectedIds);
            }
        }
    }
}
