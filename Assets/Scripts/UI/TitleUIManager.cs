using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    public class TitleUIManager : MonoBehaviour
    {
        private const string TutorialSceneName = "OpeningScene";
        private const string TutorialReturnToRoomKey = "Title_ReturnToRoom";

        [Header("遷移先のシーン名")]
        [SerializeField] private string nextSceneName = "UIテストシーン"; // 実際のメインゲームのシーン名に合わせてください

        [Header("設定画面パネル")]
        [SerializeField] private GameObject optionUIPanel;

        /// <summary>タイトルの表示差し替えが完了したことを、撮影や自動確認から読めるようにする。</summary>
        public bool PresentationApplied { get; private set; }

        private void Start()
        {
            ApplyTitlePresentation();
            PresentationApplied = true;

            // チュートリアルの完走／スキップ後だけは、入口へ戻すのではなく
            // 直前にいた部屋へ帰す。起動時に一度だけ消費する印にする。
            if (PlayerPrefs.GetInt(TutorialReturnToRoomKey, 0) != 0)
            {
                PlayerPrefs.DeleteKey(TutorialReturnToRoomKey);
                PlayerPrefs.Save();
                OnClickStartButton();
            }
        }

        private TitleMultiMenuUI multiMenu;
        private RoomScreenUI roomScreen;

        /// <summary>
        /// 「ゲーム開始」が押された時の処理。
        ///
        /// **シーンの `onClick` はこのメソッド名で配線済みなので、名前は変えないこと。**
        /// 作り直すと配線が外れて「押しても何も起きない」状態になる。
        /// 中身だけを差し替えて、まず部屋の待機画面を開く。
        /// </summary>
        public void OnClickStartButton()
        {
            if (roomScreen == null)
            {
                roomScreen = gameObject.GetComponent<RoomScreenUI>();
                if (roomScreen == null) roomScreen = gameObject.AddComponent<RoomScreenUI>();
            }

            SetTitlePresentationVisible(false);
            roomScreen.Open(OpenMatchMenu, OpenRoomTutorial, OpenRoomOptions, ExitGameFromRoom, ReturnToTitle);
        }

        private GameObject truthNameHook;

        private void Update()
        {
            if (roomScreen == null || !roomScreen.IsOpen) return;

            // OptionUI は既存 Canvas に置かれている。部屋の専用 Canvas より奥にあるため、
            // 設定中だけ部屋の絵を畳み、閉じたら元の待機画面を戻す。
            bool isOptionOpen = optionUIPanel != null && optionUIPanel.activeInHierarchy;
            roomScreen.SetContentVisible(!isOptionOpen);

            // TitleMultiMenuUI.Close() はタイトル用のコピーを再表示するため、部屋に戻った
            // 次フレームで必ず隠す。これにより「もどる」から待機画面へ自然に帰れる。
            if (truthNameHook == null)
            {
                truthNameHook = FindSceneObjectIncludingInactive("TruthNameHook");
            }
            if (truthNameHook != null && truthNameHook.activeSelf)
            {
                truthNameHook.SetActive(false);
            }
        }

        private void OpenMatchMenu()
        {
            if (multiMenu == null)
            {
                multiMenu = gameObject.AddComponent<TitleMultiMenuUI>();
            }

            multiMenu.Open(mode =>
            {
                Debug.Log($"対局開始（{mode}）。{nextSceneName} に遷移します。");
                if (roomScreen != null) roomScreen.SetContentVisible(false);
                StartMultiplayScene();
            });
        }

        private void OpenRoomOptions()
        {
            if (roomScreen != null) roomScreen.SetContentVisible(false);
            OnClickOptionButton();
        }

        private void OpenRoomTutorial()
        {
            if (roomScreen == null) return;

            roomScreen.OpenTutorialChoice(TutorialManager.GetSavedProgress(), StartTutorialScene);
        }

        private void StartTutorialScene(int roundIndex)
        {
            // OpeningScene は冒頭演出の後に StartTutorial() を呼ぶ。
            // シーンをまたいで「最初から／続きから」の選択を渡すための一回限りの要求。
            TutorialManager.RequestStartFrom(roundIndex);
            PlayerPrefs.SetInt(TutorialReturnToRoomKey, 1);
            PlayerPrefs.Save();

            if (roomScreen != null) roomScreen.SetContentVisible(false);
            StartScene(TutorialSceneName);
        }

        private void ReturnToTitle()
        {
            if (multiMenu != null) multiMenu.Close();
            if (roomScreen != null) roomScreen.Close();
            SetTitlePresentationVisible(true);
        }

        private void ExitGameFromRoom()
        {
            OnClickExitButton();
        }

        /// <summary>
        /// タイトルを「ソロ／マルチ」の区分ではなく、対局の入口として見せる。
        /// 既存 Button の onClick 配線はシーンに保存されているため、Button を作り直さず
        /// 実行時に表示だけを置き換える。
        /// </summary>
        private static void ApplyTitlePresentation()
        {
            var labels = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            TMP_FontAsset font = null;

            // "設定" など同名の文言は既存の OptionUI にもあり得る。
            // タイトルの3ボタンだけを整理するため、通常はタイトルのメニュー根の配下だけを見る。
            var titleMenuRoot = FindSceneObjectIncludingInactive("ボタン達");
            var titleMenuLabels = titleMenuRoot != null
                ? titleMenuRoot.GetComponentsInChildren<TextMeshProUGUI>(true)
                : labels;

            foreach (var label in labels)
            {
                if (label == null) continue;
                if (font == null && label.font != null) font = label.font;
            }

            foreach (var label in titleMenuLabels)
            {
                if (label == null) continue;
                string text = label.text.Trim();
                if (text == "ソロ" || text == "設定" || text == "やめる")
                {
                    var button = label.GetComponentInParent<Button>();
                    if (button != null) button.gameObject.SetActive(false);
                }
                else if (text == "マルチ")
                {
                    label.text = "ゲーム開始";
                }
            }

            // キャッチコピー「彼女の真名を、探しだせ。」はユーザーの指示で外した（2026-09-05）。
            // シーンには保存されておらず、ここで実行時に作っていただけなので、作らなければ出ない。
            // 戻すときはこの位置に TruthNameHook を作り直すこと（旧: 中央から +172, +92 / 390x42 / 22pt）。
        }

        private static void SetTitlePresentationVisible(bool visible)
        {
            string[] titleOnlyObjects =
            {
                "TitleLogo",
                "女の子",
                "女の子_Silhouette (白フチ)",
                "タイトル絵",
                "TitleScrim",
                "ボタン達",
                "Sparkle0",
                "Sparkle1",
                "Sparkle2",
                "Sparkle3",
                "Sparkle4"
            };

            foreach (string objectName in titleOnlyObjects)
            {
                // GameObject.Find は非アクティブになったタイトル要素を探せない。
                // 部屋から戻るときも確実に復帰できるよう、シーン内の非アクティブ要素を含めて探す。
                var target = FindSceneObjectIncludingInactive(objectName);
                if (target != null) target.SetActive(visible);
            }
        }

        private static GameObject FindSceneObjectIncludingInactive(string objectName)
        {
            var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var candidate in transforms)
            {
                if (candidate != null && candidate.name == objectName && candidate.gameObject.scene.IsValid())
                {
                    return candidate.gameObject;
                }
            }

            return null;
        }

        /// <summary>
        /// 対局シーンへ移る。`MatchJoinRequest` は呼ぶ前に設定しておくこと
        /// （`join` は接続直後に自動で飛ぶので、シーンに入ってからでは間に合わない）。
        /// </summary>
        private void StartMultiplayScene()
        {
            StartScene(nextSceneName);
        }

        private void StartScene(string sceneName)
        {
            if (KillingMahjong.UI.LoadingManager.Instance != null)
            {
                KillingMahjong.UI.LoadingManager.Instance.FadeOutScreen(() => 
                {
                    StartCoroutine(LoadSceneAsyncCoroutine(sceneName));
                });
            }
            else
            {
                StartCoroutine(LoadSceneAsyncCoroutine(sceneName));
            }
        }

        private System.Collections.IEnumerator LoadSceneAsyncCoroutine(string sceneName)
        {
            // 暗転完了後、非同期でシーンをロードする
            var asyncOp = SceneManager.LoadSceneAsync(sceneName);
            while (!asyncOp.isDone)
            {
                yield return null;
            }
        }

        /// <summary>
        /// 設定ボタンが押された時の処理
        /// </summary>
        public void OnClickOptionButton()
        {
            if (optionUIPanel != null)
            {
                var ui = optionUIPanel.GetComponent<OptionUI>();
                if (ui != null)
                {
                    ui.Open();
                }
                else
                {
                    optionUIPanel.SetActive(true);
                }
            }
            else
            {
                Debug.LogWarning("インスペクターで OptionUIPanel 設定されていません");
            }
        }

        /// <summary>
        /// 3つ目のボタン（ゲーム終了など）が押された時の処理
        /// </summary>
        public void OnClickExitButton()
        {
            Debug.Log("ゲームを終了します。");
#if UNITY_EDITOR
            // Unityエディタ上でのプレイモードを終了する
            UnityEditor.EditorApplication.isPlaying = false;
#else
            // ビルドされたゲームを終了する
            Application.Quit();
#endif
        }
    }
}
