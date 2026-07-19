#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace KillingMahjong.Editor
{
    public class RemoveMissingScripts
    {
        [MenuItem("Tools/Remove Missing Scripts")]
        public static void CleanupMissingScripts()
        {
            var gameObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int totalRemoved = 0;
            int objectsAffected = 0;

            foreach (var go in gameObjects)
            {
                // Prefabのインスタンスかどうか確認
                bool isPrefab = PrefabUtility.IsPartOfAnyPrefab(go);
                
                // GameObjectUtilityを使って安全に削除
                int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                if (count > 0)
                {
                    totalRemoved += count;
                    objectsAffected++;
                    
                    if (isPrefab)
                    {
                        // プレハブの場合、変更を適用する
                        PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                    }
                    else
                    {
                        EditorUtility.SetDirty(go);
                    }
                }
            }

            if (totalRemoved > 0)
            {
                Debug.Log($"[Cleanup] {totalRemoved} 個の Missing スクリプトを {objectsAffected} 個のオブジェクトから削除しました。シーンを保存してください。");
            }
            else
            {
                Debug.Log("[Cleanup] Missing スクリプトは見つかりませんでした。");
            }
        }
    }
}
#endif
