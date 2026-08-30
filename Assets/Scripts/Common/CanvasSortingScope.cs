using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.Common
{
    /// <summary>
    /// 特定のUIを一時的に最前面へ出し、後で元の状態に戻すためのユーティリティ。
    ///
    /// プロジェクトルール:
    /// 前面化は「対象のルートとなる Canvas の overrideSorting を有効化して Order を引き上げる」
    /// 方式のみを用いる。GetComponentsInChildren&lt;Canvas&gt;(true) による子Canvasの一括上書きは
    /// 背景とボタン等の重なり順を壊すため、絶対に行わないこと。
    ///
    /// - 対象に Canvas が無い場合は Canvas + GraphicRaycaster を追加し、復元時に破棄する
    /// - 対象に Canvas が有る場合は overrideSorting / sortingOrder / sortingLayerName を退避し、
    ///   復元時に元へ戻す
    /// </summary>
    public sealed class CanvasSortingScope
    {
        private sealed class CanvasState
        {
            public Canvas CanvasRef;
            public bool WasAdded;
            public bool OriginalOverrideSorting;
            public int OriginalSortingOrder;
            public string OriginalSortingLayer;
        }

        private readonly Dictionary<GameObject, CanvasState> _states =
            new Dictionary<GameObject, CanvasState>();

        /// <summary>
        /// 対象のルートCanvasを前面化する。
        /// </summary>
        /// <param name="go">前面化する対象 (この GameObject 自身の Canvas のみを操作する)</param>
        /// <param name="order">設定する sortingOrder (UISortingOrders の定数を使用すること)</param>
        /// <param name="sortingLayerName">設定する sortingLayer 名。null の場合は変更しない。
        /// 本プロジェクトの Sorting Layer は Default のみなので、通常は指定しないこと</param>
        public void BringToFront(GameObject go, int order, string sortingLayerName = null)
        {
            if (go == null) return;

            // 既に前面化済みなら Order のみ更新 (二重退避で元の状態を失わないようにする)
            if (_states.TryGetValue(go, out var existing))
            {
                if (existing.CanvasRef != null)
                {
                    existing.CanvasRef.sortingOrder = order;
                    if (sortingLayerName != null) existing.CanvasRef.sortingLayerName = sortingLayerName;
                }
                return;
            }

            var canvas = go.GetComponent<Canvas>();
            var state = new CanvasState();
            if (canvas == null)
            {
                canvas = go.AddComponent<Canvas>();
                go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                state.WasAdded = true;
            }
            else
            {
                state.WasAdded = false;
                state.OriginalOverrideSorting = canvas.overrideSorting;
                state.OriginalSortingOrder = canvas.sortingOrder;
                state.OriginalSortingLayer = canvas.sortingLayerName;
            }
            state.CanvasRef = canvas;
            _states[go] = state;

            canvas.overrideSorting = true;
            if (sortingLayerName != null) canvas.sortingLayerName = sortingLayerName;
            canvas.sortingOrder = order;
        }

        /// <summary>
        /// 対象を元の状態に戻す。BringToFront していない対象には何もしない。
        /// </summary>
        public void Restore(GameObject go)
        {
            if (go == null) return;
            if (!_states.TryGetValue(go, out var state)) return;

            RestoreState(go, state);
            _states.Remove(go);
        }

        private static void RestoreState(GameObject go, CanvasState state)
        {
            if (state.WasAdded)
            {
                var raycaster = go.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                if (raycaster != null) Object.Destroy(raycaster);
                var canvas = go.GetComponent<Canvas>();
                if (canvas != null) Object.Destroy(canvas);
            }
            else if (state.CanvasRef != null)
            {
                state.CanvasRef.overrideSorting = state.OriginalOverrideSorting;
                state.CanvasRef.sortingOrder = state.OriginalSortingOrder;
                state.CanvasRef.sortingLayerName = state.OriginalSortingLayer;
            }
        }
    }
}
