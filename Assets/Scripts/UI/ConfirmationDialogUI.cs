using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace KillingMahjong.UI
{
    public class ConfirmationDialogUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button okButton;
        [SerializeField] private Button noButton;

        private Action onConfirmAction;
        private Action onCancelAction;

        private void Awake()
        {
            if (okButton != null) okButton.onClick.AddListener(OnOkClicked);
            if (noButton != null) noButton.onClick.AddListener(OnNoClicked);

            // --- UIの被り・レイアウト崩れ対策 ---
            // 確実な最前面表示のため、Canvasコンポーネントを追加してSortingOrderを高く設定する
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = 9999; // 非常に高い値にして手牌や他のCanvasより手前に出す

            // ボタンのクリック判定が効くようにGraphicRaycasterを追加
            GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // 自身にImageが無ければ追加して、画面全体を覆う半透明背景にする
            Image bg = GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
            }
            // ちょっと白っぽい半透明背景に変更
            bg.color = new Color(0.9f, 0.9f, 0.9f, 0.95f);

            // 画面全体を覆うようにRectTransformを設定
            RectTransform rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
            }

            // テキストが巨大化するのを防ぎ、適切なサイズ・配置にする
            if (messageText != null)
            {
                messageText.enableAutoSizing = false;
                messageText.fontSize = 40; // テキストサイズを40に変更
                messageText.alignment = TextAlignmentOptions.Center;
                messageText.overflowMode = TextOverflowModes.Overflow; // 文字が潰れるのを防ぐ

                RectTransform textRt = messageText.GetComponent<RectTransform>();
                if (textRt != null)
                {
                    // アンカーをStretchに設定
                    textRt.anchorMin = new Vector2(0, 0);
                    textRt.anchorMax = new Vector2(1, 1);
                    // Left, Bottomの設定 (Bottomはボタンと被らないよう適度に空ける)
                    textRt.offsetMin = new Vector2(20, 200);
                    // Right, Topの設定 (Topを250にする)
                    textRt.offsetMax = new Vector2(-20, -250);
                }
            }

            // ボタンの位置もテキストと被らないように調整
            if (okButton != null)
            {
                RectTransform okRt = okButton.GetComponent<RectTransform>();
                if (okRt != null)
                {
                    okRt.anchorMin = new Vector2(0.55f, 0.2f);
                    okRt.anchorMax = new Vector2(0.85f, 0.35f);
                    okRt.offsetMin = Vector2.zero;
                    okRt.offsetMax = Vector2.zero;
                }
            }

            if (noButton != null)
            {
                RectTransform noRt = noButton.GetComponent<RectTransform>();
                if (noRt != null)
                {
                    noRt.anchorMin = new Vector2(0.15f, 0.2f);
                    noRt.anchorMax = new Vector2(0.45f, 0.35f);
                    noRt.offsetMin = Vector2.zero;
                    noRt.offsetMax = Vector2.zero;
                }
            }
        }

        public void ShowDialog(string message, Action onConfirm, Action onCancel)
        {
            if (messageText != null) messageText.text = message;
            onConfirmAction = onConfirm;
            onCancelAction = onCancel;
            
            gameObject.SetActive(true);
            transform.SetAsLastSibling(); // 手牌UIなどより手前(最前面)に表示
        }

        private void OnOkClicked()
        {
            gameObject.SetActive(false);
            onConfirmAction?.Invoke();
        }

        private void OnNoClicked()
        {
            gameObject.SetActive(false);
            onCancelAction?.Invoke();
        }
    }
}
