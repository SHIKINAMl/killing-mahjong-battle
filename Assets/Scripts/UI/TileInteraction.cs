using UnityEngine;
using UnityEngine.EventSystems;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public class TileInteraction : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, ICanvasRaycastFilter
    {
        public int TileId { get; set; }
        public int PoolSlotIndex { get; set; } = -1; // ★ プールのどのスロットに属するかを記憶する

        /// <summary>
        /// この牌が山（OriginalWallTiles）の何番目かを保持する。
        ///
        /// 同じ牌IDは山に複数あるため、TileId から index を引く（IndexOf など）と
        /// **常に最初の1枚が当たってしまい、別の牌を指してしまう。**
        /// サーバーへ index を送る操作（牌交換など）では必ずこちらを使うこと。
        /// 山に並べていない牌では -1。
        /// </summary>
        public int WallIndex { get; set; } = -1;
        public bool IsInHand { get; private set; }
        public Vector3 OriginalWallPosition { get; set; } // ★ 壁の本来の座標を記憶するプロパティ追加
        public bool IsHovered { get; private set; }

        private GameUIManager _gameUIManager;
        private Canvas _canvas;
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Vector3 _originalPosition;
        private Transform _originalParent;

        private TileVisual _tileVisual;

        public void Initialize(int tileId, bool isInHand, GameUIManager manager, Canvas canvas)
        {
            TileId = tileId;
            IsInHand = isInHand;
            _gameUIManager = manager;
            _canvas = canvas;
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _tileVisual = GetComponent<TileVisual>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_gameUIManager != null && _gameUIManager.IsTransitioning) return;
            
            // アニメーション中もホバーを受け付ける
            Debug.Log($"[TileInteraction] OnPointerClick called. TileId: {TileId}, IsInHand: {IsInHand}, Button: {eventData.button}, Phase: {(_gameUIManager != null ? _gameUIManager.CurrentPhaseStatus.ToString() : "null")}");
            
            if (_gameUIManager != null && _gameUIManager.CurrentPhaseStatus == RoundStatus.Discard)
            {
                if (!IsInHand)
                {
                    if (eventData.button == PointerEventData.InputButton.Left)
                    {
                        // 既に選択されていたら（浮いている状態なら）捨てる
                        if (_gameUIManager.IsTileSelected(TileId))
                        {
                            _gameUIManager.DiscardSelectedTile();
                        }
                        else
                        {
                            // 左クリック：選択（または選択解除）
                            _gameUIManager.SelectTile(TileId, IsInHand, false);
                        }
                    }
                    else if (eventData.button == PointerEventData.InputButton.Right)
                    {
                        // 右クリック：選択して即座に打牌
                        _gameUIManager.SelectTile(TileId, IsInHand, false);
                        _gameUIManager.DiscardSelectedTile();
                    }
                }
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
            {
                Debug.Log("[TileInteraction] Ignored because not Left Click.");
                return;
            }
            if (_gameUIManager != null && _gameUIManager.IsOpponentSkillProcessing)
            {
                Debug.Log("[TileInteraction] Ignored because IsOpponentSkillProcessing is true.");
                return;
            }

            // Any Click -> Move (Left or Right) or Select for Mulligan
            IsHovered = false;
            if (_tileVisual != null) _tileVisual.SetHoverHighlight(false);

            if (IsInHand)
            {
                if (_gameUIManager != null && _gameUIManager.IsMulliganSelection)
                {
                    _gameUIManager.OnMulliganTileSelected(TileId, GetComponent<RectTransform>());
                }
                else
                {
                    _gameUIManager.MoveTileToWall(TileId);
                }
            }
            else
            {
                if (_gameUIManager != null && _gameUIManager.IsMulliganSelection)
                {
                    _gameUIManager.OnMulliganTileSelected(TileId, GetComponent<RectTransform>());
                }
                else
                {
                    Debug.Log($"[TileInteraction] Calling MoveTileToHand for TileId: {TileId}");
                    _gameUIManager.MoveTileToHand(TileId);
                }
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // ユーザー要望によりドラッグでの移動は無効化
        }

        public void OnDrag(PointerEventData eventData)
        {
            // ユーザー要望によりドラッグでの移動は無効化
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // ユーザー要望によりドラッグでの移動は無効化
        }

        private void ReturnToOriginal()
        {
            transform.position = _originalPosition;
            transform.SetParent(_originalParent);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_gameUIManager != null && _gameUIManager.IsTransitioning) return;
            IsHovered = true;
            
            bool canHighlight = false;
            if (_gameUIManager != null && _gameUIManager.IsMulliganSelection)
            {
                if (TileId != -1)
                {
                    canHighlight = true;
                }
            }
            else if (_gameUIManager != null && _gameUIManager.CurrentPhaseStatus == RoundStatus.HandSelection)
            {
                if (TileId != -1)
                {
                    canHighlight = true;
                }
            }
            else if (_gameUIManager != null && _gameUIManager.CurrentPhaseStatus == RoundStatus.Discard)
            {
                if (KillingMahjong.Managers.BoardStateManager.Instance.IsLocalTurn && IsInHand && TileId != -1)
                {
                    canHighlight = true;
                }
            }

            if (canHighlight)
            {
                if (_tileVisual != null) _tileVisual.SetHoverHighlight(true);
                if (KillingMahjong.Managers.AudioManager.Instance != null)
                {
                    KillingMahjong.Managers.AudioManager.Instance.PlayHoverSE();
                }
            }
            if (_gameUIManager != null) _gameUIManager.OnTileHoverEnter(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsHovered = false;
            if (_tileVisual != null) _tileVisual.SetHoverHighlight(false);

            if (_gameUIManager != null && _gameUIManager.IsTransitioning) return;

            if (_gameUIManager != null) _gameUIManager.OnTileHoverExit(this);
        }

        /// <summary>
        /// 「見えている部分」だけをクリック判定にする。
        ///
        /// 牌は幅45に対して間隔25で並べているため、隣の牌と20ほど重なっている。
        /// 手前に描かれるのは後ろの兄弟なので、判定を牌の全幅のままにしておくと
        /// 覆われて見えない領域まで自分が拾ってしまい、
        /// 「乗っているのに反応しない」「隣の牌を打ってしまう」という挙動になる。
        ///
        /// そこで、自分より手前に描かれる牌に覆われている点は自分の判定から外す。
        /// 判定はレイアウトの実座標から毎回求めるので、手牌・山牌・打牌フェイズの
        /// 2段組みなど、並べ方が変わっても追従する。
        /// </summary>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            RectTransform rt = _rectTransform != null ? _rectTransform : transform as RectTransform;
            if (rt == null) return true;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, eventCamera, out Vector2 local))
                return false;

            // 牌の外にはみ出した子要素（透視マーク等）の上は牌として扱わない
            if (!rt.rect.Contains(local)) return false;

            Transform parent = rt.parent;
            if (parent == null) return true;

            Vector3 world = rt.TransformPoint(local);

            // 自分より後ろの兄弟＝手前に描かれる牌
            for (int i = rt.GetSiblingIndex() + 1; i < parent.childCount; i++)
            {
                RectTransform other = parent.GetChild(i) as RectTransform;
                if (other == null || !other.gameObject.activeInHierarchy) continue;
                if (other.GetComponent<TileInteraction>() == null) continue;

                // ドラッグ中などで入力を受け付けない牌は覆っていないものとして扱う
                var cg = other.GetComponent<CanvasGroup>();
                if (cg != null && !cg.blocksRaycasts) continue;

                Vector2 otherLocal = other.InverseTransformPoint(world);
                if (other.rect.Contains(otherLocal)) return false;
            }

            return true;
        }

        private void OnDisable()
        {
            IsHovered = false;
            if (_tileVisual != null) _tileVisual.SetHoverHighlight(false);
            if (_gameUIManager != null) _gameUIManager.OnTileHoverExit(this);
        }
    }
}
