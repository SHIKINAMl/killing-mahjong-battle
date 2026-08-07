using UnityEngine;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    [RequireComponent(typeof(GameUIManager))]
    public class AutoDiscardController : MonoBehaviour
    {
        private GameUIManager uiManager;
        private bool _isAutoDiscarding = false;

        private void Awake()
        {
            uiManager = GetComponent<GameUIManager>();
        }

        private void Start()
        {
            if (BoardStateManager.Instance != null)
            {
                BoardStateManager.Instance.OnTurnChanged += HandleTurnChanged;
            }
        }

        private void OnDestroy()
        {
            if (BoardStateManager.Instance != null)
            {
                BoardStateManager.Instance.OnTurnChanged -= HandleTurnChanged;
            }
        }

        private void HandleTurnChanged(bool isLocalTurn)
        {
            if (isLocalTurn)
            {
                CheckAndExecuteAutoDiscard();
            }
        }

        public void CheckAndExecuteAutoDiscard()
        {
            var handUI = uiManager.HandUI;
            if (handUI != null && handUI.IsAutoDiscardEnabled && !_isAutoDiscarding)
            {
                if (uiManager.CurrentPhaseStatus == RoundStatus.Discard && BoardStateManager.Instance.IsLocalTurn)
                {
                    // **演出中は打たない。** 人間は画面が覆われて牌を押せないので、
                    // ここを見ないと自動打牌だけが人間に不可能な速度で打ててしまう。
                    // 実際それでロン猶予が演出中に届き、保留されたまま対局が止まった。
                    if (uiManager.IsBusyWithTransition) return;

                    if (IsAgariPending())
                    {
                        handUI.IsAutoDiscardEnabled = false;
                        return;
                    }
                    StartCoroutine(AutoDiscardCoroutine());
                }
            }
        }

        private bool IsAgariPending()
        {
            return uiManager.RonWaitPanel != null && uiManager.RonWaitPanel.activeSelf;
        }

        public void CancelAutoDiscard()
        {
            var handUI = uiManager.HandUI;
            if (handUI != null) handUI.IsAutoDiscardEnabled = false;
        }

        private System.Collections.IEnumerator AutoDiscardCoroutine()
        {
            _isAutoDiscarding = true;
            yield return new WaitForSeconds(0.5f); // Simulate human delay
            
            if (uiManager.CurrentPhaseStatus != RoundStatus.Discard || !BoardStateManager.Instance.IsLocalTurn)
            {
                _isAutoDiscarding = false;
                yield break;
            }
            // 待っている 0.5 秒の間に演出が始まることがあるので、ここでも見る
            if (uiManager.IsBusyWithTransition)
            {
                _isAutoDiscarding = false;
                yield break;
            }
            if (IsAgariPending())
            {
                CancelAutoDiscard();
                _isAutoDiscarding = false;
                yield break;
            }

            var handUI = uiManager.HandUI;
            if (handUI == null || !handUI.IsAutoDiscardEnabled) 
            {
                _isAutoDiscarding = false;
                yield break;
            }

            var wallTiles = BoardStateManager.Instance.CurrentWallTiles;
            if (wallTiles != null && wallTiles.Count > 0)
            {
                int tileToDiscard = -1;
                // Find the first tile in the wall that has not been discarded yet
                foreach (int tileId in wallTiles)
                {
                    if (BoardStateManager.Instance.FindAvailableWallIndex(tileId) >= 0)
                    {
                        tileToDiscard = tileId;
                        break;
                    }
                }

                if (tileToDiscard != -1)
                {
                    uiManager.SelectTile(tileToDiscard, false, false); // isInHand = false
                    yield return new WaitForSeconds(0.2f);
                    uiManager.DiscardSelectedTile();
                }
            }
            _isAutoDiscarding = false;
        }
    }
}
