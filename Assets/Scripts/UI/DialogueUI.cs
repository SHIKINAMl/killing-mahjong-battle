using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace KillingMahjong.UI
{
    public class DialogueUI : MonoBehaviour
    {
        [Header("Main Dialogue")]
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private GameObject dialoguePanel;

        [Header("Log")]
        [SerializeField] private GameObject logPanel;
        [SerializeField] private Button toggleLogButton;
        [SerializeField] private Button closeLogBackgroundButton; // 追加: 全画面背景のボタン
        [SerializeField] private Transform logContainer; // 各ログを入れる親(ScrollRectのContentなど)
        [SerializeField] private GameObject logItemPrefab; // 1つ1つのログを表示するプレハブ（TextやUI四角などを持つ）

        private List<string> dialogueHistory = new List<string>();
        private GameObject nextRoundButtonObj;

        public bool IsLogOpen => logPanel != null && logPanel.activeSelf;

        private void Start()
        {
            if (toggleLogButton != null) toggleLogButton.onClick.AddListener(ToggleLog);
            if (closeLogBackgroundButton != null) closeLogBackgroundButton.onClick.AddListener(CloseLog);
            logPanel.SetActive(false);
        }

        public void ShowText(string text)
        {
            StopAllCoroutines(); // 既存の文字送り演出などがあれば即座にキャンセルする

            if (dialogueText != null)
            {
                // セリフの折り返しを有効化
                dialogueText.enableWordWrapping = true;
                // 長すぎる場合は省略記号などを出さずに、単に下にはみ出させる
                dialogueText.overflowMode = TextOverflowModes.Overflow;
                
                StartCoroutine(TypeMessageRoutine(text));
            }
            
            AddToLog(text);
            dialoguePanel.SetActive(true);
        }

        private System.Collections.IEnumerator TypeMessageRoutine(string fullText)
        {
            dialogueText.text = "";
            float timePerChar = 0.03f; // 1文字あたりの表示時間
            float nextSoundTime = 0f;

            for (int i = 0; i < fullText.Length; i++)
            {
                dialogueText.text += fullText[i];

                // 音が連続しすぎないように、一定間隔（例: 0.06秒）で鳴らす
                if (Time.time >= nextSoundTime)
                {
                    if (KillingMahjong.Managers.AudioManager.Instance != null)
                    {
                        // タップ音のような三角波（Triangle）、330Hz、長さ30ms、ボリューム小さめ
                        KillingMahjong.Managers.AudioManager.Instance.PlaySynthSound(KillingMahjong.Managers.SynthWaveType.Triangle, 330f, 330f, 0.03f, 0.3f);
                    }
                    nextSoundTime = Time.time + 0.06f;
                }

                yield return new WaitForSeconds(timePerChar);
            }
        }

        public void HideText()
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (gameObject.activeSelf) gameObject.SetActive(false); // パネル自体も隠す
        }

        private void AddToLog(string text)
        {
            dialogueHistory.Add(text);
            
            if (logContainer != null && logItemPrefab != null)
            {
                // 新しいログ枠（四角）を生成して配置
                GameObject newLogItem = Instantiate(logItemPrefab, logContainer);
                
                // プレハブ内の TextMeshProUGUI を探してテキストをセットする
                // 仮に直下に TextMeshProUGUI がある、あるいは子オブジェクトにある想定
                TextMeshProUGUI tmp = newLogItem.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = text;
                }

                // ログが追加されたら一番下まで自動スクロールさせる
                if (gameObject.activeInHierarchy)
                {
                    StartCoroutine(ScrollToBottom());
                }
            }
        }

        private System.Collections.IEnumerator ScrollToBottom()
        {
            // UIのレイアウト更新を1フレーム待つ（WebGL互換のため yield return null を使用）
            yield return null;
            
            if (logContainer != null)
            {
                ScrollRect scrollRect = logContainer.GetComponentInParent<ScrollRect>();
                if (scrollRect != null)
                {
                    // 0が一番下、1が一番上
                    scrollRect.verticalNormalizedPosition = 0f;
                }
            }
        }

        public void ToggleLog()
        {
            if (logPanel.activeSelf) CloseLog();
            else OpenLog();
        }

        public void SetBackgroundRaycast(bool block)
        {
            if (dialoguePanel != null)
            {
                var img = dialoguePanel.GetComponent<Image>();
                if (img != null) img.raycastTarget = block;
            }
        }

        public void OpenLog()
        {
            logPanel.SetActive(true);
        }

        public void CloseLog()
        {
            logPanel.SetActive(false);

            // ログが閉じられたら、GameUIManager側で止まっていたリアクションの消化を再開する
            var reactionController = KillingMahjong.Managers.ReactionController.Instance;
            if (reactionController != null)
            {
                reactionController.ProcessNextReaction(); // 止まっていた場合、ここから再開される
            }
        }

        private System.Action onNextRoundButtonClicked;

        public void ShowNextRoundButton(System.Action onClick)
        {
            onNextRoundButtonClicked = onClick;

            if (nextRoundButtonObj == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas == null) return;

                // Canvas直下の子として生成し、確実に全画面基準の右下に配置する
                nextRoundButtonObj = new GameObject("NextRoundOKButton");
                nextRoundButtonObj.transform.SetParent(canvas.transform, false);
                
                var rt = nextRoundButtonObj.AddComponent<RectTransform>();
                // 画面の右下に配置
                rt.anchorMin = new Vector2(1, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(1, 0);
                rt.anchoredPosition = new Vector2(-20, 20);
                rt.sizeDelta = new Vector2(120, 45);

                var img = nextRoundButtonObj.AddComponent<Image>();
                img.color = new Color(0.2f, 0.2f, 0.2f, 1f);

                var btn = nextRoundButtonObj.AddComponent<Button>();

                var txtObj = new GameObject("Text");
                txtObj.transform.SetParent(nextRoundButtonObj.transform, false);
                var txtRt = txtObj.AddComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = Vector2.zero;
                txtRt.offsetMax = Vector2.zero;

                var txt = txtObj.AddComponent<TextMeshProUGUI>();
                txt.text = "OK";
                txt.color = Color.white;
                txt.fontSize = 24; // フォントサイズを固定して巨大化を防ぐ
                txt.alignment = TextAlignmentOptions.Center;

                btn.onClick.AddListener(() => {
                    HideNextRoundButton();
                    var action = onNextRoundButtonClicked;
                    onNextRoundButtonClicked = null;
                    action?.Invoke();
                });
            }

            // 万が一他のUIの下に隠れないように、表示するたびに最前面に持ってくる
            nextRoundButtonObj.transform.SetAsLastSibling();
            nextRoundButtonObj.SetActive(true);
        }

        public void HideNextRoundButton()
        {
            if (nextRoundButtonObj != null)
            {
                nextRoundButtonObj.SetActive(false);
            }
        }

        // ==================== 画面クリックでセリフ送り ====================

        private GameObject advanceCatcherObj;
        private System.Action onAdvanceClicked;
        private GameObject advanceMarkerObj;

        /// <summary>
        /// 画面のどこをクリックしてもセリフが進むようにする（要望15）。
        /// 小さな OK ボタンを探して押させるより、読み終わったら適当に押すほうが速い。
        ///
        /// 透明な全画面ボタンを最前面に置くので、**待っている間は他のUIが押せなくなる。**
        /// 「役一覧を開かせる」のようにプレイヤーへ操作させたい場面では、
        /// セリフを送り終えてから（＝ここを閉じてから）誘導すること。
        /// </summary>
        public void ShowAdvanceOnAnyClick(System.Action onClick)
        {
            onAdvanceClicked = onClick;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) { onClick?.Invoke(); return; }

            if (advanceCatcherObj == null)
            {
                advanceCatcherObj = new GameObject("DialogueAdvanceCatcher");
                advanceCatcherObj.transform.SetParent(canvas.transform, false);

                var rt = advanceCatcherObj.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                // 見えないが押せる板。alpha 0 だと Raycast に当たらないので極小の値を入れる
                var img = advanceCatcherObj.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.001f);

                var btn = advanceCatcherObj.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() =>
                {
                    HideAdvanceOnAnyClick();
                    var action = onAdvanceClicked;
                    onAdvanceClicked = null;
                    action?.Invoke();
                });
            }

            advanceCatcherObj.transform.SetAsLastSibling();
            advanceCatcherObj.SetActive(true);

            ShowAdvanceMarker(canvas);
        }

        public void HideAdvanceOnAnyClick()
        {
            if (advanceCatcherObj != null) advanceCatcherObj.SetActive(false);
            if (advanceMarkerObj != null) advanceMarkerObj.SetActive(false);
        }

        /// <summary>吹き出しの右下で点滅する「▼」。クリック待ちだと分かるようにする。</summary>
        private void ShowAdvanceMarker(Canvas canvas)
        {
            if (advanceMarkerObj == null)
            {
                advanceMarkerObj = new GameObject("AdvanceMarker");
                var parent = dialoguePanel != null ? dialoguePanel.transform : canvas.transform;
                advanceMarkerObj.transform.SetParent(parent, false);

                var rt = advanceMarkerObj.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-14f, 10f);
                rt.sizeDelta = new Vector2(20f, 20f);

                var txt = advanceMarkerObj.AddComponent<TextMeshProUGUI>();
                txt.text = "▼";
                txt.color = Color.white;
                txt.fontSize = 16;
                txt.alignment = TextAlignmentOptions.Center;
                txt.raycastTarget = false; // クリックは全画面の板に任せる
                txt.outlineColor = Color.black;
                txt.outlineWidth = 0.25f;

                advanceMarkerObj.AddComponent<BlinkGraphic>();
            }

            advanceMarkerObj.transform.SetAsLastSibling();
            advanceMarkerObj.SetActive(true);
        }
    }

    /// <summary>点滅させるだけの小物。セリフ送り待ちの「▼」に使う。</summary>
    public class BlinkGraphic : MonoBehaviour
    {
        [SerializeField] private float cycle = 0.9f;
        private Graphic target;

        private void Awake() { target = GetComponent<Graphic>(); }

        private void Update()
        {
            if (target == null) return;
            var c = target.color;
            c.a = Mathf.Lerp(0.25f, 1f, (Mathf.Sin(Time.unscaledTime / cycle * Mathf.PI * 2f) + 1f) * 0.5f);
            target.color = c;
        }
    }
}
