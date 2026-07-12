using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public class EnemyHandUI : HandBaseUI
    {
        // 敵の本来の牌IDを記録する（見た目は0のダミーでも、後々の公開イベントで使うため）
        private List<int> realTileIds = new List<int>();
        
        public List<int> GetRealTileIds() => realTileIds;

        public void AddEnemyTile(RectTransform tileTransform, int visualId, int realId)
        {
            // まずは共通のUI追加処理を呼び出す
            base.AddTileToHand(tileTransform, visualId);
            
            // プレイヤーと同じ操作用のInteractionが残らないよう無効化
            var interactions = tileTransform.GetComponentsInChildren<TileInteraction>(true);
            foreach(var interaction in interactions)
            {
                interaction.enabled = false;
            }

            // 万が一ホバー状態が残っていた場合に備えて強制リセット
            var visual = tileTransform.GetComponent<TileVisual>();
            if (visual != null) visual.SetHoverHighlight(false);

            // マウスカーソルに一切反応しないようにレイキャストを遮断する
            var images = tileTransform.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            foreach(var img in images)
            {
                img.raycastTarget = false;
            }

            // クリック公開用のButtonは不要（ユーザーがクリックできてしまうため）なので無効化
            var btn = tileTransform.gameObject.GetComponent<UnityEngine.UI.Button>();
            if (btn != null) btn.interactable = false;

            // 本当のIDをリストに追加して記憶する
            realTileIds.Add(realId);
        }

        public override void RemoveTileFromHand(RectTransform tileTransform, int tileId)
        {
            if (handSlots.Contains(tileTransform))
            {
                int index = handSlots.IndexOf(tileTransform);
                if (index >= 0 && index < realTileIds.Count)
                {
                    realTileIds.RemoveAt(index);
                }
            }
            base.RemoveTileFromHand(tileTransform, tileId);
        }

        public void ClearHand()
        {
            // 注: 実際のGameObjectのプール返却は GameUIManager 経由で行うため、ここではリストのクリアのみ行う
            GetHandSlots().Clear();
            realTileIds.Clear();
        }

        public void RevealTileByIndex(int index)
        {
            Debug.Log($"[EnemyHandUI] RevealTileByIndex called with index: {index}. handSlots Count: {handSlots.Count}, realTileIds Count: {realTileIds.Count}");
            if (index < 0 || index >= handSlots.Count || index >= realTileIds.Count) return;

            int realId = realTileIds[index];
            RectTransform targetTile = handSlots[index];

            if (targetTile != null)
            {
                if (tileResourceManager == null)
                {
                    if (gameUIManager != null)
                    {
                        tileResourceManager = gameUIManager.TileResourceManager;
                    }
                    
                    if (tileResourceManager == null)
                    {
                        Debug.LogError("[EnemyHandUI] tileResourceManager is NULL and could not be found via GameUIManager! Cannot reveal tile.");
                        return;
                    }
                }

                var visual = targetTile.GetComponent<TileVisual>();
                if (visual != null)
                {
                    Sprite s = tileResourceManager.GetTileSprite(realId);
                    Debug.Log($"[EnemyHandUI] Revealing tile with realId: {realId}, Sprite: {(s != null ? s.name : "null")}");
                    visual.SetTile(realId, s);
                }
                else
                {
                    Debug.LogError("[EnemyHandUI] TileVisual component not found on targetTile!");
                }
            }
            else
            {
                Debug.LogError("[EnemyHandUI] targetTile is null!");
            }
        }

        public void RevealAllHands(TileResourceManager tileResourceManager)
        {
            for (int i = 0; i < handSlots.Count; i++)
            {
                if (i < realTileIds.Count)
                {
                    int realId = realTileIds[i]; // ここにはencodedIdが入るようになった
                    var visual = handSlots[i].GetComponent<TileVisual>();
                    if (visual != null && tileResourceManager != null)
                    {
                        visual.SetTile(realId, tileResourceManager.GetTileSprite(realId));
                        visual.SetExposed(false); // 目アイコンは出さない
                    }
                }
            }
        }

        public override void SortHandSlots()
        {
            var pairs = new List<System.Tuple<int, RectTransform>>();
            for (int i = 0; i < handSlots.Count; i++)
            {
                int id = (i < realTileIds.Count) ? realTileIds[i] : 0;
                pairs.Add(new System.Tuple<int, RectTransform>(id, handSlots[i]));
            }
            
            pairs.Sort((a, b) =>
            {
                int baseA = a.Item1 & 0x1F;
                int baseB = b.Item1 & 0x1F;
                if (baseA != baseB) return baseA.CompareTo(baseB);
                return a.Item1.CompareTo(b.Item1);
            });

            handSlots.Clear();
            realTileIds.Clear();
            for (int i = 0; i < pairs.Count; i++)
            {
                var p = pairs[i];
                realTileIds.Add(p.Item1);
                handSlots.Add(p.Item2);
                p.Item2.SetSiblingIndex(i);
            }
            
            if (gameUIManager != null) UpdateLayout(gameUIManager.CurrentPhaseStatus);
        }

        public override void UpdateLayout(RoundStatus phaseStatus)
        {
            if (handSlotContainer == null) return;

            bool isGameEndPhase = phaseStatus == RoundStatus.Agari || 
                                  phaseStatus == RoundStatus.Ron || 
                                  phaseStatus == RoundStatus.Result || 
                                  phaseStatus == RoundStatus.Draw;

            bool isBoardActivePhase = phaseStatus == RoundStatus.Discard || isGameEndPhase;

            var layoutGroup = handSlotContainer.GetComponent<UnityEngine.UI.LayoutGroup>();
            Transform activeContainer = (isBoardActivePhase && discardPhaseContainer != null) 
                                        ? discardPhaseContainer : handSlotContainer;
            
            if (discardPhaseContainer != null)
            {
                discardPhaseContainer.gameObject.SetActive(activeContainer == discardPhaseContainer);
            }
            if (handSlotContainer != null)
            {
                handSlotContainer.gameObject.SetActive(activeContainer == handSlotContainer);
            }

            if (!isBoardActivePhase)
            {
                if (layoutGroup != null) layoutGroup.enabled = true;
                
                foreach (var st in handSlots)
                {
                    if (st.parent != activeContainer) 
                    {
                        st.SetParent(activeContainer, false);
                    }
                    st.localPosition = new Vector3(st.localPosition.x, st.localPosition.y, 0f);
                    st.localScale = Vector3.one;
                    st.localRotation = Quaternion.identity;
                }
                
                // Do NOT sort enemy hand slots! Sorting messes up the index mapping for RevealTileByIndex.

                for (int i = 0; i < handSlots.Count; i++)
                {
                    handSlots[i].SetSiblingIndex(i);
                }
                return;
            }

            // Discard Phase Layout
            if (layoutGroup != null) layoutGroup.enabled = false;
            
            foreach (var st in handSlots)
            {
                if (st.parent != activeContainer) 
                {
                    st.SetParent(activeContainer, false);
                }
                st.localPosition = new Vector3(st.localPosition.x, st.localPosition.y, 0f);
                st.localScale = Vector3.one;
                st.localRotation = Quaternion.identity;
            }

            // Do NOT sort enemy hand slots!

            int totalTiles = handSlots.Count;
            int maxPerRow = 14; // 敵の手牌を1列で表示する
            float startX = - ((Mathf.Min(totalTiles, maxPerRow) - 1) * tileSpacingX) / 2f;
            
            for (int i = 0; i < totalTiles; i++)
            {
                int row = i / maxPerRow;
                int col = i % maxPerRow;
                
                int tilesInThisRow = Mathf.Min(maxPerRow, totalTiles - row * maxPerRow);
                float rowStartX = - ((tilesInThisRow - 1) * tileSpacingX) / 2f;

                float xPos = rowStartX + col * tileSpacingX;
                float yPos = -row * tileSpacingY;

                var rt = handSlots[i];
                rt.anchoredPosition3D = new Vector3(xPos, yPos, 0f);
                rt.localPosition = new Vector3(rt.localPosition.x, rt.localPosition.y, 0f); // 念のためローカルZも0に
                
                rt.SetSiblingIndex(i);
            }
        }
    }
}
