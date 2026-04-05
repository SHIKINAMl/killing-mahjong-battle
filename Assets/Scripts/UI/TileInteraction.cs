using UnityEngine;
using UnityEngine.EventSystems;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public class TileInteraction : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public int TileId { get; private set; }
        public bool IsInHand { get; private set; }
        public Vector3 OriginalWallPosition { get; set; } // ★ 壁の本来の座標を記憶するプロパティ追加
        public bool IsHovered { get; private set; }

        private GameUIManager _gameUIManager;
        private Canvas _canvas;
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Vector3 _originalPosition;
        private Transform _originalParent;

        public void Initialize(int tileId, bool isInHand, GameUIManager manager, Canvas canvas)
        {
            TileId = tileId;
            IsInHand = isInHand;
            _gameUIManager = manager;
            _canvas = canvas;
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
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

            if (eventData.button != PointerEventData.InputButton.Left) return;

            // Any Click -> Move (Left or Right)
            if (IsInHand)
                _gameUIManager.MoveTileToWall(TileId);
            else
                _gameUIManager.MoveTileToHand(TileId);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (_gameUIManager != null && _gameUIManager.CurrentPhaseStatus == RoundStatus.Discard) return;
            
            _originalPosition = transform.position;
            _originalParent = transform.parent;
            
            _canvasGroup.blocksRaycasts = false;
            
            // Lift up visually?
            // If World Object, this might behave differently than UI.
            // Assuming UI because user mentioned "HandUI...HorizontalLayout".
            // But WallUI is 3D world?
            // "Transform" slots imply mixed usage or World usage.
            // If World Object, IDragHandler requires PhysicsRaycaster on Camera.
            // Let's assume standard EventSystem setup handles it if Colliders/Images present.
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (_gameUIManager != null && _gameUIManager.CurrentPhaseStatus == RoundStatus.Discard) return;

            // If Screen Space Overlay/Camera
            if (_rectTransform != null && _canvas != null)
            {
                _rectTransform.anchoredPosition += eventData.delta / _canvas.scaleFactor;
            }
            else
            {
                // World Space Drag?
                // Simple implementation: Screen to World point
                Plane plane = new Plane(Vector3.up, transform.position);
                Ray ray = Camera.main.ScreenPointToRay(eventData.position);
                if (plane.Raycast(ray, out float enter))
                {
                    transform.position = ray.GetPoint(enter);
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (_gameUIManager != null && _gameUIManager.CurrentPhaseStatus == RoundStatus.Discard) return;

            _canvasGroup.blocksRaycasts = true;

            // Hit Detection
            // We need to check if dropped on Hand Area.
            bool droppedInHand = _gameUIManager.IsPointerInHandArea(eventData.position);

            if (IsInHand)
            {
                if (!droppedInHand)
                {
                    // Dragged OUT of Hand -> Move to Wall
                    _gameUIManager.MoveTileToWall(TileId);
                }
                else
                {
                    // Dropped inside Hand -> Just reorder? Or reset.
                    // For now, reset position (LayoutGroup will handle it)
                    ReturnToOriginal();
                }
            }
            else // In Wall
            {
                if (droppedInHand)
                {
                    // Dragged INTO Hand -> Move to Hand
                    _gameUIManager.MoveTileToHand(TileId);
                }
                else
                {
                    // Dropped elsewhere -> Return
                    ReturnToOriginal();
                }
            }
        }

        private void ReturnToOriginal()
        {
            transform.position = _originalPosition;
            transform.SetParent(_originalParent);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            IsHovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsHovered = false;
        }
    }
}
