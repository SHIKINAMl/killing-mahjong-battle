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
    /// 絵は、撃ち込んだ自分の血の粒。血の表示の右上に小さく置き、ゆっくり脈を打たせる。
    /// 最初の版は血の表示を囲む照準（四隅のかぎ形）だったが、演出を透視・牌交換の雰囲気へ
    /// 作り直したのに合わせて、線の照準はやめた（<see cref="AssaultSkillEffect"/>）。
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
                _shapes.Ring(at, Radius + 3f + beat * 9f, 2f, ring);

                DrawDrop(_shapes, at, Radius, 1f);
            }
            _shapes.End();
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
