using UnityEditor;
using UnityEngine;
using KillingMahjong.UI;

namespace KillingMahjong.EditorTools
{
    /// <summary>通常対局の牌と配置設定だけを、独立した試写盤面へ保存する。</summary>
    public static class TutorialReplayBoardBuilder
    {
        [MenuItem("Tools/演出/流局動画の盤面を更新")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("再生を止めてから更新してください。");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/Scenes/UIテストシーン.unity");
            try
            {
                var source = System.Linq.Enumerable.First(System.Linq.Enumerable.SelectMany(scene.GetRootGameObjects(),
                    root => root.GetComponentsInChildren<GameUIManager>(true)));
                Save(source);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }

        public static void Save(GameUIManager source)
        {
            var board = Create(source, null);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(board.gameObject, source.gameObject.scene);
            PrefabUtility.SaveAsPrefabAsset(board.gameObject, "Assets/Resources/Presentation/TutorialReplayBoard.prefab");
            Object.DestroyImmediate(board.gameObject);
        }

        public static TutorialReplayBoard Create(GameUIManager source, Transform parent)
        {
            var root = new GameObject("ReplayBoardTemplate", typeof(RectTransform), typeof(TutorialReplayBoard));
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(800, 600);
            var board = root.GetComponent<TutorialReplayBoard>();
            board.tilePrefab = source.TilePrefab;
            board.localRiver = Copy(source.RiverUI, root.transform, "LocalRiver", "riverContainer");
            board.enemyRiver = Copy(source.EnemyRiverUI, root.transform, "EnemyRiver", "riverContainer");
            board.wallUI = Copy(source.WallUI, root.transform, "Wall", "wallContainer");
            board.enemyWallUI = Copy(source.EnemyWallUI, root.transform, "EnemyWall", "enemyWallContainer");
            board.enemyHandUI = Copy(source.EnemyHandUI, root.transform, "EnemyHand", "handSlotContainer");
            board.enemyRiver.AlignToOpponentRiver(board.localRiver);
            foreach (var river in new[] { board.localRiver, board.enemyRiver })
            {
                var data = new SerializedObject(river);
                data.FindProperty("turnText").objectReferenceValue = null;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var handRoot = new GameObject("Hand", typeof(RectTransform), typeof(HandBaseUI));
            handRoot.transform.SetParent(root.transform, false);
            Stretch((RectTransform)handRoot.transform);
            board.handUI = handRoot.GetComponent<HandBaseUI>();
            var original = new SerializedObject(source.HandUI);
            var hand = new SerializedObject(board.handUI);
            foreach (var name in new[] { "tileResourceManager", "tileSpacingX", "tileSpacingY", "animationSpeed" })
                hand.CopyFromSerializedProperty(original.FindProperty(name));
            var container = (RectTransform)original.FindProperty("handSlotContainer").objectReferenceValue;
            hand.FindProperty("handSlotContainer").objectReferenceValue = CopyPath(container, source.HandUI.transform, handRoot.transform);
            hand.ApplyModifiedPropertiesWithoutUndo();
            return board;
        }

        private static T Copy<T>(T source, Transform parent, string name, string containerName) where T : MonoBehaviour
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var copy = go.AddComponent<T>();
            EditorUtility.CopySerialized(source, copy);
            var original = new SerializedObject(source);
            var container = (RectTransform)original.FindProperty(containerName).objectReferenceValue;
            var data = new SerializedObject(copy);
            data.FindProperty(containerName).objectReferenceValue = CopyPath(container, source.transform, go.transform);
            data.ApplyModifiedPropertiesWithoutUndo();
            return copy;
        }

        private static RectTransform CopyPath(RectTransform source, Transform sourceRoot, Transform destination)
        {
            if (source == sourceRoot) return (RectTransform)destination;
            var parent = source.parent == sourceRoot ? destination
                : CopyPath((RectTransform)source.parent, sourceRoot, destination);
            var rect = (RectTransform)new GameObject(source.name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = source.anchorMin; rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot; rect.sizeDelta = source.sizeDelta;
            rect.anchoredPosition = source.anchoredPosition;
            rect.localScale = source.localScale; rect.localRotation = source.localRotation;
            var layout = source.GetComponent<UnityEngine.UI.LayoutGroup>();
            if (layout != null) EditorUtility.CopySerialized(layout, rect.gameObject.AddComponent(layout.GetType()));
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
