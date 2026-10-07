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
        private const string RoomSceneName = "部屋シーン";
        private const string TutorialReturnToRoomKey = TutorialNavigation.ReturnKey;

        [Header("遷移先のシーン名")]
        [SerializeField] private string nextSceneName = "UIテストシーン"; // 実際のメインゲームのシーン名に合わせてください

        [Header("設定画面パネル")]
        [SerializeField] private GameObject optionUIPanel;

        [SerializeField] private bool startInPreservedRoom;

        /// <summary>タイトルの表示差し替えが完了したことを、撮影や自動確認から読めるようにする。</summary>
        public bool PresentationApplied { get; private set; }

        private void Start()
        {
            ApplyTitlePresentation();
            HideTitleLogo();
            PresentationApplied = true;

            if (startInPreservedRoom)
            {
                var preview = FindSceneObjectIncludingInactive("SavedRoomPreview");
                if (preview != null)
                {
                    preview.SetActive(false);
                    Destroy(preview);
                }
                OpenPreservedRoom();
                return;
            }

            BuildTitleClickTarget();
            ExhibitionIdleReturn.EnableSession();

            // 既存の帰還キーを一度だけ消費し、チュートリアル後は対局メニューへ進む。
            int tutorialReturn = PlayerPrefs.GetInt(TutorialReturnToRoomKey, 0);
            if (tutorialReturn != 0)
            {
                PlayerPrefs.DeleteKey(TutorialReturnToRoomKey);
                PlayerPrefs.Save();
                if (tutorialReturn == 2)
                    SceneManager.LoadScene(RoomSceneName);
                else
                    OpenMatchMenu();
            }
        }

        private TitleMultiMenuUI multiMenu;
        private RoomScreenUI roomScreen;

        /// <summary>
        /// 「ゲーム開始」が押された時の処理。
        ///
        /// **シーンの `onClick` はこのメソッド名で配線済みなので、名前は変えないこと。**
        /// 作り直すと配線が外れて「押しても何も起きない」状態になる。
        /// 展示用タイトルからはチュートリアルを最初から開始する。
        /// </summary>
        public void OnClickStartButton()
        {
            if (startInPreservedRoom)
            {
                OpenPreservedRoom();
                return;
            }
            if (isStartingTutorial) return;
            isStartingTutorial = true;
            if (titleClickTarget != null) titleClickTarget.SetActive(false);
            StartTutorialScene(0);
        }

        private bool isStartingTutorial;
        private GameObject titleClickTarget;

        private void BuildTitleClickTarget()
        {
            titleClickTarget = new GameObject("TitleClickToTutorial", typeof(RectTransform),
                typeof(Canvas), typeof(GraphicRaycaster), typeof(Image), typeof(Button));
            var canvas = titleClickTarget.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = KillingMahjong.Common.UISortingOrders.TitleStartInput;
            var image = titleClickTarget.GetComponent<Image>();
            image.color = Color.clear;
            var button = titleClickTarget.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(OnClickStartButton);

            // 繰り返し遊ぶ来場者は、練習を通さず対局メニューを開ける。
            var shortcut = SessionPrompt.CreateButton(titleClickTarget.transform,
                "ルールを知っている方：対局へ", new Vector2(0f, 40f), OpenMatchMenu);
            var rect = shortcut.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(400f, 48f);
            shortcut.GetComponentInChildren<TMP_Text>().fontSize = 20f;

            // 開発用の入口（2026-10-07 のユーザー指示）。部屋シーンへ直接行く。
            // 展示用タイトルはチュートリアルか対局メニューにしか進めず、部屋
            // （コレクションや演出の試写がある）へ行く道が無かった。
            // 来場者向けの導線ではないので、右下の隅に小さく、目立たない色で置く。
            var debug = SessionPrompt.CreateButton(titleClickTarget.transform,
                "デバッグ部屋", new Vector2(-70f, 22f), OpenDebugRoom);
            var debugRect = debug.GetComponent<RectTransform>();
            debugRect.anchorMin = debugRect.anchorMax = new Vector2(1f, 0f);
            debugRect.sizeDelta = new Vector2(124f, 28f);
            debug.GetComponent<Image>().color = new Color32(30, 18, 26, 170);
            var debugLabel = debug.GetComponentInChildren<TMP_Text>();
            debugLabel.rectTransform.sizeDelta = debugRect.sizeDelta;
            debugLabel.fontSize = 14f;
            debugLabel.color = new Color32(200, 188, 196, 255);
        }

        /// <summary>
        /// タイトルから「デバッグ部屋」＝部屋シーンへ移る。
        ///
        /// **押したらタイトルの「どこでも押せば開始」を止める。**
        /// 止めないと、暗転している間のクリックでチュートリアルも始まってしまう。
        /// </summary>
        private void OpenDebugRoom()
        {
            if (isStartingTutorial) return;
            if (titleClickTarget != null) titleClickTarget.SetActive(false);
            StartScene(RoomSceneName);
        }

        private void OpenPreservedRoom()
        {
            if (roomScreen == null)
            {
                roomScreen = gameObject.GetComponent<RoomScreenUI>();
                if (roomScreen == null) roomScreen = gameObject.AddComponent<RoomScreenUI>();
            }

            SetTitlePresentationVisible(false);
            roomScreen.Open(OpenMatchMenu, OpenRoomTutorial, OpenRoomOptions, ExitGameFromRoom, ReturnToTitle,
                OpenCollection);

            // 部屋ではチルい曲（2026-09-19 のユーザー指示）。タイトルへ戻るときに戻す
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio != null) audio.PlayRoomBgm();
        }

        private CollectionUI collection;

        /// <summary>
        /// コレクション画面を開く（2026-09-11）。
        ///
        /// **開いている間は部屋の絵を畳む。** 畳まないと、全画面モーダルの下で
        /// 部屋のメニューが押せてしまう（設定パネルを開くときと同じ扱い）。
        /// </summary>
        private void OpenCollection()
        {
            if (collection == null)
            {
                collection = gameObject.GetComponent<CollectionUI>();
                if (collection == null) collection = gameObject.AddComponent<CollectionUI>();
            }

            if (roomScreen != null) roomScreen.SetContentVisible(false);
            collection.Open(() =>
            {
                if (roomScreen != null && roomScreen.IsOpen) roomScreen.SetContentVisible(true);

                // コレクションは閉じるときにタイトル曲を流すので、部屋にいるなら部屋の曲へ戻す
                var audio = KillingMahjong.Managers.AudioManager.Instance;
                if (audio != null && roomScreen != null && roomScreen.IsOpen) audio.PlayRoomBgm();
            });
        }

        private GameObject truthNameHook;

        private void Update()
        {
            if (roomScreen == null || !roomScreen.IsOpen) return;

            // **設定中は部屋を暗くして背景に残す（2026-09-19 のユーザー指示）。**
            // 以前は部屋を畳んでいて、パネルの後ろに何もない空間が映っていた。
            // パネルを部屋より手前に出すのは OpenRoomOptions。
            //
            // **コレクションは今までどおり部屋を畳む（2026-09-11）。** ここは毎フレーム走るので、
            // 判定に入れておかないと開いた次のフレームで部屋の絵が戻ってきてしまう。
            bool isOptionOpen = optionUIPanel != null && optionUIPanel.activeInHierarchy;
            bool isCollectionOpen = collection != null && collection.IsOpen;
            roomScreen.SetContentVisible(!isCollectionOpen);
            roomScreen.SetBackdropMode(isOptionOpen && !isCollectionOpen);

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
            if (titleClickTarget != null) titleClickTarget.SetActive(false);
            var titleMenu = FindSceneObjectIncludingInactive("ボタン達");
            if (titleMenu != null) titleMenu.SetActive(false);
            if (multiMenu == null)
            {
                multiMenu = gameObject.AddComponent<TitleMultiMenuUI>();
            }

            multiMenu.Open(mode =>
            {
                Debug.Log($"対局開始（{mode}）。{nextSceneName} に遷移します。");
                if (titleClickTarget != null) titleClickTarget.SetActive(false);
                if (roomScreen != null) roomScreen.SetContentVisible(false);
                StartMultiplayScene();
            }, () =>
            {
                if (titleClickTarget != null) titleClickTarget.SetActive(true);
                if (titleMenu != null && !startInPreservedRoom) titleMenu.SetActive(true);
            });
        }

        private void OpenRoomOptions()
        {
            // 設定パネルは既存の Canvas（並び順 0）にあり、部屋（TitleRoomScreen）より奥になる。
            // 部屋を背景に残すので、パネルだけ部屋より手前へ出す。
            // **開いてから設定すること。** overrideSorting は入れ子の Canvas にしか効かず、
            // 非表示のうちに立てても無視される（先に立てたら部屋の裏に隠れたままだった）
            OnClickOptionButton();
            if (optionUIPanel != null)
            {
                var canvas = optionUIPanel.GetComponent<Canvas>();
                if (canvas == null) canvas = optionUIPanel.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingOrder = KillingMahjong.Common.UISortingOrders.TitleRoomScreen + 5;
                if (optionUIPanel.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                    optionUIPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
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
            TutorialNavigation.Begin(startInPreservedRoom ? 2 : 1);
            TutorialManager.RequestStartFrom(roundIndex);

            if (roomScreen != null) roomScreen.SetContentVisible(false);
            StartScene(TutorialSceneName);
        }

        private void ReturnToTitle()
        {
            if (multiMenu != null) multiMenu.Close();
            if (roomScreen != null) roomScreen.Close();
            SetTitlePresentationVisible(true);
            if (titleClickTarget != null) titleClickTarget.SetActive(true);

            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio != null) audio.PlayTitleBgm();
        }

        private void ExitGameFromRoom()
        {
            OnClickExitButton();
        }

        /// <summary>
        /// タイトルを「ソロ／マルチ」の区分ではなく、対局の入口として見せる。
        /// 既存 Button の onClick 配線はシーンに保存されているため、Button を作り直さず
        /// 表示状態はタイトルシーンにも保存している。旧状態のシーンでも同じ表示に揃える。
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
            // ロゴ（TitleLogo）とキラキラ（Sparkle0〜4）はここに入れない。
            // 入れると、部屋から「タイトルへ」で戻ったときに復活する。
            // 消すのは HideTitleLogo() の役目。
            string[] titleOnlyObjects =
            {
                "女の子",
                "女の子_Silhouette (白フチ)",
                "タイトル絵",
                "TitleScrim",
                "ボタン達"
            };

            foreach (string objectName in titleOnlyObjects)
            {
                // GameObject.Find は非アクティブになったタイトル要素を探せない。
                // 部屋から戻るときも確実に復帰できるよう、シーン内の非アクティブ要素を含めて探す。
                var target = FindSceneObjectIncludingInactive(objectName);
                if (target != null) target.SetActive(visible);
            }
        }

        /// <summary>
        /// タイトルのロゴ「じゃんぱいあ」と、その周りのキラキラを消す。ユーザーの指示（2026-09-05）。
        ///
        /// 停止中にも現在のタイトルが見えるよう、非表示状態をシーンに保存している。
        /// 起動時にも揃えることで、旧状態のシーンを読み込んでもロゴが復活しない。
        /// </summary>
        private static void HideTitleLogo()
        {
            string[] logoObjects =
            {
                "TitleLogo",
                "Sparkle0",
                "Sparkle1",
                "Sparkle2",
                "Sparkle3",
                "Sparkle4"
            };

            foreach (string objectName in logoObjects)
            {
                var target = FindSceneObjectIncludingInactive(objectName);
                if (target != null) target.SetActive(false);
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
            if (isLoadingScene) return;
            isLoadingScene = true;
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

        private bool isLoadingScene;

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
