using UnityEngine;
using KillingMahjong.Common;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 強襲を撃ったあと、狙われた側の血の表示に残る「血の印」（2026-10-08、10-09 に絵を作り直した）。
    ///
    /// **強襲は、撃ってからアガるまで何巡も効果が出ない。** そのあいだ画面に何も残らないと、
    /// 撃ったこと自体を忘れる。局が終わるまで印を出しっぱなしにして、「いま狙っている」を見せ続ける。
    ///
    /// 絵は、差し押さえの札（自分の血で判を押した小さな紙。<see cref="DrawTag"/>）。
    /// 血の表示の右上に小さく置き、ゆっくり脈を打たせる。
    /// 最初の版は血の表示を囲む照準（四隅のかぎ形）、次は血の粒だった。2026-10-10 に強襲の背景を
    /// 「ベルルとの追加の契約（配当を受け取らず、その分を相手から取り立てさせる）」と決めたので、札にした。
    ///
    /// 出すのは <see cref="AssaultSkillEffect"/> が血を撃ち込んだ瞬間。消すのは局の頭
    /// （<see cref="Clear"/>。BoardStateManager.ClearAllBoardData が呼ぶ）。シーンをまたぐと自分で消える。
    ///
    /// 印は対象を毎コマ追いかける。血の表示が隠れているあいだ（決着の演出など）は一緒に隠れる。
    /// </summary>
    public class AssaultMarkUI : MonoBehaviour
    {
        public static readonly Color Blood = new Color32(0xE8, 0x24, 0x34, 0xFF);
        public static readonly Color BloodDark = new Color32(0x7A, 0x0C, 0x18, 0xFF);
        public static readonly Color BloodShine = new Color32(0xFF, 0xB4, 0xBC, 0xFF);
        public static readonly Color Outline = new Color(0.10f, 0f, 0.02f, 0.92f);

        /// <summary>印の粒の半径（Canvas の単位）。</summary>
        public const float Radius = 8f;

        private static AssaultMarkUI _current;

        private SkillEffectStage _stage;
        private PixelShapeGraphic _shapes;
        private RectTransform _target;
        private float _clock;

        /// <summary>対象の血の表示に印を出す。すでに出ていれば対象だけ差し替える。</summary>
        public static void Show(RectTransform target)
        {
            if (!Application.isPlaying || target == null) return;

            if (_current == null)
            {
                var go = new GameObject("AssaultMarkUI");
                _current = go.AddComponent<AssaultMarkUI>();
                _current._stage = new SkillEffectStage("Stage", UISortingOrders.AssaultMark, go.transform);
                _current._shapes = _current._stage.AddShapes("Mark");
            }

            _current._target = target;
            _current._clock = 0f;
            _current.Redraw();
        }

        /// <summary>印を消す。出ていなければ何もしない。</summary>
        public static void Clear()
        {
            if (_current == null) return;
            Destroy(_current.gameObject);
            _current = null;
        }

        private void OnDestroy()
        {
            if (_current == this) _current = null;
        }

        private void LateUpdate()
        {
            _clock += Time.deltaTime;
            Redraw();
        }

        private void Redraw()
        {
            if (_shapes == null) return;

            _shapes.Begin();
            // 対象が消えた・隠れたあいだは描かない（印だけ宙に浮いて見えるのを防ぐ）
            if (_target != null && _target.gameObject.activeInHierarchy)
            {
                Vector2 at = MarkPosition(_stage, _target);

                // 脈。1.2 秒に1回、輪が広がりながら薄くなる
                float beat = Mathf.Repeat(_clock, 1.2f) / 1.2f;
                Color ring = Blood;
                ring.a = (1f - beat) * 0.7f;
                _shapes.Ring(at, Radius + 5f + beat * 9f, 2f, ring);

                DrawTag(_shapes, at, 1f, 1f);
            }
            _shapes.End();
        }

        private static readonly Color Paper = new Color32(0xF2, 0xE6, 0xC8, 0xFF);
        private static readonly Color Ink = new Color32(0x4A, 0x3A, 0x30, 0xFF);

        /// <summary>
        /// 差し押さえの札を描く。縦長の紙に、字のつもりの横線2本と、赤い血判。
        ///
        /// 強襲は「配当を受け取らない代わりに、その分をベルルが相手から取り立てる」追加の契約
        /// （2026-10-10 にユーザーと決めた背景）。相手の血の表示に残るのは、その札。
        /// </summary>
        /// <param name="scale">1 で幅14・高さ20（Canvas の単位）</param>
        internal static void DrawTag(PixelShapeGraphic g, Vector2 center, float scale, float alpha)
        {
            if (scale <= 0.05f || alpha <= 0f) return;

            float w = 14f * scale, h = 20f * scale;
            float x0 = center.x - w * 0.5f, x1 = center.x + w * 0.5f;
            float y0 = center.y - h * 0.5f, y1 = center.y + h * 0.5f;

            Color outline = Outline; outline.a *= alpha;
            Color paper = Paper; paper.a = alpha;
            Color ink = Ink; ink.a = alpha;

            g.Box(x0 - 2f, y0 - 2f, x1 + 2f, y1 + 2f, outline);
            g.Box(x0, y0, x1, y1, paper);
            g.Box(x0 + 3f * scale, y1 - 5f * scale, x1 - 3f * scale, y1 - 3f * scale, ink);
            g.Box(x0 + 3f * scale, y1 - 9f * scale, x1 - 5f * scale, y1 - 7f * scale, ink);
            DrawSeal(g, center + new Vector2(1.5f * scale, -4f * scale), 4.5f * scale, alpha);
        }

        /// <summary>血判（丸い判子の跡）を描く。暗い縁、赤い面、内側に暗い輪。</summary>
        internal static void DrawSeal(PixelShapeGraphic g, Vector2 center, float radius, float alpha)
        {
            if (radius <= 0.5f || alpha <= 0f) return;

            Color dark = BloodDark; dark.a = alpha;
            Color red = Blood; red.a = alpha;

            g.Disc(center, radius + 1.5f, dark);
            g.Disc(center, radius, red);
            if (radius >= 8f) g.Ring(center, radius - 4f, 2f, dark);
        }

        /// <summary>
        /// 印を置く場所。血が見えている所の右上の角。真ん中に置くと血の量が読めなくなる。
        /// 演出（<see cref="AssaultSkillEffect"/>）の撃ち込む先も、同じ式で決める。
        /// </summary>
        internal static Vector2 MarkPosition(SkillEffectStage stage, RectTransform target)
        {
            Rect hp = stage.LocalVisibleRectOf(target);
            return SkillEffectStage.Snap(new Vector2(hp.xMax + 2f, hp.yMax - 2f));
        }

        /// <summary>血の粒を1つ描く。暗い縁、赤い玉、左上の照り。</summary>
        internal static void DrawDrop(PixelShapeGraphic g, Vector2 center, float radius, float alpha)
        {
            if (radius <= 0.5f || alpha <= 0f) return;

            Color outline = Outline; outline.a *= alpha;
            Color dark = BloodDark; dark.a = alpha;
            Color red = Blood; red.a = alpha;
            Color shine = BloodShine; shine.a = alpha;

            g.Disc(center, radius + 2f, outline);
            g.Disc(center, radius, dark);
            g.Disc(center + new Vector2(-radius * 0.12f, radius * 0.12f), radius * 0.80f, red);
            g.Disc(center + new Vector2(-radius * 0.38f, radius * 0.38f), Mathf.Max(1.5f, radius * 0.22f), shine);
        }
    }
}
