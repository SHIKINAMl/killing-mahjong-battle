using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class GameUIVisualController
    {
        public void InitializeTileComponent(RectTransform rt, int id, bool inHand)
        {
            var visual = rt.GetComponent<TileVisual>();
            if (visual != null)
            {
                visual.SetHoverHighlight(false);
                visual.SetFuritenHighlight(false);
                visual.SetExposed(false);
                
                if (uiManager.TileResourceManager != null)
                {
                    visual.SetTile(id, uiManager.TileResourceManager.GetTileSprite(id));
                }
            }

            var interaction = rt.GetComponent<TileInteraction>();
            if (interaction == null) interaction = rt.gameObject.AddComponent<TileInteraction>();
            
            interaction.enabled = true;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            
            interaction.Initialize(id, inHand, uiManager, canvas);
        }

        private bool UpdateTileIdInUI(int oldId, int newId)
        {
            bool found = false;
            if (uiManager.WallUI != null)
            {
                foreach (var slot in uiManager.WallUI.GetWallSlots())
                {
                    if (slot == null) continue;
                    var interaction = slot.GetComponent<TileInteraction>();
                    if (interaction != null && interaction.TileId == oldId)
                    {
                        interaction.TileId = newId;
                        var visual = slot.GetComponent<TileVisual>();
                        if (visual != null && uiManager.TileResourceManager != null)
                        {
                            visual.SetTile(newId, uiManager.TileResourceManager.GetTileSprite(newId), uiManager.TileResourceManager);
                        }
                        found = true;
                        break;
                    }
                }
            }
            if (!found && uiManager.HandUI != null)
            {
                foreach (var slot in uiManager.HandUI.GetHandSlots())
                {
                    if (slot == null) continue;
                    var interaction = slot.GetComponent<TileInteraction>();
                    if (interaction != null && interaction.TileId == oldId)
                    {
                        interaction.TileId = newId;
                        var visual = slot.GetComponent<TileVisual>();
                        if (visual != null && uiManager.TileResourceManager != null)
                        {
                            visual.SetTile(newId, uiManager.TileResourceManager.GetTileSprite(newId), uiManager.TileResourceManager);
                        }
                        found = true;
                        break;
                    }
                }
            }
            return found;
        }
    }
}
