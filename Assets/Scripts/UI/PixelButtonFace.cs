using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>
    /// ボタンの面をドット絵で描く（2026-11-08 ユーザー指示「並べなおす、お任せ、決定ボタンも作り直してほしい。ドットっぽくして」）。
    ///
    /// それまでの3つのボタンは、絵の無い赤一色の四角だった。ここでは 1ドット＝2単位で、
    ///   ・墨の縁（四隅を1ドット欠いて角を丸める）
    ///   ・上と左に1ドットの光、下に2ドットの影（盛り上がって見える）
    ///   ・押している間は影を消して面を下げ、上に1ドットの陰を落とす（へこんで見える）
    ///   ・乗せると面が少し明るくなる。押せないときは灰色
    /// を描く。色はスクロールバー（<see cref="PixelScrollBar"/>）の墨・血の色と合わせた。
    ///
    /// ボタンの子に置く（ボタンの Image は透明にして、押せる範囲だけに使う）。
    /// 乗せた・押したの状態は、同じボタンの <see cref="UIInteractionFeedback"/> から読む。
    /// 中の文字も、面が下がったぶんだけ下げる。
    /// 置き場所はシーン（HandUI の決定・おまかせ。並べ直す・選び直すは決定の複製なので一緒に付いてくる）。
    /// </summary>
    public class PixelButtonFace : PixelShapeGraphic
    {
        public enum Tone { Primary, Secondary }

        [SerializeField] private Tone tone = Tone.Primary;
        [Tooltip("1ドットの大きさ（Canvas の単位）")]
        [SerializeField] private float dot = 2f;

        private static readonly Color32 Ink = new Color32(24, 15, 22, 255);

        // 主ボタン（決定）: 血の赤
        private static readonly Color32 PrimaryFace = new Color32(178, 44, 54, 255);
        private static readonly Color32 PrimaryHover = new Color32(204, 64, 70, 255);
        private static readonly Color32 PrimaryLight = new Color32(236, 122, 108, 255);
        private static readonly Color32 PrimaryShadow = new Color32(104, 22, 34, 255);

        // 控えのボタン（おまかせ・並べ直す）: 赤茶
        private static readonly Color32 SecondaryFace = new Color32(102, 64, 72, 255);
        private static readonly Color32 SecondaryHover = new Color32(126, 84, 90, 255);
        private static readonly Color32 SecondaryLight = new Color32(164, 124, 124, 255);
        private static readonly Color32 SecondaryShadow = new Color32(56, 34, 44, 255);

        // 押せないとき
        private static readonly Color32 DisabledFace = new Color32(78, 66, 72, 255);
        private static readonly Color32 DisabledLight = new Color32(100, 88, 94, 255);
        private static readonly Color32 DisabledShadow = new Color32(48, 40, 46, 255);

        private const int StateNormal = 0, StateHover = 1, StatePressed = 2, StateDisabled = 3;

        private Selectable _selectable;
        private UIInteractionFeedback _feedback;
        private int _lastState = -1;
        private Rect _lastRect;

        public void SetTone(Tone value)
        {
            tone = value;
            _lastState = -1;
            Draw(CurrentState());
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            Cache();
            _lastState = -1;
            Draw(CurrentState());
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            Cache();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (!isActiveAndEnabled) return;
            _lastState = -1;
            Draw(CurrentState());
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _lastState = -1;
            Draw(StateNormal);
        }
#endif

        private void LateUpdate()
        {
            if (_selectable == null) Cache();
            int state = CurrentState();
            if (state != _lastState || rectTransform.rect != _lastRect) Draw(state);
        }

        private void Cache()
        {
            _selectable = GetComponentInParent<Selectable>();
            _feedback = _selectable != null ? _selectable.GetComponent<UIInteractionFeedback>() : null;
        }

        private int CurrentState()
        {
            if (_selectable != null && !_selectable.IsInteractable()) return StateDisabled;
            if (_feedback != null && _feedback.IsPressed) return StatePressed;
            if (_feedback != null && _feedback.IsHovered) return StateHover;
            return StateNormal;
        }

        private void Draw(int state)
        {
            _lastState = state;
            Rect r = rectTransform.rect;
            _lastRect = r;
            float d = Mathf.Max(1f, dot);

            // 升目にそろえる
            float w = Mathf.Floor(r.width / d) * d;
            float h = Mathf.Floor(r.height / d) * d;
            float x0 = Mathf.Round((r.center.x - w * 0.5f) / d) * d;
            float y0 = Mathf.Round((r.center.y - h * 0.5f) / d) * d;
            float x1 = x0 + w, y1 = y0 + h;

            Color32 face, light, shadow;
            if (state == StateDisabled) { face = DisabledFace; light = DisabledLight; shadow = DisabledShadow; }
            else if (tone == Tone.Primary)
            {
                face = state == StateHover ? PrimaryHover : PrimaryFace; light = PrimaryLight; shadow = PrimaryShadow;
            }
            else
            {
                face = state == StateHover ? SecondaryHover : SecondaryFace; light = SecondaryLight; shadow = SecondaryShadow;
            }

            bool pressed = state == StatePressed;
            float shadowH = pressed ? 0f : state == StateDisabled ? d : d * 2f;
            float topInset = pressed ? d : 0f;

            Begin();
            // 墨の縁。四隅を1ドット欠く
            Box(x0 + d, y0, x1 - d, y1, Ink);
            Box(x0, y0 + d, x1, y1 - d, Ink);

            float ix0 = x0 + d, iy0 = y0 + d, ix1 = x1 - d, iy1 = y1 - d;
            // 下の影（押していないとき）
            Box(ix0, iy0, ix1, iy0 + shadowH, shadow);
            // 押している間は上に陰を落とす
            Box(ix0, iy1 - topInset, ix1, iy1, shadow);
            // 面
            float fy0 = iy0 + shadowH, fy1 = iy1 - topInset;
            Box(ix0, fy0, ix1, fy1, face);
            if (!pressed)
            {
                // 上と左の光。内側の角は1ドット欠いて丸める
                Box(ix0 + d, fy1 - d, ix1 - d, fy1, light);
                Box(ix0, fy0 + d, ix0 + d, fy1 - d, light);
            }
            End();

            // 文字を面の真ん中へ
            float faceCenter = (fy0 + fy1) * 0.5f - r.center.y;
            MoveLabels(Mathf.Round(faceCenter), state == StateDisabled);
        }

        private void MoveLabels(float y, bool disabled)
        {
            // 編集中は文字に触らない（シーンが書き換わってしまう）
            if (!Application.isPlaying || _selectable == null) return;
            foreach (var tmp in _selectable.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                var rt = tmp.rectTransform;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
                tmp.alpha = disabled ? 0.55f : 1f;
            }
        }
    }
}
