using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>接続失敗と離席予告で使う、シーン内だけに存在する操作可能な案内。</summary>
    public sealed class SessionPrompt : MonoBehaviour
    {
        private TMP_Text message;
        private bool returning;

        public static SessionPrompt Show(string text, string actionLabel, Action action)
        {
            var root = new GameObject("SessionPrompt", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Image), typeof(SessionPrompt));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Common.UISortingOrders.SessionPrompt;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            root.GetComponent<Image>().color = new Color(0.04f, 0.01f, 0.02f, 0.96f);
            var prompt = root.GetComponent<SessionPrompt>();
            prompt.message = MakeText(root.transform, text, new Vector2(0f, 50f), new Vector2(700f, 180f));
            CreateButton(root.transform, actionLabel, new Vector2(0f, -100f), action);
            return prompt;
        }

        public void SetMessage(string text) { message.text = text; }

        public static Button CreateButton(Transform parent, string label, Vector2 position, Action action)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400f, 60f);
            rect.anchoredPosition = position;
            obj.GetComponent<Image>().color = new Color32(65, 20, 31, 255);
            var text = MakeText(obj.transform, label, Vector2.zero, rect.sizeDelta);
            text.raycastTarget = false;
            var button = obj.GetComponent<Button>();
            button.onClick.AddListener(() => action());
            return button;
        }

        private static TMP_Text MakeText(Transform parent, string value, Vector2 position, Vector2 size)
        {
            var obj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var text = obj.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset font = null;
            foreach (var existing in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (existing != text && existing.font != null) { font = existing.font; break; }
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = 24f;
            text.color = new Color32(240, 232, 236, 255);
            text.alignment = TextAlignmentOptions.Center;
            return text;
        }

        public async void ReturnToTitle()
        {
            if (returning) return;
            returning = true;
            foreach (var button in GetComponentsInChildren<Button>()) button.interactable = false;
            var tutorial = FindFirstObjectByType<Managers.TutorialManager>();
            if (tutorial != null)
            {
                LoadingManager.Instance?.ForceHide();
                tutorial.SkipTutorial();
                return;
            }
            TutorialNavigation.Cancel();
            Managers.Tutorial.TutorialAudioDirector.ResetVisuals();
            var client = WebSocketGameClientSample.Instance;
            if (client != null) await Task.WhenAny(client.ResetConnectionAsync(), Task.Delay(2000));
            if (this == null) return;
            LoadingManager.Instance?.ForceHide();
            SceneManager.LoadScene("タイトルシーン");
        }
    }
}
