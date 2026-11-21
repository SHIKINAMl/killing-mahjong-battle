using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KillingMahjong.UI
{
    /// <summary>
    /// Web 版で、TMP_InputField に日本語（IME）を打てるようにする（2026-10-20）。
    ///
    /// Unity の Web 版の TMP_InputField は、変換入力を受け取れない（「たろう」と打っても何も入らない）。
    /// そこで、打つあいだだけ入力欄の真上に透明な HTML の &lt;input&gt; を重ね（<c>Assets/Plugins/WebGL/KmImeInput.jslib</c>）、
    /// その中身を毎フレーム入力欄へ写す。Enter で決めたら <see cref="TMP_InputField.onSubmit"/> を呼ぶ。
    ///
    /// 入力欄と同じ物に付けておく。エディタと Windows 版では何もしない（TMP_InputField がそのまま打てる）。
    /// </summary>
    [RequireComponent(typeof(TMP_InputField))]
    public class WebImeInput : MonoBehaviour, IPointerDownHandler
    {
        private TMP_InputField _input;
        private bool _open;

        private void Awake()
        {
            _input = GetComponent<TMP_InputField>();
#if UNITY_WEBGL && !UNITY_EDITOR
            _input.onSelect.AddListener(_ => Open());
#endif
        }

        /// <summary>
        /// 入力欄を選んで、すぐパソコンのキーボードで打てるようにする（2026-10-25、画面のキーボードを消したとき）。
        /// `ActivateInputField` だけでは選ばれた扱いにならず、Web 版では日本語の欄が重ならないので、
        /// 欄を一度押すまで日本語が打てなかった。
        /// </summary>
        public static void Focus(TMP_InputField input)
        {
            if (input == null) return;
            input.ActivateInputField();
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != input.gameObject)
                events.SetSelectedGameObject(input.gameObject);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // もう開いていても開き直す（HTML の欄から離れて閉じたのと、同じフレームで押されることがある）
            Open();
        }

        private void Open()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!_input.interactable || !isActiveAndEnabled) return;
            var rt = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners); // 左下・左上・右上・右下
            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            float sw = Screen.width, sh = Screen.height;
            KmImeInput_Open(min.x / sw, 1f - max.y / sh, (max.x - min.x) / sw, (max.y - min.y) / sh,
                _input.text, _input.characterLimit);
            // Unity がページ全体のキーを吸うと、HTML の欄に文字が入らない
            WebGLInput.captureAllKeyboardInput = false;
            _open = true;
#endif
        }

        private void Close()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!_open) return;
            _open = false;
            KmImeInput_Close();
            WebGLInput.captureAllKeyboardInput = true;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void Update()
        {
            if (!_open) return;
            string text = KmImeInput_GetText();
            if (text != _input.text)
            {
                _input.text = text;
                _input.caretPosition = _input.text.Length;
            }
            int state = KmImeInput_GetState();
            if (state == 0) return;
            Close();
            if (state == 1) _input.onSubmit.Invoke(_input.text);
        }

        private void OnDisable() => Close();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void KmImeInput_Open(float x, float y, float w, float h, string text, int maxLength);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string KmImeInput_GetText();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int KmImeInput_GetState();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void KmImeInput_Close();
#endif
    }
}
