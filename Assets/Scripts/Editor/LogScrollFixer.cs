using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace KillingMahjong.Editor
{
    public class LogScrollFixer : EditorWindow
    {
        [MenuItem("Tools/UI/ログ画面のスクロールを修正")]
        public static void FixLogScroll()
        {
            // DialogueUIを探す
            var dialogueUI = Object.FindAnyObjectByType<KillingMahjong.UI.DialogueUI>(FindObjectsInactive.Include);
            if (dialogueUI == null)
            {
                Debug.LogError("[LogScrollFixer] DialogueUIが見つかりません。");
                return;
            }

            // privateなlogContainerをリフレクションで取得
            var logContainerField = typeof(KillingMahjong.UI.DialogueUI).GetField("logContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (logContainerField == null) return;

            Transform logContainer = logContainerField.GetValue(dialogueUI) as Transform;

            if (logContainer == null)
            {
                Debug.LogError("[LogScrollFixer] logContainerが設定されていません。");
                return;
            }

            Undo.RecordObject(logContainer.gameObject, "Fix Log Container");

            // 1. ContentSizeFitterの追加（子要素の数に合わせて縦に伸びるようにする）
            ContentSizeFitter fitter = logContainer.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = Undo.AddComponent<ContentSizeFitter>(logContainer.gameObject);
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // 2. VerticalLayoutGroupの追加と設定（子要素を縦に綺麗に並べる）
            VerticalLayoutGroup layout = logContainer.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = Undo.AddComponent<VerticalLayoutGroup>(logContainer.gameObject);
            layout.childControlHeight = false; // 高さは子要素自身に任せる
            layout.childControlWidth = true;   // 幅は親に合わせる
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 5f;

            // 3. ScrollRectとViewportの設定
            ScrollRect scrollRect = logContainer.GetComponentInParent<ScrollRect>(true);
            if (scrollRect != null)
            {
                Undo.RecordObject(scrollRect.gameObject, "Fix ScrollRect");
                scrollRect.horizontal = false; // 横スクロールを無効化
                scrollRect.vertical = true;
                scrollRect.content = logContainer.GetComponent<RectTransform>();

                Transform viewport = scrollRect.viewport;
                if (viewport != null)
                {
                    // 枠外にはみ出た文字を切り取る（マスクする）
                    RectMask2D mask = viewport.GetComponent<RectMask2D>();
                    if (mask == null) Undo.AddComponent<RectMask2D>(viewport.gameObject);
                }
            }

            EditorUtility.SetDirty(dialogueUI.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[LogScrollFixer] ログ画面のスクロール設定を修正しました！");
        }
    }
}
