using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 能力の演出（役強化・強襲）が絵を置くための舞台（2026-10-08）。
    ///
    /// 自前の Canvas を1枚作り、その上に暗幕・図形・文字を重ねる。
    /// **シーンには置かない。** 対局シーンが複数あるので、シーンに持たせると
    /// 片方にだけ入れる事故が起きる（<see cref="PerspectiveSkillEffect"/> と同じ理由）。
    ///
    /// 座標は 800x600 を基準にした Canvas の単位で、**画面の中心が原点。**
    /// 画面が大きくなっても同じ見た目の比率で出る。
    ///
    /// クリックは食わない（GraphicRaycaster を付けない）。演出の最中も、下の画面は今までどおり押せる。
    /// </summary>
    internal sealed class SkillEffectStage
    {
        public readonly GameObject Root;
        public readonly RectTransform Rect;
        public readonly Canvas Canvas;

        public SkillEffectStage(string name, int sortingOrder, Transform parent)
        {
            Root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            if (parent != null) Root.transform.SetParent(parent, false);

            Canvas = Root.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.overrideSorting = true;
            Canvas.sortingOrder = sortingOrder;

            var scaler = Root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.matchWidthOrHeight = 0.5f;

            Rect = (RectTransform)Root.transform;

            // 作った直後は Canvas の大きさも倍率もまだ決まっていない。
            // 決めさせてからでないと、画面の座標をこの Canvas の座標へ直せない
            UnityEngine.Canvas.ForceUpdateCanvases();
        }

        /// <summary>図形を並べる板を1枚足す。あとから足したものほど手前に描かれる。</summary>
        public PixelShapeGraphic AddShapes(string name)
        {
            // CanvasRenderer は自分で付ける。new GameObject 経由だと RequireComponent が効かず、絵が出ない
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(PixelShapeGraphic));
            Stretch(go);
            var g = go.GetComponent<PixelShapeGraphic>();
            g.raycastTarget = false;
            return g;
        }

        /// <summary>楕円の穴つきの暗幕を足す（透視と同じ部品）。</summary>
        public PerspectiveDarkenLayer AddDarken(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(PerspectiveDarkenLayer));
            Stretch(go);
            var g = go.GetComponent<PerspectiveDarkenLayer>();
            g.raycastTarget = false;
            g.color = color;
            g.Strength = 0f;
            return g;
        }

        public TextMeshProUGUI AddText(string name, string text, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(Rect, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(600f, fontSize * 1.6f);

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            // 赤い壁や緑の卓の上を通るので、縁取りが無いと読めない
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = new Color32(0, 0, 0, 255);
            return tmp;
        }

        private void Stretch(GameObject go)
        {
            go.transform.SetParent(Rect, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// 対象（HPの表示など）が画面のどこに映っているかを、この舞台の座標で返す。
        ///
        /// **相手側のHP表示は「Screen Space - Camera」の Canvas に載っている。**
        /// ワールド座標をそのまま使うと単位が合わない（2026-10-08 にロン演出で踏んだ）。
        /// 必ずその Canvas のカメラを通して画面の座標へ直し、そこからこの舞台へ持ってくる。
        /// </summary>
        public Rect LocalRectOf(RectTransform target)
        {
            return target == null ? new Rect() : LocalRectOf(target, target.rect);
        }

        /// <summary>
        /// 対象の絵のうち、**実際に色が載っている部分**が画面のどこに映っているか。
        ///
        /// 相手の血袋の絵は、吊るす管まで含めた縦長の1枚（256x1024）で、血が見えるのはその一部だけ。
        /// 枠の四角をそのまま使うと、照準が血袋の上へ外れる（2026-10-08 に踏んだ）。
        /// スプライトが覚えている「透明を削った範囲」を使って、見えている所だけを返す。
        /// 絵が無い・範囲が取れないときは <see cref="LocalRectOf(RectTransform)"/> と同じ。
        /// </summary>
        public Rect LocalVisibleRectOf(RectTransform target)
        {
            if (target == null) return new Rect();

            Rect r = target.rect;
            var image = target.GetComponent<Image>();
            if (image != null && image.sprite != null && !image.preserveAspect &&
                (image.type == Image.Type.Simple || image.type == Image.Type.Filled))
            {
                try
                {
                    Sprite s = image.sprite;
                    Rect full = s.rect;
                    Rect used = s.textureRect;
                    Vector2 offset = s.textureRectOffset;
                    if (full.width > 0f && full.height > 0f && used.width > 0f && used.height > 0f)
                    {
                        r = UnityEngine.Rect.MinMaxRect(
                            r.xMin + r.width * (offset.x / full.width),
                            r.yMin + r.height * (offset.y / full.height),
                            r.xMin + r.width * ((offset.x + used.width) / full.width),
                            r.yMin + r.height * ((offset.y + used.height) / full.height));
                    }
                }
                catch (UnityException)
                {
                    // アトラスに詰めたスプライトは範囲を教えてくれない。そのときは枠の四角のまま使う
                }
            }
            return LocalRectOf(target, r);
        }

        private Rect LocalRectOf(RectTransform target, Rect inTarget)
        {
            var corners = new Vector3[4];
            corners[0] = target.TransformPoint(new Vector3(inTarget.xMin, inTarget.yMin, 0f));
            corners[2] = target.TransformPoint(new Vector3(inTarget.xMax, inTarget.yMax, 0f));

            Camera cam = null;
            Canvas canvas = target.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                Canvas root = canvas.rootCanvas;
                if (root.renderMode != RenderMode.ScreenSpaceOverlay) cam = root.worldCamera;
            }

            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

            Vector2 la, lb;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, a, null, out la);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, b, null, out lb);

            return UnityEngine.Rect.MinMaxRect(Mathf.Min(la.x, lb.x), Mathf.Min(la.y, lb.y),
                                               Mathf.Max(la.x, lb.x), Mathf.Max(la.y, lb.y));
        }

        /// <summary>1ドット（UIの2単位）の倍数にそろえる。半端な位置に置くと、ドット絵の中でそこだけ滲む。</summary>
        public static float Snap(float v)
        {
            return Mathf.Round(v * 0.5f) * 2f;
        }

        public static Vector2 Snap(Vector2 v)
        {
            return new Vector2(Snap(v.x), Snap(v.y));
        }

        public static Rect Expand(Rect r, float by)
        {
            return UnityEngine.Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);
        }
    }
}
