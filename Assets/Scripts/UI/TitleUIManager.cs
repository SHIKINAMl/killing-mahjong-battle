using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public class TitleUIManager : MonoBehaviour
    {
        [Header("遷移先のシーン名")]
        [SerializeField] private string nextSceneName = "UIテストシーン"; // 実際のメインゲームのシーン名に合わせてください

        [Header("設定画面パネル")]
        [SerializeField] private GameObject optionUIPanel;

        private void Start()
        {
            ApplyMatchHubPresentation();
        }

        private TitleMultiMenuUI multiMenu;

        /// <summary>
        /// 「対局する」が押された時の処理。
        ///
        /// **シーンの `onClick` はこのメソッド名で配線済みなので、名前は変えないこと。**
        /// 作り直すと配線が外れて「押しても何も起きない」状態になる。
        /// 中身だけを差し替えて、対戦相手の探し方（野良／フレンド）を
        /// 先に選ばせるようにしている。
        /// </summary>
        public void OnClickStartButton()
        {
            if (multiMenu == null)
            {
                multiMenu = gameObject.AddComponent<TitleMultiMenuUI>();
            }

            multiMenu.Open(mode =>
            {
                Debug.Log($"対局開始（{mode}）。{nextSceneName} に遷移します。");
                StartMultiplayScene();
            });
        }

        /// <summary>
        /// タイトルを「ソロ／マルチ」の区分ではなく、対局の入口として見せる。
        /// 既存 Button の onClick 配線はシーンに保存されているため、Button を作り直さず
        /// 実行時に表示だけを置き換える。
        /// </summary>
        private static void ApplyMatchHubPresentation()
        {
            var labels = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            TMP_FontAsset font = null;

            foreach (var label in labels)
            {
                if (label == null) continue;
                if (font == null && label.font != null) font = label.font;

                string text = label.text.Trim();
                if (text == "ソロ")
                {
                    var button = label.GetComponentInParent<Button>();
                    if (button != null) button.gameObject.SetActive(false);
                }
                else if (text == "マルチ")
                {
                    label.text = "対局する";
                }
            }

            if (font == null) return;

            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            var existing = canvas.transform.Find("TruthNameHook");
            var hook = existing != null ? existing.gameObject :
                new GameObject("TruthNameHook", typeof(RectTransform), typeof(TextMeshProUGUI));
            hook.transform.SetParent(canvas.transform, false);

            var rect = hook.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(172f, 92f);
            rect.sizeDelta = new Vector2(390f, 42f);

            var textMesh = hook.GetComponent<TextMeshProUGUI>();
            textMesh.font = font;
            textMesh.text = "彼女の真名を、探しだせ。";
            textMesh.fontSize = 22f;
            textMesh.fontStyle = FontStyles.Bold;
            textMesh.color = new Color32(240, 232, 236, 230);
            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.raycastTarget = false;
        }

        /// <summary>
        /// 対局シーンへ移る。`MatchJoinRequest` は呼ぶ前に設定しておくこと
        /// （`join` は接続直後に自動で飛ぶので、シーンに入ってからでは間に合わない）。
        /// </summary>
        private void StartMultiplayScene()
        {
            if (KillingMahjong.UI.LoadingManager.Instance != null)
            {
                KillingMahjong.UI.LoadingManager.Instance.FadeOutScreen(() => 
                {
                    StartCoroutine(LoadSceneAsyncCoroutine());
                });
            }
            else
            {
                StartCoroutine(LoadSceneAsyncCoroutine());
            }
        }

        private System.Collections.IEnumerator LoadSceneAsyncCoroutine()
        {
            // 暗転完了後、非同期でシーンをロードする
            var asyncOp = SceneManager.LoadSceneAsync(nextSceneName);
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
