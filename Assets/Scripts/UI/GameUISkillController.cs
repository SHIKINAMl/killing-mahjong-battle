using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;
using UnityEngine.UI;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    [RequireComponent(typeof(GameUIManager))]
    public partial class GameUISkillController : MonoBehaviour
    {
        private GameUIManager uiManager;
        private YakuSelectionUI yakuSelectionUI;

        [Header("Yaku Selection")]
        [SerializeField] private Font yakuSelectionFont;

        public bool IsMulliganSelection { get; private set; }

        private int _lastMulliganOutTileId = -1;
        private int _lastMulliganTargetIndex = -1;

        /// <summary>牌交換スキルの交換演出（分離クラス）</summary>
        private MulliganSwapAnimator _mulliganSwapAnimator;
        public void Setup(GameUIManager manager)
        {
            this.uiManager = manager;
            _mulliganSwapAnimator = new MulliganSwapAnimator(manager);

            if (mulliganCanvas != null)
            {
                mulliganCanvas.SetActive(false);
            }
        }

        public void CancelSkillSelection()
        {
            IsMulliganSelection = false;
            HideMulliganUI();
        }

        [Header("Mulligan UI Settings")]
        [SerializeField] private GameObject mulliganCanvas;
        private System.Collections.Generic.List<GameObject> hiddenUIs = new System.Collections.Generic.List<GameObject>();

        public void StartMulliganSelection()
        {
            IsMulliganSelection = true;
            ShowMulliganUI();
        }

        private RectTransform _lastMulliganOutSlotRt;

        /// <summary>診断用。牌IDの並びを「id(牌名)」形式で連結する。</summary>
        private static string Join(System.Collections.Generic.List<int> list)
        {
            if (list == null) return "(null)";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                int id = list[i];
                sb.Append(i).Append(':').Append(id);
                int b = Common.TileId.BaseId(id);
                if (b > 28) sb.Append("(範囲外!)");
            }
            return sb.ToString();
        }

        /// <summary>診断用。リスト内に同じ牌IDが何枚あるかを数える。</summary>
        private static int CountOf(System.Collections.Generic.List<int> list, int tileId)
        {
            if (list == null) return 0;
            int n = 0;
            foreach (var v in list) if (v == tileId) n++;
            return n;
        }

        public void OnMulliganTileSelected(int tileId, RectTransform slotRt)
        {
            IsMulliganSelection = false;
            HideMulliganUI();
            
            // アニメーション中の不意なRebuildを防ぐ
            CancelPendingSkillRequest();
            pendingMulligan = uiManager.BeginTransition("mulligan-request");
            
            var wallTiles = BoardStateManager.Instance.OriginalWallTiles;
            if (wallTiles != null)
            {
                // クリックされた牌そのものの山index を使う。
                // ここで wallTiles.IndexOf(tileId) を使うと、同じ牌IDが山に複数あるとき
                // **常に最初の1枚の index が返り、別の牌が交換されてしまう**
                // （同じ絵柄の別の牌が入れ替わって見える不具合の原因だった）。
                int targetIndex = -1;
                var clicked = slotRt != null ? slotRt.GetComponent<TileInteraction>() : null;
                if (clicked != null) targetIndex = clicked.WallIndex;

                // 山に並べていない牌など WallIndex が無い場合だけ、従来どおり牌IDから引く
                if (targetIndex < 0 || targetIndex >= wallTiles.Count || wallTiles[targetIndex] != tileId)
                {
                    targetIndex = wallTiles.IndexOf(tileId);
                }

                if (targetIndex != -1)
                {
                    _lastMulliganOutTileId = tileId;
                    _lastMulliganTargetIndex = targetIndex;
                    _lastMulliganOutSlotRt = slotRt;
                    
                    // クリック直後には透明にしない。アニメーション開始時に透明にする。

                    uiManager.SendActionToServer("skill", new Network.ActionPayload { skill_type = "mulligan", target_hand_index = targetIndex });
                }
                else
                {
                    Debug.LogWarning("Mulligan failed: Selected tile not found in wall tiles.");
                    CancelPendingSkillRequest();
                }
            }
            else CancelPendingSkillRequest();
        }

        private void ShowMulliganUI()
        {
            if (mulliganCanvas != null) mulliganCanvas.SetActive(true);
            
            _sortingScope.BringToFront(uiManager.HandUI?.gameObject, UISortingOrders.MulliganFocusTiles);
            _sortingScope.BringToFront(uiManager.WallUI?.gameObject, UISortingOrders.MulliganFocusTiles);
            
            // Hide distracting/overlapping UI elements
            hiddenUIs.Clear();
            HideIfActive(uiManager.DialogueUI?.gameObject);
            HideIfActive(uiManager.PlayerInfoUI?.gameObject);
            HideIfActive(uiManager.EnemyInfoUI?.gameObject);
            HideIfActive(uiManager.YakuListUI?.gameObject);
        }

        private void HideIfActive(GameObject go)
        {
            if (go != null && go.activeSelf)
            {
                hiddenUIs.Add(go);
                go.SetActive(false);
            }
        }

        /// <summary>マリガン中の手牌/山UIの前面化と復元。
        /// プロジェクトルールに従い、対象のルートCanvasの overrideSorting のみを操作する。</summary>
        private readonly CanvasSortingScope _sortingScope = new CanvasSortingScope();

        private void HideMulliganUI()
        {
            if (mulliganCanvas != null) mulliganCanvas.SetActive(false);
            if (uiManager != null)
            {
                if (uiManager.HandUI != null) _sortingScope.Restore(uiManager.HandUI.gameObject);
                if (uiManager.WallUI != null) _sortingScope.Restore(uiManager.WallUI.gameObject);
            }
            
            foreach (var go in hiddenUIs)
            {
                if (go != null) go.SetActive(true);
            }
            hiddenUIs.Clear();
        }

        public void StartBoostHandSelection()
        {
            if (yakuSelectionUI == null)
            {
                yakuSelectionUI = gameObject.AddComponent<YakuSelectionUI>();
                if (yakuSelectionFont != null)
                {
                    yakuSelectionUI.customFont = yakuSelectionFont;
                }
            }

            yakuSelectionUI.Show(
                onSelected: (yakuName) => {
                    uiManager.SendActionToServer("skill", new Network.ActionPayload { skill_type = "boost_hand", yaku_name = yakuName });
                },
                onCanceled: () => {
                    Debug.Log("Boost hand cancelled");
                }
            );
        }

    }
}
