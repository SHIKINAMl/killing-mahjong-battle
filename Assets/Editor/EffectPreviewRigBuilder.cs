using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using KillingMahjong.UI;

namespace KillingMahjong.EditorTools
{
    /// <summary>本編の配置・画像・設定を借り、通信等を除いた演出用Prefabを作る。</summary>
    public static class EffectPreviewRigBuilder
    {
        public const string Path = "Assets/Resources/Presentation/EffectPreviewRig.prefab";

        [MenuItem("Tools/演出/コレクションの試写舞台を更新")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("再生を止めてから更新してください。");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/UIテストシーン.unity");
            try
            {
                var roots = scene.GetRootGameObjects();
                var ui = roots.SelectMany(r => r.GetComponentsInChildren<GameUIManager>(true)).First();
                var root = new GameObject("EffectPreviewRig");
                SceneManager.MoveGameObjectToScene(root, scene);
                foreach (var item in roots) item.transform.SetParent(root.transform, true);
                var rig = root.AddComponent<EffectPreviewRig>();
                rig.phase = ui.PhaseTransitionUI; rig.ron = ui.RonAnimationUI;
                rig.player = ui.PlayerInfoUI; rig.enemy = ui.EnemyInfoUI; rig.betting = ui.BettingUI;
                rig.blink = root.GetComponentInChildren<BlinkEffectUI>(true);
                rig.tiles = ui.TileResourceManager;
                rig.dialogue = ui.DialogueUI;
                rig.momentum = root.GetComponentInChildren<KillingMahjong.UI.Effects.MatchMomentumUI>(true);
                var enemyData = new SerializedObject(rig.enemy);
                rig.character = enemyData.FindProperty("characterRenderer").objectReferenceValue as SpriteRenderer;
                rig.face = enemyData.FindProperty("faceRenderer").objectReferenceValue as SpriteRenderer;
                rig.viewCamera = root.GetComponentInChildren<Camera>(true);
                rig.arrowArt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/TutorialArrow.png");
                rig.font = root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).First(t => t.font != null).font;
                var victory = root.GetComponentInChildren<VictoryUI>(true);
                var data = new SerializedObject(victory);
                rig.endings = ReadConfigs(data.FindProperty("configs"));
                rig.victoryEndings = ReadConfigs(data.FindProperty("victoryEndingConfigs"));
                // シーンの編集時はメニュー類が開いていてもよいが、試写には持ち込まない。
                var visibleRoots = new Component[] { rig.phase, rig.ron, rig.player, rig.enemy, rig.betting, rig.blink, rig.dialogue, rig.momentum }
                    .Where(c => c != null).Select(c => c.transform).ToArray();
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    bool backdrop = canvas.name == "マージャン卓" || canvas.name == "背景Canvas" || canvas.name == "HeartbeatEffect";
                    bool animation = visibleRoots.Any(t => t == canvas.transform || t.IsChildOf(canvas.transform) || canvas.transform.IsChildOf(t));
                    if (!backdrop && !animation) canvas.gameObject.SetActive(false);
                }
                if (rig.blink == null)
                {
                    var opening = EditorSceneManager.OpenPreviewScene("Assets/Scenes/OpeningScene.unity");
                    try
                    {
                        var source = opening.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BlinkEffectUI>(true)).First();
                        var copy = UnityEngine.Object.Instantiate(source.gameObject);
                        SceneManager.MoveGameObjectToScene(copy, scene);
                        copy.transform.SetParent(root.transform, false);
                        copy.SetActive(false);
                        rig.blink = copy.GetComponent<BlinkEffectUI>();
                    }
                    finally { EditorSceneManager.ClosePreviewScene(opening); }
                }
                // 許可リスト。ネットワーク、GameUIManager、戦績、シーン遷移、台本は持ち込まない。
                var keep = new[] { typeof(EffectPreviewRig), typeof(PhaseTransitionUI), typeof(RonAnimationUI),
                    typeof(PlayerInfoUI), typeof(EnemyInfoUI), typeof(BettingUI), typeof(BlinkEffectUI),
                    typeof(DialogueUI), typeof(KillingMahjong.UI.Effects.MatchMomentumUI),
                    typeof(KillingMahjong.UI.Effects.HeartbeatEffect) };
                // InputModule が EventSystem を要求するため、コンポーネント単位で順に消すと残る。
                foreach (var input in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true))
                    UnityEngine.Object.DestroyImmediate(input.gameObject);
                foreach (var item in root.GetComponentsInChildren<MonoBehaviour>(true)
                    .OrderBy(item => item is GameUIManager ? 1 : 0))
                {
                    if (item == null) continue;
                    var type = item.GetType();
                    if (keep.Contains(type) || type.Namespace == "UnityEngine.UI" || type.Namespace == "TMPro") continue;
                    UnityEngine.Object.DestroyImmediate(item);
                }
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
                    UnityEngine.Object.DestroyImmediate(listener);
                foreach (var item in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(item.gameObject);
                foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    button.interactable = false;
                if (root.GetComponentsInChildren<GameUIManager>(true).Length != 0)
                    throw new InvalidOperationException("試写舞台に対局処理が残っています。");
                root.SetActive(false);
                if (!AssetDatabase.IsValidFolder("Assets/Resources/Presentation"))
                    AssetDatabase.CreateFolder("Assets/Resources", "Presentation");
                PrefabUtility.SaveAsPrefabAsset(root, Path);
                Debug.Log("演出試写舞台を更新: " + Path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static VictoryConfig[] ReadConfigs(SerializedProperty array)
        {
            var result = new VictoryConfig[array.arraySize];
            for (int i = 0; i < result.Length; i++)
            {
                var item = array.GetArrayElementAtIndex(i);
                var pages = item.FindPropertyRelative("dialoguePages");
                result[i] = new VictoryConfig {
                    victoryType = (VictoryType)item.FindPropertyRelative("victoryType").enumValueIndex,
                    image = item.FindPropertyRelative("image").objectReferenceValue as Sprite,
                    text = item.FindPropertyRelative("text").stringValue,
                    dialoguePages = Enumerable.Range(0, pages.arraySize).Select(n => pages.GetArrayElementAtIndex(n).stringValue).ToArray(),
                    resultTitle = item.FindPropertyRelative("resultTitle").stringValue,
                    endingName = item.FindPropertyRelative("endingName").stringValue };
            }
            return result;
        }
    }
}
