using TMPro;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// ドット字体の数字に、**濃い色の縁と影を付けた**文字（2026-11-25）。
    ///
    /// ユーザー「清算後の血を獲得したり失ったりするときの数字が見にくい」。
    /// 原因は、TMP の縁取り（outlineWidth）が**ドット字体（DenkiChip・PixelMplus）には描かれていなかった**こと。
    /// 縁の無い水色・赤の字が、娘の服（灰）や血袋（桃）の上を通って溶けていた（実機の録画で確認）。
    /// <see cref="DamagePopupUI"/> と同じく、**同じ字を濃い色で周りにずらして重ね、縁にする。**
    ///
    /// 表の字（<see cref="Create"/> が返す物）の文字・大きさ・透明度を変えれば、縁は毎フレーム後から付いてくる。
    /// 動かす・拡大する・消すのは <c>root</c> の方を使う。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InkedText : MonoBehaviour
    {
        /// <summary>縁の色。ダメージの浮き数字と同じ。</summary>
        public static readonly Color32 Ink = new Color32(29, 13, 22, 255);

        private TextMeshProUGUI _face;
        private TextMeshProUGUI[] _edges;

        /// <param name="edge">縁の太さ（親の単位）。影はその倍だけ右下へ落とす。</param>
        public static TextMeshProUGUI Create(Transform parent, string name, float edge, out RectTransform root)
        {
            var go = new GameObject(name, typeof(RectTransform));
            root = (RectTransform)go.transform;
            root.SetParent(parent, false);
            var inked = go.AddComponent<InkedText>();

            // 影を先に置く（いちばん奥）。続けて8方向の縁、最後に表の字
            Vector2[] offsets =
            {
                new Vector2(edge, -edge * 2f),
                new Vector2(-edge, 0f), new Vector2(edge, 0f), new Vector2(0f, edge), new Vector2(0f, -edge),
                new Vector2(-edge, edge), new Vector2(edge, edge), new Vector2(-edge, -edge), new Vector2(edge, -edge),
            };
            inked._edges = new TextMeshProUGUI[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
                inked._edges[i] = Layer(root, i == 0 ? "Shadow" : "Edge", offsets[i]);
            inked._face = Layer(root, "Face", Vector2.zero);
            return inked._face;
        }

        private static TextMeshProUGUI Layer(RectTransform root, string name, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(root, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = offset;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            return text;
        }

        private void LateUpdate() => Sync();

        /// <summary>表の字に合わせ直す。作った直後のフレームでも描く前に呼ばれる（LateUpdate）。</summary>
        public void Sync()
        {
            if (_face == null || _edges == null) return;
            Color ink = Ink;
            ink.a = _face.color.a;
            foreach (var e in _edges)
            {
                if (e == null) continue;
                if (e.text != _face.text) e.text = _face.text;
                if (e.font != _face.font) e.font = _face.font;
                e.fontSize = _face.fontSize;
                e.fontStyle = _face.fontStyle;
                e.alignment = _face.alignment;
                e.textWrappingMode = _face.textWrappingMode;
                e.overflowMode = _face.overflowMode;
                e.color = ink;
                e.enabled = _face.enabled;
            }
        }
    }
}
