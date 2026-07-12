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
            rect.sizeDelta = new Vector2(800, 900);
            var img = panelObj.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.1f, 0.95f); // ダークグレー背景

            OptionUI optionUI = panelObj.AddComponent<OptionUI>();

            // タイトル
            CreateText(panelObj.transform, "OptionTitle", "OPTIONS", new Vector2(0, 400), 50, TextAlignmentOptions.Center);

            // BGM
            CreateText(panelObj.transform, "BgmText", "BGM Volume", new Vector2(-200, 250), 30, TextAlignmentOptions.Left);
            Slider bgmSlider = CreateSlider(panelObj.transform, "BgmSlider", new Vector2(100, 250));

            // SE
            CreateText(panelObj.transform, "SeText", "SE Volume", new Vector2(-200, 150), 30, TextAlignmentOptions.Left);
            Slider seSlider = CreateSlider(panelObj.transform, "SeSlider", new Vector2(100, 150));

            // Voice
            CreateText(panelObj.transform, "VoiceText", "Voice Volume", new Vector2(-200, 50), 30, TextAlignmentOptions.Left);
            Slider voiceSlider = CreateSlider(panelObj.transform, "VoiceSlider", new Vector2(100, 50));

            // High Speed Toggle
            CreateText(panelObj.transform, "SpeedText", "High Speed Mode", new Vector2(-200, -100), 30, TextAlignmentOptions.Left);
            Toggle speedToggle = CreateToggle(panelObj.transform, "HighSpeedToggle", new Vector2(200, -100));

            // Effect Toggle
            CreateText(panelObj.transform, "EffectText", "Show Effects", new Vector2(-200, -200), 30, TextAlignmentOptions.Left);
            Toggle effectToggle = CreateToggle(panelObj.transform, "EffectToggle", new Vector2(200, -200));

            // Buttons
            Button saveBtn = CreateButton(panelObj.transform, "SaveButton", "Save & Close", new Vector2(-150, -350));
            Button cancelBtn = CreateButton(panelObj.transform, "CancelButton", "Cancel", new Vector2(150, -350));

            // スクリプトにアタッチ
            SerializedObject serializedObject = new SerializedObject(optionUI);
            serializedObject.FindProperty("bgmSlider").objectReferenceValue = bgmSlider;
            serializedObject.FindProperty("seSlider").objectReferenceValue = seSlider;
            serializedObject.FindProperty("voiceSlider").objectReferenceValue = voiceSlider;
            serializedObject.FindProperty("highSpeedToggle").objectReferenceValue = speedToggle;
            serializedObject.FindProperty("effectToggle").objectReferenceValue = effectToggle;
            serializedObject.FindProperty("saveAndCloseButton").objectReferenceValue = saveBtn;
            serializedObject.FindProperty("closeButton").objectReferenceValue = cancelBtn;
            serializedObject.ApplyModifiedProperties();

            Selection.activeGameObject = panelObj;
            Debug.Log("【成功】オプション画面のUIを生成しました！");
        }

        private static void CreateText(Transform parent, string name, string text, Vector2 pos, float size, TextAlignmentOptions align)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(300, 60);

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
            rect.sizeDelta = new Vector2(400, 40);
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

        private static Button CreateButton(Transform parent, string name, string text, Vector2 pos)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(250, 80);

            var img = obj.AddComponent<Image>();
            img.color = new Color(0.8f, 0.2f, 0.2f); // 赤っぽいボタン

            var btn = obj.AddComponent<Button>();

            CreateText(obj.transform, "Text", text, Vector2.zero, 30, TextAlignmentOptions.Center);

            return btn;
        }
    }
}
