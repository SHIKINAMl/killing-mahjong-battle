using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using KillingMahjong.UI;

namespace KillingMahjong.Editor
{
    public class OptionUICreator : MonoBehaviour
    {
        [MenuItem("Tools/UI/OptionUIプレハブにすりガラスを適用")]
        public static void ApplyBlurToPrefab()
        {
            string prefabPath = "Assets/Prefabs/OptionUI.prefab";
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError("Prefab not found at " + prefabPath);
                return;
            }
            
            Material blurMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UIGlassmorphismBlurMat.mat");
            if (blurMat == null)
            {
                Shader blurShader = Shader.Find("UI/StylishPattern");
                if (blurShader != null)
                {
                    blurMat = new Material(blurShader);
                    if (!System.IO.Directory.Exists("Assets/Materials")) System.IO.Directory.CreateDirectory("Assets/Materials");
                    AssetDatabase.CreateAsset(blurMat, "Assets/Materials/UIGlassmorphismBlurMat.mat");
                    AssetDatabase.SaveAssets();
                }
            }

            Image img = prefabRoot.GetComponent<Image>();
            if (img != null && blurMat != null)
            {
                img.material = blurMat;
                img.color = new Color(0.1f, 0.1f, 0.1f, 0.95f); // 背景の基本色を濃く（ほぼ黒に）戻す
                EditorUtility.SetDirty(prefabRoot);
                PrefabUtility.SavePrefabAsset(prefabRoot);
                Debug.Log("【成功】Prefabs/OptionUI にスタイリッシュな斜線パターンを適用しました！");
            }
            else
            {
                Debug.LogError($"Failed to apply blur. Image is null: {img == null}, Material is null: {blurMat == null}");
            }
        }

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
            rect.sizeDelta = new Vector2(800, 700); // 800x700に拡大
            var img = panelObj.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.1f, 0.6f); // ガラス感が強く出るように透明度を下げる

            // すりガラスマテリアルの適用
            Material blurMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UIGlassmorphismBlurMat.mat");
            if (blurMat == null)
            {
                Shader blurShader = Shader.Find("UI/StylishPattern");
                if (blurShader != null)
                {
                    blurMat = new Material(blurShader);
                    if (!System.IO.Directory.Exists("Assets/Materials")) System.IO.Directory.CreateDirectory("Assets/Materials");
                    AssetDatabase.CreateAsset(blurMat, "Assets/Materials/UIGlassmorphismBlurMat.mat");
                    AssetDatabase.SaveAssets();
                }
            }
            if (blurMat != null)
            {
                img.material = blurMat;
            }

            OptionUI optionUI = panelObj.AddComponent<OptionUI>();

            // タイトル
            CreateText(panelObj.transform, "OptionTitle", "OPTIONS", new Vector2(0, 300), KillingMahjong.Common.UITypography.BodyLarge, TextAlignmentOptions.Center);

            // 背景枠（Audio）
            CreatePanel(panelObj.transform, "AudioPanel", new Vector2(0, 150), new Vector2(750, 200));
            // BGM
            CreateText(panelObj.transform, "BgmText", "BGM Volume", new Vector2(-200, 210), KillingMahjong.Common.UITypography.BodySmall, TextAlignmentOptions.Left);
            Slider bgmSlider = CreateSlider(panelObj.transform, "BgmSlider", new Vector2(100, 210));
            // SE
            CreateText(panelObj.transform, "SeText", "SE Volume", new Vector2(-200, 150), KillingMahjong.Common.UITypography.BodySmall, TextAlignmentOptions.Left);
            Slider seSlider = CreateSlider(panelObj.transform, "SeSlider", new Vector2(100, 150));
            // Voice
            CreateText(panelObj.transform, "VoiceText", "Voice Volume", new Vector2(-200, 90), KillingMahjong.Common.UITypography.BodySmall, TextAlignmentOptions.Left);
            Slider voiceSlider = CreateSlider(panelObj.transform, "VoiceSlider", new Vector2(100, 90));

            // 背景枠（Game / Window Settings）
            CreatePanel(panelObj.transform, "GamePanel", new Vector2(0, -70), new Vector2(750, 230));
            
            // High Speed Toggle
            CreateText(panelObj.transform, "SpeedText", "High Speed Mode", new Vector2(-200, -10), KillingMahjong.Common.UITypography.BodySmall, TextAlignmentOptions.Left);
            Toggle speedToggle = CreateToggle(panelObj.transform, "HighSpeedToggle", new Vector2(200, -10));
            
            // Effect Toggle
            CreateText(panelObj.transform, "EffectText", "Show Effects", new Vector2(-200, -60), KillingMahjong.Common.UITypography.BodySmall, TextAlignmentOptions.Left);
            Toggle effectToggle = CreateToggle(panelObj.transform, "EffectToggle", new Vector2(200, -60));

            // Fullscreen Toggle
            CreateText(panelObj.transform, "FullscreenText", "Fullscreen Mode", new Vector2(-200, -110), KillingMahjong.Common.UITypography.BodySmall, TextAlignmentOptions.Left);
            Toggle fullscreenToggle = CreateToggle(panelObj.transform, "FullscreenToggle", new Vector2(200, -110));

            // Resolution Dropdown
            CreateText(panelObj.transform, "ResolutionText", "Window Resolution", new Vector2(-200, -160), KillingMahjong.Common.UITypography.BodySmall, TextAlignmentOptions.Left);
            var resolutionOptions = new System.Collections.Generic.List<string> { "1920x1080", "1600x900", "1280x720", "1024x576", "800x600" };
            TMP_Dropdown resolutionDropdown = CreateDropdown(panelObj.transform, "ResolutionDropdown", new Vector2(100, -160), new Vector2(240, 40), resolutionOptions);

            // Buttons (System)
            Button returnBtn = CreateButton(panelObj.transform, "ReturnButton", "Title", new Vector2(-250, -280), new Vector2(180, 50));
            Button quitBtn = CreateButton(panelObj.transform, "QuitButton", "Quit", new Vector2(-50, -280), new Vector2(180, 50));

            // Buttons (Save/Close)
            Button saveBtn = CreateButton(panelObj.transform, "SaveButton", "Save & Close", new Vector2(200, -280), new Vector2(220, 50));
            Button cancelBtn = CreateButton(panelObj.transform, "CancelButton", "X", new Vector2(360, 310), new Vector2(50, 50));

            // スクリプトにアタッチ
            SerializedObject serializedObject = new SerializedObject(optionUI);
            serializedObject.FindProperty("bgmSlider").objectReferenceValue = bgmSlider;
            serializedObject.FindProperty("seSlider").objectReferenceValue = seSlider;
            serializedObject.FindProperty("voiceSlider").objectReferenceValue = voiceSlider;
            serializedObject.FindProperty("highSpeedToggle").objectReferenceValue = speedToggle;
            serializedObject.FindProperty("effectToggle").objectReferenceValue = effectToggle;
            serializedObject.FindProperty("resolutionDropdown").objectReferenceValue = resolutionDropdown;
            serializedObject.FindProperty("fullscreenToggle").objectReferenceValue = fullscreenToggle;
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

            CreateText(obj.transform, "Text", text, Vector2.zero, KillingMahjong.Common.UITypography.BodySmall, TextAlignmentOptions.Center);

            return btn;
        }

        private static TMP_Dropdown CreateDropdown(Transform parent, string name, Vector2 pos, Vector2 size, System.Collections.Generic.List<string> options)
        {
            EditorApplication.ExecuteMenuItem("GameObject/UI/Dropdown - TextMeshPro");
            GameObject obj = Selection.activeGameObject;
            if (obj != null)
            {
                obj.name = name;
                obj.transform.SetParent(parent, false);
                RectTransform rect = obj.GetComponent<RectTransform>();
                rect.anchoredPosition = pos;
                rect.sizeDelta = size;
                
                TMP_Dropdown dropdown = obj.GetComponent<TMP_Dropdown>();
                if (dropdown != null)
                {
                    dropdown.ClearOptions();
                    dropdown.AddOptions(options);
                }

                var label = obj.transform.Find("Label");
                if (label != null)
                {
                    var tmp = label.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) tmp.fontSize = KillingMahjong.Common.UITypography.BodySmall;
                }
                
                return dropdown;
            }
            return null;
        }
    }
}
