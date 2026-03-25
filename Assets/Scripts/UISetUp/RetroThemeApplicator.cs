#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KillingMahjong.UISetUp
{

    public class RetroThemeApplicator : MonoBehaviour
    {
        [Header("Retro Theme Colors")]
        [Tooltip("ダイアログやパネルの背景色（例: Windows風のダークブルー）")]
        public Color windowBackgroundColor = new Color(0f, 0f, 0.5f, 0.95f); 
        
        [Tooltip("ボタンの背景色（例: レトロなグレーや白）")]
        public Color buttonBackgroundColor = new Color(0.8f, 0.8f, 0.8f, 1f); 
        
        [Tooltip("ボタンのテキスト色")]
        public Color buttonTextColor = Color.black;
        
        [Tooltip("通常のテキスト色（画面内の白文字など）")]
        public Color generalTextColor = Color.white;
        
        [Header("Font Settings")]
        [Tooltip("プロジェクト内のピクセルフォント（SDFアセット）のパス")]
        public string fontAssetPath = "Assets/Resources/PixelMplus-20130602/PixelMplus-20130602/PixelMplus10-Bold SDF.asset";

        [ContextMenu("Apply Retro Theme to Current Canvas")]
        public void ApplyTheme()
        {
#if UNITY_EDITOR
            TMP_FontAsset retroFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
            if (retroFont == null)
            {
                Debug.LogError($"[RetroThemeApplicator] Font asset not found at {fontAssetPath}. パスが正しいか確認してください。");
                // 処理を続行しますが、フォントは変更されません。
            }

            // シーン上の全てのテキストと画像を取得
            var allImages = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var allTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // --- 変更をUndo（Ctrl+Z）できるように記録 ---
            Undo.RecordObjects(allImages, "Apply Retro Theme to Images");
            Undo.RecordObjects(allTexts, "Apply Retro Theme to Texts");

            // 0. 全てのCanvasを強制的に「Screen Space - Camera」に変更（15個あっても一瞬で終わらせるため）
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Undo.RecordObjects(allCanvases, "Change Canvas Render Modes");
                foreach (var canvas in allCanvases)
                {
                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    }
                    // すでにCameraモードになっているか、今回変更されたもの全てに実行
                    if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                    {
                        canvas.worldCamera = mainCam;
                        canvas.planeDistance = 0.5f; // 手前に強制配置
                    }
                }
            }
            else
            {
                Debug.LogWarning("[RetroThemeApplicator] Main Cameraが見つからなかったため、Canvasの設定自動化はスキップされました。");
            }

            // 1. テキスト（TextMeshProUGUI）のフォントと色を変更
            foreach (var txt in allTexts)
            {
                if (retroFont != null)
                {
                    txt.font = retroFont;
                }
                
                // ボタンの小オブジェクトなら文字色を黒に、それ以外は白にする
                if (txt.GetComponentInParent<Button>() != null)
                {
                    txt.color = buttonTextColor;
                    // ピクセルフォントが見やすくなるように少し大きめにする
                    if (txt.fontSize < 20) txt.fontSize = 20;
                }
                else
                {
                    txt.color = generalTextColor;
                }
            }

            // 2. 画像（Image）とボタン（Button）の色・Spriteを変更
            foreach (var img in allImages)
            {
                Button btn = img.GetComponent<Button>();
                if (btn != null)
                {
                    // ボタンの場合：丸みを消して四角いレトロなブロック感を出すためにSpriteをnullにする
                    img.sprite = null; 
                    img.color = buttonBackgroundColor;
                }
                else
                {
                    // 背景パネルらしきものをダークブルーにする（名前で簡易判定）
                    bool isPanel = img.name.ToLower().Contains("panel") || 
                                   img.name.ToLower().Contains("background") ||
                                   img.name.ToLower().Contains("dialogue") ||
                                   img.name.ToLower().Contains("window");
                                   
                    if (isPanel)
                    {
                        img.sprite = null;
                        img.color = windowBackgroundColor;
                    }
                }
            }

            Debug.Log("[RetroThemeApplicator] 適用完了しました！ Scene/Gameビューを確認してください。");
            
            // シーンの変更をUnityに認識させる
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
#else
            Debug.LogWarning("この処理はUnityエディタ内でのみ実行可能です。");
#endif
        }
    }
}
