using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.UI;

namespace KillingMahjong.Editor
{
    public class FixContainerBackgrounds : EditorWindow
    {
        // 一時的に非表示。Player Settings の Scripting Define Symbols に KM_ALL_TOOLS を足すと戻る
#if KM_ALL_TOOLS
        [MenuItem("Tools/UI/邪魔な青ブロック(透明コンテナの背景)を削除")]
#endif
        public static void FixContainers()
        {
            // 本来透明であるべきUIコンテナのリスト
            System.Type[] containerTypes = new System.Type[] {
                typeof(EnemyHandUI),
                typeof(HandUI),
                typeof(WallUI),
                typeof(EnemyWallUI),
                typeof(WaitUI)
            };

            foreach (var type in containerTypes)
            {
                var objs = FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var obj in objs)
                {
                    Component comp = obj as Component;
                    if (comp != null)
                    {
                        // 1. コンテナ自身から余計な装飾を削除
                        RemoveDecorations(comp.gameObject);

                        // 2. コンテナの子要素（レイアウトグループを持つオブジェクト等）からも削除
                        foreach (Transform child in comp.transform)
                        {
                            // タイルなどのPrefabには影響させないよう、名前にContainerやLayoutが入っているもの、
                            // またはボタン等を持たないただのImageを対象にする
                            if (child.name.ToLower().Contains("container") || child.GetComponent<LayoutGroup>() != null)
                            {
                                RemoveDecorations(child.gameObject);
                            }
                        }

                        EditorUtility.SetDirty(comp.gameObject);
                    }
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[FixContainerBackgrounds] 邪魔な青ブロックの削除が完了しました！");
        }

        private static void RemoveDecorations(GameObject go)
        {
            // OutlineとShadowを削除
            foreach (var s in go.GetComponents<Shadow>()) DestroyImmediate(s, true);
            foreach (var o in go.GetComponents<Outline>()) DestroyImmediate(o, true);

            // コンテナにアタッチされたImageで、かつButton等の機能がない場合はImage自体を削除するか透明にする
            Image img = go.GetComponent<Image>();
            if (img != null && go.GetComponent<Button>() == null)
            {
                // Imageを消すとレイキャストが効かなくなる場合があるので、念のため透明(Color.clear)にする
                img.sprite = null;
                img.color = Color.clear;
                Debug.Log($"[FixContainerBackgrounds] {go.name} の背景を透明にしました。");
            }
        }
    }
}
