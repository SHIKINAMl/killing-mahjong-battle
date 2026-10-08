using UnityEngine;
using KillingMahjong.Common;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 強襲を撃ったあと、狙われた側の血の表示に残る照準の印（2026-10-08）。
    ///
    /// **強襲は、撃ってからアガるまで何巡も効果が出ない。** そのあいだ画面に何も残らないので、
    /// 撃ったこと自体を忘れる（Antigravity と案を出し合ったときに挙がった、いちばんの問題）。
    /// 局が終わるまで赤い照準を出しっぱなしにして、「いま狙っている」を見せ続ける。
    ///
    /// 出すのは <see cref="AssaultSkillEffect"/> の最後。消すのは局の頭
    /// （<see cref="Clear"/> を呼ぶ）。シーンをまたぐと自分で消える。
    ///
    /// 印は対象を毎コマ追いかける。血の表示が隠れているあいだ（決着の演出など）は一緒に隠れる。
    /// </summary>
    public class AssaultMarkUI : MonoBehaviour
    {
        /// <summary>かぎ形の腕の長さと太さ（Canvas の単位。2 で1ドット）。</summary>
        public const float Arm = 18f;
        public const float Thickness = 4f;

        public static readonly Color Red = new Color32(0xF0, 0x28, 0x34, 0xFF);
        public static readonly Color RedDim = new Color32(0xA8, 0x14, 0x20, 0xFF);
        /// <summary>照準の芯の色。**赤だけだと赤い壁と血袋の上で見えない**ので、芯は白に近い色にして赤で縁取る。</summary>
        public static readonly Color Core = new Color32(0xFF, 0xF0, 0xF0, 0xFF);
        public static readonly Color CoreDim = new Color32(0xFF, 0xA8, 0xB0, 0xFF);
        public static readonly Color Outline = new Color(0f, 0f, 0f, 0.85f);

        private const float PulseStep = 0.30f;

        private static AssaultMarkUI _current;

        private SkillEffectStage _stage;
        private PixelShapeGraphic _shapes;
        private RectTransform _target;
        private float _clock;

        /// <summary>対象の血の表示に照準を出す。すでに出ていれば対象だけ差し替える。</summary>
        public static void Show(RectTransform target)
        {
            if (!Application.isPlaying || target == null) return;

            if (_current == null)
            {
                var go = new GameObject("AssaultMarkUI");
                _current = go.AddComponent<AssaultMarkUI>();
                _current._stage = new SkillEffectStage("Stage", UISortingOrders.AssaultMark, go.transform);
                _current._shapes = _current._stage.AddShapes("Reticle");
            }

            _current._target = target;
            _current._clock = 0f;
            _current.Redraw();
        }

        /// <summary>照準を消す。出ていなければ何もしない。</summary>
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
                Rect r = ReticleRect(_stage, _target);
                // ゆっくり2段で脈を打たせる。なめらかには動かさない
                bool wide = Mathf.FloorToInt(_clock / PulseStep) % 2 == 1;
                DrawReticle(_shapes, wide ? SkillEffectStage.Expand(r, 2f) : r, wide ? CoreDim : Core, Red);
            }
            _shapes.End();
        }

        /// <summary>
        /// 照準が最後に収まる四角。演出（<see cref="AssaultSkillEffect"/>）と印とで同じ式を使い、
        /// 引き継ぐときに位置が飛ばないようにする。
        /// </summary>
        internal static Rect ReticleRect(SkillEffectStage stage, RectTransform target)
        {
            Rect hp = stage.LocalVisibleRectOf(target);

            // 血の表示は側によって大きさが違う（スマホの画面／血袋）。
            // 小さすぎても大きすぎても照準に見えないので、ほどよい大きさに収める
            float w = Mathf.Clamp(hp.width + 16f, 64f, 170f);
            float h = Mathf.Clamp(hp.height + 16f, 64f, 170f);

            Vector2 c = SkillEffectStage.Snap(hp.center);
            w = SkillEffectStage.Snap(w);
            h = SkillEffectStage.Snap(h);
            return new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h);
        }

        /// <summary>
        /// 照準の絵。四隅のかぎ形と、4辺の真ん中から内へ向く短い目盛り。
        /// **真ん中には何も描かない。** そこには血の量が見えていて、十字を重ねると読めなくなる。
        /// </summary>
        /// <param name="core">芯の色</param>
        /// <param name="rim">芯のまわりの縁の色。その外にさらに黒い縁が付く</param>
        internal static void DrawReticle(PixelShapeGraphic g, Rect r, Color core, Color rim)
        {
            Color outline = Outline;
            outline.a *= core.a;

            // 外から 黒 → 縁 → 芯 の3重。どんな背景の上でも形が残る
            g.Brackets(r, Arm, Thickness, 4f, outline);
            DrawTicks(g, r, 4f, outline);
            g.Brackets(r, Arm, Thickness, 2f, rim);
            DrawTicks(g, r, 2f, rim);
            g.Brackets(r, Arm, Thickness, 0f, core);
            DrawTicks(g, r, 0f, core);
        }

        private static void DrawTicks(PixelShapeGraphic g, Rect r, float grow, Color color)
        {
            const float len = 10f;
            float half = Thickness * 0.5f;
            Vector2 c = r.center;

            g.Box(c.x - half - grow, r.yMax - len - grow, c.x + half + grow, r.yMax + grow, color);
            g.Box(c.x - half - grow, r.yMin - grow, c.x + half + grow, r.yMin + len + grow, color);
            g.Box(r.xMin - grow, c.y - half - grow, r.xMin + len + grow, c.y + half + grow, color);
            g.Box(r.xMax - len - grow, c.y - half - grow, r.xMax + grow, c.y + half + grow, color);
        }
    }
}
