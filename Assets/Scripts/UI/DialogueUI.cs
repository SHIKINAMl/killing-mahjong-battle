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
        private Coroutine logScrollRoutine;
        private bool followLatestLog = true;
        private bool hasOpenedLog;

        /// <summary>文字送りの音の高さを決めるために見る、画面の相手キャラ。</summary>
        private EnemyInfoUI enemyInfo;

        public bool IsLogOpen => logPanel != null && logPanel.activeSelf;

        /// <summary>セリフ欄そのもの。チュートリアルの動画（<see cref="TutorialDrawReplayUI"/>）が見た目を写すのに使う。触らないこと。</summary>
        public GameObject PanelForCopy => dialoguePanel;

        /// <summary>セリフの文字。<see cref="PanelForCopy"/> のどの子が文字かを知るためだけに出している。</summary>
        public TextMeshProUGUI TextForCopy => dialogueText;

        /// <summary>
        /// いま1文字ずつ送っている最中か。**喋っているあいだだけ跳ねさせる**ために出している
        /// （<see cref="TalkBobAnimator"/> が見ている）。
        ///
        /// **`static` にしてあるのは、跳ねる側が吹き出しを知らないから。**
        /// 女の子はシーンに置いたスプライトで、吹き出しへの参照を持っていない。
        /// 吹き出しは画面に1つしか出ないので、静的な旗で足りる。
        ///
        /// 黒幕が降りているあいだは送り自体を飛ばす（<see cref="TypeMessageRoutine"/>）ので、
        /// ここも立たない。見えていないのに跳ねる、ということは起きない。
        /// </summary>
        public static bool IsTalking { get; private set; }

        private void Awake()
        {
            // シーンに置いてある仮の文字（"Nothing"）を消しておく。
            // 吹き出しが最初に出た一瞬だけ、それが読めてしまっていた
            // （2026-09-24 に録画で確認）。シーンを直さずコードで潰す。
            if (dialogueText != null) dialogueText.text = string.Empty;

            // 文字とLOGボタンを含むパネルは固定する。親の浮遊を文字にも当てると、
            // ピクセルフォントが動くたびにちらつき、読み進めにくくなる。
            // 対局とチュートリアルの両方で同じ設定にする。
            if (dialoguePanel != null)
            {
                var floating = dialoguePanel.GetComponent<FloatingAnimator>();
                if (floating != null) floating.enabled = false;
            }

            ApplyBubbleSetting();
        }

        /// <summary>
        /// 仮の文字を消すのは `OnEnable` でもやる（2026-09-28）。
        ///
        /// **`Awake` は伏せたままの間は走らない。** 2026-09-27 に
        /// `OpeningSequenceManager` が開幕で吹き出しを起こすのをやめたため、
        /// チュートリアルの頭では伏せられたままになり、シーンに置いてある
        /// "Nothing" が消えないまま残っていた（実機で確認）。
        /// 最初に起こされた瞬間にも消しておけば、1フレームも読めない。
        /// </summary>
        private void OnEnable()
        {
            if (dialogueText != null && dialogueText.text == "Nothing")
                dialogueText.text = string.Empty;
        }

        private void Start()
        {
            if (toggleLogButton != null) toggleLogButton.onClick.AddListener(ToggleLog);
            if (closeLogBackgroundButton != null) closeLogBackgroundButton.onClick.AddListener(CloseLog);
            logPanel.SetActive(false);

            ApplyBubbleSetting();
        }

        // ==================== 吹き出しの枠 ====================
        //
        // **枠は既定で出さない（2026-09-27、プランナーの判断）。**
        // ただし有りと無しの感触を見比べたいので、設定から切り替えられるようにしてある。
        //
        // 消すのは `DialoguePanel` 自身の `Image` と `Shadow` だけ。
        // **SetActive では消さないこと。** あれを切ると子の文字とLOGボタンも道連れになり、
        // セリフが1行も出なくなる。

        /// <summary>
        /// 画面にある DialogueUI すべてに設定を当て直す。
        /// 設定画面で切り替えた瞬間に見た目が変わるように、`SettingsManager` から呼ぶ。
        /// **非アクティブなものも拾う。** セリフが出ていない間に切り替えることの方が多い。
        /// </summary>
        public static void ApplyBubbleSettingToAll()
        {
            var all = Object.FindObjectsByType<DialogueUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++) all[i].ApplyBubbleSetting();
        }

        private void ApplyBubbleSetting()
        {
            if (dialoguePanel == null) return;

            // 設定がまだ居ない場面（起動直後など）は、既定の「出さない」で進める
            var settings = KillingMahjong.Core.SettingsManager.Instance;
            bool show = settings != null && settings.ShowDialogueBubble;

            var img = dialoguePanel.GetComponent<Image>();
            if (img != null) img.enabled = show;

            // 枠に付いている影も一緒に。枠が消えているのに影だけ残ると、
            // 文字のまわりに薄い四角が浮いて見える
            var shadow = dialoguePanel.GetComponent<Shadow>();
            if (shadow != null) shadow.enabled = show;
        }

        // セリフの影の設定。対局シーンが2つ（UIテストシーン / OpeningScene）あるので
        // SerializeField にせずコードに持つ。シーンに焼くと片方だけ古い値になる。
        //
        // **internal にしてある（2026-10-08）。** チュートリアルの強調で使う太字のフォント
        // （TutorialEmphasis）にも同じ影を付けるため。値を二重に持つと片方だけ変えて食い違う。
        internal const float UnderlayOffsetX = 1f;
        internal const float UnderlayOffsetY = -1f;
        internal const float UnderlayDilate = 0.25f;
        internal const float UnderlaySoftness = 0f;

        private bool shadowApplied = false;

        /// <summary>
        /// セリフを背景から浮かせて、吹き出しと重なっても読めるようにする。
        ///
        /// 使えるのは TMP の Underlay だけ。ほかの2つは実測で駄目だった:
        /// ・`UnityEngine.UI.Shadow` / `Outline` は TMP が無視する（付けても画素が1つも変わらない）
        /// ・SDF の輪郭（_OutlineWidth）は、このフォントアセットが pointSize 90 / padding 9 で
        ///   焼かれているため fontSize 15 では padding 全体でも 1.5px しかない。0.2 では黒画素が
        ///   1つも出ず、見える太さ（0.5〜）にすると先に字の面が食われて潰れる。_FaceDilate で
        ///   面を太らせて補正しても駄目だった
        ///
        /// Start() ではなく ShowText() から呼ぶ。DialogueUI は非アクティブで置かれていて
        /// SetActive(true) の直後に ShowText() が来るため、Start() は**まだ走っていない**。
        ///
        /// マテリアルを触ると TMP がインスタンスを作るので、一度当てたら使い回す。
        /// </summary>
        private void ApplyShadow()
        {
            if (shadowApplied || dialogueText == null) return;
            shadowApplied = true;

            var mat = dialogueText.fontMaterial;
            if (mat == null) return;

            // キーワードを立てないと、値は入っているのに影が一切描かれない。
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", Color.black);
            mat.SetFloat("_UnderlayOffsetX", UnderlayOffsetX);
            mat.SetFloat("_UnderlayOffsetY", UnderlayOffsetY);
            mat.SetFloat("_UnderlayDilate", UnderlayDilate);
            mat.SetFloat("_UnderlaySoftness", UnderlaySoftness);

            dialogueText.UpdateMeshPadding();
        }

        public void ShowText(string text)
        {
            // 波ダッシュ「〜」(U+301C) は吹き出しのフォント（PixelMplus10）に無く、□ になる。
            // 同じ形の全角チルダ「～」(U+FF5E) はあるので差し替える（2026-10-23。CSV や反応ルールに書かれても化けないように）
            if (!string.IsNullOrEmpty(text)) text = text.Replace('〜', '～');

            // **`StopAllCoroutines` で止めた分は `finally` が走らない。** 先に倒しておく。
            // 送りの途中で次のセリフが来たとき、旗が立ちっぱなしになるのを防ぐ。
            IsTalking = false;
            StopAllCoroutines(); // 既存の文字送り演出などがあれば即座にキャンセルする

            ApplyShadow();

            if (dialogueText != null)
            {
                // セリフの折り返しを有効化
                dialogueText.textWrappingMode = TextWrappingModes.Normal;
                // 長すぎる場合は省略記号などを出さずに、単に下にはみ出させる
                dialogueText.overflowMode = TextOverflowModes.Overflow;
                
                StartCoroutine(TypeMessageRoutine(text));
            }
            
            AddToLog(text);
            dialoguePanel.SetActive(true);
        }

        private System.Collections.IEnumerator TypeMessageRoutine(string fullText)
        {
            // 先に全文を TMP に解析させ、可視文字数だけを進める。
            // これなら <color> / <size> のタグそのものを文字送りで見せずに済む。
            dialogueText.text = fullText;
            dialogueText.maxVisibleCharacters = 0;
            yield return null; // ShowText() が DialoguePanel を有効化した次フレームに解析する
            dialogueText.ForceMeshUpdate();

            // **黒幕が降りているあいだは1文字ずつ送らない（2026-09-27 のユーザー指示）。**
            // 「第1局進行中...」で画面が真っ暗な裏でセリフ送りが走っていて、
            // 見えないのに送りの音だけが「ポポポポポ」と鳴っていた。
            // ここで全文を出してしまえば、音も鳴らず、待っている側も普通に先へ進む。
            if (PhaseTransitionUI.IsScreenDarkened)
            {
                dialogueText.maxVisibleCharacters = int.MaxValue;
                yield break;
            }

            IsTalking = true;

            // 1文字あたりの表示時間。**設定から引く（2026-09-27）。**
            // 以前は 0.03 の決め打ちだった。設定が無いときは従来どおり 0.03。
            var settings = KillingMahjong.Core.SettingsManager.Instance;
            float timePerChar = settings != null ? settings.SecondsPerCharacter : 0.03f;
            float nextSoundTime = 0f;
            int visibleCharacterCount = dialogueText.textInfo.characterCount;

            // 音の高さは、いま画面にいる相手キャラで決める（2026-10-25）。金髪の先輩は低め、赤い服の女の子は高め
            if (enemyInfo == null) enemyInfo = FindFirstObjectByType<EnemyInfoUI>();
            var speaker = enemyInfo != null ? enemyInfo.CurrentCharacterData : null;
            float blipStartHz = speaker != null ? speaker.TextBlipStart : CharacterData.DefaultTextBlipHz;
            float blipEndHz = speaker != null ? speaker.TextBlipEnd : CharacterData.DefaultTextBlipHz;

            for (int i = 0; i < visibleCharacterCount; i++)
            {
                dialogueText.maxVisibleCharacters = i + 1;

                // 音が連続しすぎないように、一定間隔（例: 0.06秒）で鳴らす
                if (Time.time >= nextSoundTime)
                {
                    if (KillingMahjong.Managers.AudioManager.Instance != null)
                    {
                        // タップ音のような三角波（Triangle）、330Hz、長さ30ms。
                        //
                        // **最後の値は 0.3 では BGM に完全に埋もれる（2026-09-26 に 5.0 へ）。**
                        // 経路は 三角波 × 包絡 × 0.2（AudioSynth の中で掛かる）× ここの値 × seVolume。
                        // 0.3 のときの実効値は -42.76 dBFS で、BGM（-21.74 dBFS）より **21dB 下**だった。
                        // 中の 0.2 は全シンセSE共通なので触らず、ここだけで持ち上げている。
                        // 5.0 で -18.33 dBFS、BGM比 +3.4 dB、ピーク -6.87 dBFS（歪まない）。
                        //
                        // **1 を超えてよい。** AudioSource.PlayOneShot の volumeScale は
                        // 頭打ちにならない。実測で vol=3 がちょうど3.2倍、vol=10 が9.6倍になった。
                        KillingMahjong.Managers.AudioManager.Instance.PlaySynthSound(KillingMahjong.Managers.SynthWaveType.Triangle, blipStartHz, blipEndHz, 0.03f, 5.0f);
                    }
                    nextSoundTime = Time.time + 0.06f;
                }

                yield return new WaitForSeconds(timePerChar);
            }

            dialogueText.maxVisibleCharacters = int.MaxValue;
            IsTalking = false;
        }

        public void HideText()
        {
            IsTalking = false;
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (gameObject.activeSelf) gameObject.SetActive(false); // パネル自体も隠す
        }

        // 吹き出しごと伏せられたときも倒す。**シーンを移ると `static` が残る**ので、
        // ここで落としておかないと、次のシーンで誰も喋っていないのに跳ね続ける。
        private void OnDisable()
        {
            IsTalking = false;
            if (logScrollRoutine != null) StopCoroutine(logScrollRoutine);
            logScrollRoutine = null;
        }

        /// <summary>確定したスキル使用を記録する。セリフ表示・演出の再生は行わない。</summary>
        public void AddSkillToLog(string skillType, bool isLocalPlayer)
        {
            if (string.IsNullOrEmpty(skillType)) return;
            string text = (isLocalPlayer ? "自分" : "敵") + "：" + Common.SkillNames.GetDisplayName(skillType);
            AddToLog(text, isLocalPlayer ? new Color32(63, 158, 239, 255) : new Color32(237, 72, 88, 255), isLocalPlayer);
        }

        private void AddToLog(string text, Color? skillColor = null, bool isLocalPlayer = false)
        {
            dialogueHistory.Add(text);
            
            if (logContainer != null && logItemPrefab != null)
            {
                var scroll = logContainer.GetComponentInParent<ScrollRect>(true);
                bool visible = logPanel != null && logPanel.activeInHierarchy;
                if (visible) followLatestLog = IsNearLatestLog(scroll);
                var position = ((RectTransform)logContainer).anchoredPosition;
                // 新しいログ枠（四角）を生成して配置
                GameObject newLogItem = Instantiate(logItemPrefab, logContainer);
                
                // プレハブ内の TextMeshProUGUI を探してテキストをセットする
                // 仮に直下に TextMeshProUGUI がある、あるいは子オブジェクトにある想定
                TextMeshProUGUI tmp = newLogItem.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = text;
                }
                if (skillColor.HasValue)
                {
                    var color = skillColor.Value;
                    var rowRect = newLogItem.GetComponent<RectTransform>();
                    if (rowRect != null) rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, 36);
                    var layout = newLogItem.GetComponent<LayoutElement>();
                    if (layout == null) layout = newLogItem.AddComponent<LayoutElement>();
                    layout.minHeight = layout.preferredHeight = 36;
                    layout.flexibleHeight = 0;
                    var background = newLogItem.GetComponent<Image>();
                    if (background != null) background.enabled = false;
                    newLogItem.AddComponent<SkillLogRowUI>().Configure(tmp, color, isLocalPlayer,
                        animate: visible && followLatestLog);
                }

                if (visible) ScheduleLogScroll(scroll, position);
            }
        }

        private static bool IsNearLatestLog(ScrollRect scroll)
        {
            if (scroll == null || scroll.content == null) return true;
            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            float overflow = Mathf.Max(0, scroll.content.rect.height - viewport.rect.height);
            return overflow * scroll.verticalNormalizedPosition <= 16f;
        }

        private void ScheduleLogScroll(ScrollRect scroll, Vector2 position)
        {
            if (scroll == null || !isActiveAndEnabled) return;
            if (logScrollRoutine != null) StopCoroutine(logScrollRoutine);
            logScrollRoutine = StartCoroutine(RefreshLogScroll(scroll, position, followLatestLog));
        }

        private System.Collections.IEnumerator RefreshLogScroll(ScrollRect scroll, Vector2 position, bool follow)
        {
            yield return null; // 追加した行のレイアウト確定を待つ。
            logScrollRoutine = null;
            if (scroll == null || scroll.content == null || logPanel == null || !logPanel.activeInHierarchy) yield break;
            var current = scroll.content.anchoredPosition;
            // 待機中に読み手が動かした場合も、その位置を優先する。
            if (Mathf.Abs(current.y - position.y) > 1f) follow = false;
            Canvas.ForceUpdateCanvases();
            if (follow)
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 0f;
            }
            else scroll.content.anchoredPosition = current;
            followLatestLog = follow;
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
            // 会話本体のCanvasは盤面用なので、ログだけを独立した描画順へ上げる。
            var canvas = logPanel.GetComponent<Canvas>();
            if (canvas == null) canvas = logPanel.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = KillingMahjong.Common.UISortingOrders.DialogueLog;
            // 子Canvasへ移したGraphicもスクロール・閉じる操作を受けられるようにする。
            if (logPanel.GetComponent<GraphicRaycaster>() == null)
                logPanel.AddComponent<GraphicRaycaster>();
            var scroll = logContainer != null ? logContainer.GetComponentInParent<ScrollRect>(true) : null;
            if (!hasOpenedLog) followLatestLog = true;
            hasOpenedLog = true;
            if (scroll != null && scroll.content != null) ScheduleLogScroll(scroll, scroll.content.anchoredPosition);
        }

        public void CloseLog()
        {
            var scroll = logContainer != null ? logContainer.GetComponentInParent<ScrollRect>(true) : null;
            followLatestLog = IsNearLatestLog(scroll);
            if (logScrollRoutine != null) StopCoroutine(logScrollRoutine);
            logScrollRoutine = null;
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
