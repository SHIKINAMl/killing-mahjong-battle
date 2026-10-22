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

        public void OnPointerDown(PointerEventData eventData)
        {
            // もう開いていても開き直す（HTML の欄から離れて閉じたのと、同じフレームで押されることがある）
            Open();
        }

        /// <summary>HTML の入力欄を重ねているか。Web 版以外では常に false。</summary>
        public bool IsOpen => _open;

        /// <summary>
        /// 重ねた HTML の入力欄が押されたとき（Web 版だけ）。そこを押しても Unity の入力欄には届かないので、
        /// 押されたら知りたい物（名前の欄のキーボード）はこれを聞く。
        /// </summary>
#pragma warning disable 0067 // 鳴らすのは Web 版のときだけ（エディタと Windows 版では使わない）
        public event System.Action OverlayClicked;
#pragma warning restore 0067

        /// <summary>重ねている HTML の入力欄を閉じる（決定はしない）。画面のキーボード（<see cref="ContractKeyboardUI"/>）が使う。</summary>
        public void CloseOverlay() => Close();

        /// <summary>入力欄のいまの位置に、HTML の入力欄を重ね直す。入力欄が動いたあとに使う。</summary>
        public void Reopen() => Open();

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
            if (KmImeInput_ConsumeClick() == 1)
            {
                OverlayClicked?.Invoke();
                if (!_open) return; // 押されたのを受けて閉じられた
            }
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

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int KmImeInput_ConsumeClick();
#endif
    }
}
