using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.Visuals
{
    /// <summary>
    /// 色の濃い UI（カットインの帯など）の外周に黒い縁を足すためのヘルパー。
    ///
    /// UGUI は子が親より手前に描かれるので、対象の「後ろ」に置くには
    /// 同じ親の中で対象より前の兄弟にする必要がある。ここではそれを行う。
    ///
    /// 対象がアニメーションで動く場合は、返した RectTransform も
    /// 同じように動かすこと（呼び出し側でアニメーション対象に含める）。
    /// </summary>
    public static class UIEdgeOutline
    {
        /// <summary>
        /// target と同じ形・同じ位置で、少しだけ大きい黒い板を target の背面に作る。
        /// </summary>
        /// <param name="target">縁を付けたい UI</param>
        /// <param name="thickness">縁の太さ(px)。上下左右にこのぶん広げる</param>
        public static RectTransform AddBehind(RectTransform target, float thickness = 10f)
        {
            if (target == null || target.parent == null) return null;

            var go = new GameObject(target.name + "_BlackEdge", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(target.parent, false);

            rt.anchorMin = target.anchorMin;
            rt.anchorMax = target.anchorMax;
            rt.pivot = target.pivot;
            rt.sizeDelta = target.sizeDelta + new Vector2(thickness * 2f, thickness * 2f);
            rt.anchoredPosition = target.anchoredPosition;
            rt.localRotation = target.localRotation;
            rt.localScale = target.localScale;

            var img = go.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;

            // target のひとつ手前＝背面へ
            rt.SetSiblingIndex(target.GetSiblingIndex());
            return rt;
        }
    }
}
