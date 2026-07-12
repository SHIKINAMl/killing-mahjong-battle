using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using KillingMahjong.UI;

namespace KillingMahjong.Editor
{
    public class OptionUICreator : MonoBehaviour
    {
        [MenuItem("Tools/UI/オプション画面（OptionUI）を作成")]
        public static void CreateOptionUI()
        {
            // 既存のOptionUIがあれば削除
            OptionUI existing = FindObjectOfType<OptionUI>();
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            // Canvasを探す
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("シーンにCanvasがありません。先にCanvasを作成してください。");
                return;
            }

            // ベースパネル作成
            GameObject panelObj = new GameObject("OptionUI");
            panelObj.transform.SetParent(canvas.transform, false);
            var rect = panelObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(800, 600); // 800x600ジャストに設定
            var img = panelObj.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.1f, 0.95f); // ダークグレー背景

            OptionUI optionUI = panelObj.AddComponent<OptionUI>();

            // タイトル
            CreateText(panelObj.transform, "OptionTitle", "OPTIONS", new Vector2(0, 250), 40, TextAlignmentOptions.Center);

            // 背景枠（Audio）
            CreatePanel(panelObj.transform, "AudioPanel", new Vector2(0, 100), new Vector2(750, 200));
            // BGM
            CreateText(panelObj.transform, "BgmText", "BGM Volume", new Vector2(-200, 160), 24, TextAlignmentOptions.Left);
            Slider bgmSlider = CreateSlider(panelObj.transform, "BgmSlider", new Vector2(100, 160));
            // SE
            CreateText(panelObj.transform, "SeText", "SE Volume", new Vector2(-200, 100), 24, TextAlignmentOptions.Left);
            Slider seSlider = CreateSlider(panelObj.transform, "SeSlider", new Vector2(100, 100));
            // Voice
            CreateText(panelObj.transform, "VoiceText", "Voice Volume", new Vector2(-200, 40), 24, TextAlignmentOptions.Left);
            Slider voiceSlider = CreateSlider(panelObj.transform, "VoiceSlider", new Vector2(100, 40));

            // 背景枠（Game）
            CreatePanel(panelObj.transform, "GamePanel", new Vector2(0, -70), new Vector2(750, 120));
            // High Speed Toggle
            CreateText(panelObj.transform, "SpeedText", "High Speed Mode", new Vector2(-200, -40), 24, TextAlignmentOptions.Left);
            Toggle speedToggle = CreateToggle(panelObj.transform, "HighSpeedToggle", new Vector2(200, -40));
            // Effect Toggle
            CreateText(panelObj.transform, "EffectText", "Show Effects", new Vector2(-200, -100), 24, TextAlignmentOptions.Left);
            Toggle effectToggle = CreateToggle(panelObj.transform, "EffectToggle", new Vector2(200, -100));

            // Buttons (System)
            Button returnBtn = CreateButton(panelObj.transform, "ReturnButton", "Title", new Vector2(-250, -230), new Vector2(180, 50));
            Button quitBtn = CreateButton(panelObj.transform, "QuitButton", "Quit", new Vector2(-50, -230), new Vector2(180, 50));

            // Buttons (Save/Close)
            Button saveBtn = CreateButton(panelObj.transform, "SaveButton", "Save & Close", new Vector2(200, -230), new Vector2(220, 50));
            Button cancelBtn = CreateButton(panelObj.transform, "CancelButton", "X", new Vector2(360, 260), new Vector2(50, 50));

            // スクリプトにアタッチ
            SerializedObject serializedObject = new SerializedObject(optionUI);
            serializedObject.FindProperty("bgmSlider").objectReferenceValue = bgmSlider;
            serializedObject.FindProperty("seSlider").objectReferenceValue = seSlider;
            serializedObject.FindProperty("voiceSlider").objectReferenceValue = voiceSlider;
            serializedObject.FindProperty("highSpeedToggle").objectReferenceValue = speedToggle;
            serializedObject.FindProperty("effectToggle").objectReferenceValue = effectToggle;
            serializedObject.FindProperty("saveAndCloseButton").objectReferenceValue = saveBtn;
            serializedObject.FindProperty("closeButton").objectReferenceValue = cancelBtn;
            serializedObject.FindProperty("returnToTitleButton").objectReferenceValue = returnBtn;
            serializedObject.FindProperty("quitButton").objectReferenceValue = quitBtn;
            serializedObject.ApplyModifiedProperties();

            // Canvasのサイズに合わせて全体を縮小（はみ出し防止）
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                float scaleY = (canvasRect.rect.height * 0.95f) / 750f;
                float scaleX = (canvasRect.rect.width * 0.95f) / 800f;
                float finalScale = Mathf.Min(scaleX, scaleY);
                // 画面が小さい時だけ縮小する
                if (finalScale < 1.0f)
                {
                    rect.localScale = new Vector3(finalScale, finalScale, 1f);
                }
            }

            Selection.activeGameObject = panelObj;
            Debug.Log("【成功】オプション画面のUIを生成しました！");
        }

        private static void CreateText(Transform parent, string name, string text, Vector2 pos, float size, TextAlignmentOptions align)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(250, 40);

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Color.white;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 pos)
        {
            GameObject obj = DefaultControls.CreateSlider(new DefaultControls.Resources());
            obj.name = name;
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(300, 30);
            return obj.GetComponent<Slider>();
        }

        private static Toggle CreateToggle(Transform parent, string name, Vector2 pos)
        {
            GameObject obj = DefaultControls.CreateToggle(new DefaultControls.Resources());
            obj.name = name;
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            
            // TextMeshProに置き換えたいが複雑になるため標準のテキストを削除
            Transform label = obj.transform.Find("Label");
            if (label != null) GameObject.DestroyImmediate(label.gameObject);

            return obj.GetComponent<Toggle>();
        }

        private static void CreatePanel(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.SetAsFirstSibling(); // テキストの背景にするため一番奥へ
            var rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var img = obj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        }

        private static Button CreateButton(Transform parent, string name, string text, Vector2 pos, Vector2 size)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var img = obj.AddComponent<Image>();
            img.color = new Color(0.8f, 0.2f, 0.2f); // 赤っぽいボタン

            var btn = obj.AddComponent<Button>();

            CreateText(obj.transform, "Text", text, Vector2.zero, 24, TextAlignmentOptions.Center);

            return btn;
        }
    }
}
