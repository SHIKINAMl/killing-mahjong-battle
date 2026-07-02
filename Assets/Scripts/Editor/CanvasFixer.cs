using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace KillingMahjong.Editor
{
    public class CanvasFixer
    {
        [MenuItem("Tools/UI/全てのCanvasを 800x600 (Scale With Screen Size) に修正")]
        public static void FixAllCanvases()
        {
            // 1. 現在のシーン内の全てのCanvasScalerを取得
            CanvasScaler[] sceneScalers = Object.FindObjectsOfType<CanvasScaler>(true);
            int count = 0;

            foreach (var scaler in sceneScalers)
            {
                Undo.RecordObject(scaler, "Fix Canvas Scaler");
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800, 600);
                PrefabUtility.RecordPrefabInstancePropertyModifications(scaler);
                count++;
            }

            // 2. プロジェクト内の全プレハブを取得して修正
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                
                // パッケージ内のプレハブ（読み取り専用）はスキップする
                if (!path.StartsWith("Assets/")) continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    CanvasScaler[] prefScalers = prefab.GetComponentsInChildren<CanvasScaler>(true);
                    if (prefScalers.Length > 0)
                    {
                        foreach (var scaler in prefScalers)
                        {
                            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                            scaler.referenceResolution = new Vector2(800, 600);
                            EditorUtility.SetDirty(scaler);
                            count++;
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Canvas Fixer] 合計 {count} 個の CanvasScaler を「Scale With Screen Size (800x600)」に設定しました！");
        }
    }
}
