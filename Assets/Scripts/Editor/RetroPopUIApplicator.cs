using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KillingMahjong.Editor
{
    public class RetroPopUIApplicator : EditorWindow
    {
        // カラー定義
        private readonly Color colorDarkRed = new Color32(80, 0, 15, 255);       // 深いワインレッド（元ネイビーブルー）
        private readonly Color colorRed = new Color32(194, 39, 45, 255);       // ディープレッド
        private readonly Color colorCream = new Color32(245, 245, 220, 255);     // クリーム色（オフホワイト）
        private readonly Color colorDarkShadow = new Color32(26, 26, 26, 200);   // ドロップシャドウ用の濃い黒

        private string fontAssetPath = "Assets/Resources/PixelMplus-20130602/PixelMplus-20130602/PixelMplus10-Bold SDF.asset";

        // 一時的に非表示。Player Settings の Scripting Define Symbols に KM_ALL_TOOLS を足すと戻る
#if KM_ALL_TOOLS
        [MenuItem("Tools/UI/レトロポップ風スタイルを一括適用 (Retro Pop)")]
#endif
        public static void ShowWindow()
        {
            GetWindow<RetroPopUIApplicator>("Retro Pop UI");
        }

        private void OnGUI()
        {
            GUILayout.Label("レトロポップ・カジノ風 UI適用ツール", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox("シーン内のUI（ボタン、パネル、テキスト）を画像のようなポップでレトロな雰囲気に一括変更します。\n\n・ボタン: 赤とネイビー交互\n・パネル: ネイビー\n・縁取り: クリーム色\n・レイアウト: 少しだけ斜めに回転", MessageType.Info);

            EditorGUILayout.Space();

            if (GUILayout.Button("選択中のオブジェクト（と子要素）に適用", GUILayout.Height(40)))
            {
                ApplyRetroPopStyle();
            }
        }

        private void ApplyRetroPopStyle()
        {
            TMP_FontAsset retroFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
            if (retroFont == null)
            {
                Debug.LogWarning($"[RetroPopUI] フォントが見つかりません: {fontAssetPath}");
            }

            // --- 0. 選択中のオブジェクトから対象を取得 ---
            var allImages = new System.Collections.Generic.List<Image>();
            var allTexts = new System.Collections.Generic.List<TextMeshProUGUI>();
            var allAbilityItems = new System.Collections.Generic.List<KillingMahjong.UI.AbilityItemUI>();

            if (Selection.gameObjects.Length == 0)
            {
                Debug.LogWarning("対象のオブジェクトが選択されていません。ヒエラルキーで適用したいUIオブジェクトを選択してください。");
                return;
            }

            foreach (GameObject obj in Selection.gameObjects)
            {
                allImages.AddRange(obj.GetComponentsInChildren<Image>(true));
                allTexts.AddRange(obj.GetComponentsInChildren<TextMeshProUGUI>(true));
                allAbilityItems.AddRange(obj.GetComponentsInChildren<KillingMahjong.UI.AbilityItemUI>(true));
            }

            int buttonIndex = 0;

            // --- 1. AbilityItemUIの処理 (左パネルのスキルなど) ---
            foreach (var ability in allAbilityItems)
            {
                var so = new SerializedObject(ability);
                so.FindProperty("normalColor").colorValue = colorDarkRed;
                so.FindProperty("selectedColor").colorValue = colorRed;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(ability.gameObject);
            }

            // --- 2. テキストの適用 ---
            foreach (var txt in allTexts)
            {
                if (retroFont != null) txt.font = retroFont;
                
                txt.color = colorCream; // テキストはすべてクリーム色に統一
                
                // OutlineとShadowをクリアして再設定
                foreach (var s in txt.GetComponents<Shadow>()) DestroyImmediate(s, true);
                
                Shadow shadow = txt.gameObject.AddComponent<Shadow>();
                shadow.effectColor = colorDarkShadow;
                shadow.effectDistance = new Vector2(2f, -2f);

                EditorUtility.SetDirty(txt.gameObject);
            }

            // --- 3. 画像（パネル・ボタン）の適用 ---
            foreach (var img in allImages)
            {
                Button btn = img.GetComponent<Button>();
                KillingMahjong.UI.AbilityItemUI ability = img.GetComponent<KillingMahjong.UI.AbilityItemUI>();
                bool isPanel = img.name.ToLower().Contains("panel") || 
                               img.name.ToLower().Contains("dialogue") ||
                               img.name.ToLower().Contains("window") ||
                               img.name.ToLower().Contains("background");

                if (btn != null || ability != null || isPanel)
                {
                    img.sprite = null; 

                    if (btn != null)
                    {
                        img.color = (buttonIndex % 2 == 0) ? colorRed : colorDarkRed;
                        buttonIndex++;
                    }
                    else if (isPanel || ability != null)
                    {
                        img.color = colorDarkRed;
                    }

                    // 既存のShadow/Outlineを全削除（重複や描画順のバグを防ぐため）
                    foreach (var s in img.GetComponents<Shadow>()) DestroyImmediate(s, true);

                    // 先にOutlineを追加
                    Outline outline = img.gameObject.AddComponent<Outline>();
                    outline.effectColor = colorCream;
                    outline.useGraphicAlpha = true;

                    // その後にShadowを追加（Outlineごとドロップシャドウさせる）
                    Shadow shadow = img.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = colorDarkShadow;

                    // 斜め回転をリセット（水平を維持）
                    RectTransform rt = img.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.localRotation = Quaternion.identity;
                        
                        // オブジェクトのサイズに合わせて枠線や影の太さを自動調整する（小さなボタンが潰れないように）
                        float minSize = Mathf.Min(rt.rect.width, rt.rect.height);
                        float scale = Mathf.Clamp(minSize / 100f, 0.3f, 1f); // 最小でも通常の0.3倍の太さ

                        outline.effectDistance = new Vector2(3f * scale, -3f * scale);
                        shadow.effectDistance = new Vector2(6f * scale, -6f * scale);
                    }

                    EditorUtility.SetDirty(img.gameObject);
                }
            }

            AssetDatabase.SaveAssets(); // Prefabの変更を保存
            Debug.Log("[RetroPopUIApplicator] レトロポップ風スタイルの適用が完了しました！（Prefabも含めて更新）");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }
}
