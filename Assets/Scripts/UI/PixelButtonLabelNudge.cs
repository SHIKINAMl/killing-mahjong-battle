using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// ドット絵のボタン（Assets/Sprites/HandButtons。Aseprite で描いた）の上の文字を、絵の面の真ん中に置く。
    ///
    /// 絵は下に2ドットの厚みがあり、押した絵では面が1ドット沈む。絵の切り替えは Button の Sprite Swap がやり、
    /// ここは文字だけを合わせる: ふだんは2単位上（厚みのぶん）、押している間は真ん中、押せないときは薄く。
    /// 乗せた・押したの状態は同じボタンの <see cref="UIInteractionFeedback"/> から読む。
    /// 2026-11-08 ユーザー「ちょっとAIで生成したっぽいか？ asepriteを起動してほかのものを作ったみたいに作ってみて」
    /// </summary>
    public class PixelButtonLabelNudge : MonoBehaviour
    {
        [Tooltip("ふだんの文字の高さ（厚み 2ドット × 2倍表示 ÷ 2）")]
        [SerializeField] private float restOffset = 2f;
        [Tooltip("押している間の文字の高さ")]
        [SerializeField] private float pressedOffset = 0f;

        private Selectable _selectable;
        private UIInteractionFeedback _feedback;
        private TextMeshProUGUI[] _labels;
        private int _lastState = -1;

        private void OnEnable()
        {
            _selectable = GetComponent<Selectable>();
            _feedback = GetComponent<UIInteractionFeedback>();
            _labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            _lastState = -1;
        }

        private void LateUpdate()
        {
            if (_feedback == null) _feedback = GetComponent<UIInteractionFeedback>();
            bool disabled = _selectable != null && !_selectable.IsInteractable();
            bool pressed = !disabled && _feedback != null && _feedback.IsPressed;
            int state = disabled ? 2 : pressed ? 1 : 0;
            if (state == _lastState) return;
            _lastState = state;

            float y = pressed ? pressedOffset : restOffset;
            foreach (var tmp in _labels)
            {
                if (tmp == null) continue;
                var rt = tmp.rectTransform;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
                tmp.alpha = disabled ? 0.55f : 1f;
            }
        }
    }
}
