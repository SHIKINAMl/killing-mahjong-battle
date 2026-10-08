using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;
using KillingMahjong.Network;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class PhaseTransitionUI : MonoBehaviour
    {
        [Header("Animation Setup")]
        [SerializeField] private Material checkerMaterial; // Material using UI/CheckerboardTransition
        [SerializeField] private Image fullScreenCheckerImage;
        [SerializeField] private RectTransform horizontalLineRt; // The line that expands
        
        [Header("Text References")]
        [SerializeField] private TextMeshProUGUI centerText; // Used for "対局開始", "1 Round", "先行/後攻"
        [SerializeField] private TextMeshProUGUI loadingText; // "対戦相手を待機中..." など用
        [SerializeField] private TextMeshProUGUI promptText; // "手牌を選んでください" などのプロンプト用

        [Header("Bet & HP Deduction Setup")]
        [SerializeField] private GameObject hpBetContainer;
        [SerializeField] private TextMeshProUGUI enemyBetObj;
        [SerializeField] private TextMeshProUGUI playerBetObj;
        [SerializeField] private TextMeshProUGUI enemyHpObj;
        [SerializeField] private TextMeshProUGUI playerHpObj;

        [Header("スキルカットインの描画負荷")]
        // WebGL は塗り面積（フィルレート）が効くので、半透明で重ねる面積を絞れるようにしている。
        // 血飛沫は 800x600 基準で 1200px にすると画面の4倍の面積を半透明で塗ることになる。
        [SerializeField, Tooltip("血飛沫の枚数。0で無し")] private int splatterCount = 2;
        [SerializeField, Tooltip("血飛沫の最小サイズ(px)")] private float splatterSizeMin = 500f;
        [SerializeField, Tooltip("血飛沫の最大サイズ(px)")] private float splatterSizeMax = 800f;
        [SerializeField, Tooltip("立ち絵にドロップシャドウを付ける。付けると立ち絵1枚ぶん塗る面積が増える")]
        private bool portraitShadow = false;
        [SerializeField, Tooltip("文字に色付きのズレ影を重ねる。黒影は読みやすさのため常に付く")]
        private bool textThemeShadow = false;

        [Header("ドット血しぶき（レトロ演出）")]
        [Tooltip("決着演出を「血が敗者から勝者へ移る」表現にする。画面外周も赤く縁取る")]
        [SerializeField] private bool useBloodTransfer = true;
        [Tooltip("useBloodTransfer が OFF のときだけ有効。その場で弾ける血しぶき")]
        [SerializeField] private bool usePixelBlood = true;
        [Tooltip("飛ばすドットの数（弾ける方の演出用）")]
        [SerializeField] private int pixelBloodDotCount = 90;
        [Tooltip("座標を丸めるグリッド幅(px)。大きいほど粗くレトロになる")]
        [SerializeField] private float pixelBloodGridSize = 6f;

        [Header("Animation Durations")]
        [SerializeField] private float lineInDuration = 0.5f;
        [SerializeField] private float textWaitDuration = 1.0f;
        [SerializeField] private float checkerFadeDuration = 1.0f;
        [SerializeField] private float hpDeductionDuration = 1.5f;

        /// <summary>
        /// 実行時に作ったマテリアルの複製。**後始末のために持っておく。**
        /// </summary>
        private Material checkerMaterialInstance;

        private void Awake()
        {
            EnsureCheckerMaterialInstance();
        }

        /// <summary>
        /// 市松模様のマテリアルを、実行時だけの複製に差し替える。
        ///
        /// **共有アセットを直接触ってはいけない。** `checkerMaterial` はシーンから
        /// `Assets/Resources/市松模様.mat` を直に指していて、そこへ `SetFloat("_Progress", ...)`
        /// を書くと**アセットそのものが書き換わる**。エディタでは再生するたびに
        /// `_Progress: 0` が `1` になってファイルが汚れ、毎回 git に差分が出ていた
        /// （2026-09-27 に原因を特定）。
        ///
        /// 複製に差し替えれば、演出は同じまま、アセットには何も書かれない。
        /// **`Start` ではなく `Awake` でやる。** 演出は `Start` より前に走ることがある。
        /// </summary>
        private void EnsureCheckerMaterialInstance()
        {
            if (checkerMaterialInstance != null) return;
            if (checkerMaterial == null) return;

            checkerMaterialInstance = new Material(checkerMaterial);
            checkerMaterialInstance.name = checkerMaterial.name + " (実行時の複製)";
            checkerMaterial = checkerMaterialInstance;

            // 画像側も複製を使うようにする。ここを忘れると、見た目は元のまま動かない
            if (fullScreenCheckerImage != null) fullScreenCheckerImage.material = checkerMaterialInstance;
        }

        private void Start()
        {
            // UIの被り対策: トランジション演出を最前面に表示するためCanvasを追加してSortingOrderを高く設定
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = UISortingOrders.PhaseTransitionBase;
            
            // レイキャストを有効にする場合（必要に応じて）
            UnityEngine.UI.GraphicRaycaster raycaster = GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null)
            {
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            ResetVisuals();

            if (loadingText != null)
            {
                loadingText.gameObject.SetActive(false);
            }

        }

        private void OnDestroy()
        {
            // **立てたまま消さない。** 残ると次の場面でセリフ送りが効かなくなる
            IsScreenDarkened = false;


            // 複製は自分で捨てる。放っておくと再生のたびに積もる
            if (checkerMaterialInstance != null)
            {
                if (Application.isPlaying) Destroy(checkerMaterialInstance);
                else DestroyImmediate(checkerMaterialInstance);
                checkerMaterialInstance = null;
            }
        }

        // 空の Update() があると Unity から毎フレーム呼ばれるだけ無駄なので削除した。
        // （かつての loadingText タイマー用。処理は残っていない）

        private void ResetVisuals()
        {
            if (fullScreenCheckerImage != null && checkerMaterial != null)
            {
                if (!isDarkened)
                {
                    checkerMaterial.SetFloat("_Progress", 0f);
                    fullScreenCheckerImage.gameObject.SetActive(false);
                }
                
                // 確実に画面(親Canvas)全体を覆うようにアンカー設定を強制し、
                // さらに万が一親コンテナが画面より小さい場合に備えて圧倒的なスケールをかける
                RectTransform rt = fullScreenCheckerImage.rectTransform;
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.sizeDelta = Vector2.zero;
                    rt.anchoredPosition = Vector2.zero;
                    rt.localScale = new Vector3(10f, 10f, 1f); // 画面を10倍で覆う
                }
            }
            
            if (horizontalLineRt != null)
            {
                horizontalLineRt.localScale = new Vector3(0, 0, 1); // target (1,1,1)
                
                // 横線が画面の左右端まで確実に届くように横方向ストレッチを設定
                horizontalLineRt.anchorMin = new Vector2(0, 0.5f);
                horizontalLineRt.anchorMax = new Vector2(1, 0.5f);
                horizontalLineRt.sizeDelta = new Vector2(0, horizontalLineRt.sizeDelta.y);
                horizontalLineRt.anchoredPosition = Vector2.zero;

                horizontalLineRt.gameObject.SetActive(false);
            }

            if (centerText != null) centerText.gameObject.SetActive(false);
            if (hpBetContainer != null) hpBetContainer.SetActive(false);
        }

        private PlayerInfoUI targetPlayerInfoUI;

        public void PlayTransition(string roundName, PlayerInfoUI playerInfoUI, KillingMahjong.EngineData.BettingCompletedInfo bet, Action onMidpoint, Action onComplete, bool? displayIsLocalTurn = null)
        {
            this.targetPlayerInfoUI = playerInfoUI;
            StartCoroutine(SequenceRoutine(roundName, bet, onMidpoint, onComplete, displayIsLocalTurn));
        }

        public void PlayCenterTextAnim(string text, float duration = 1.5f, Action onComplete = null)
        {
            this.gameObject.SetActive(true);
            StartCoroutine(CenterTextAnimRoutine(text, duration, onComplete));
        }

        public IEnumerator PlayCenterTextAnimRoutine(string text, float duration = 1.5f, Action onComplete = null)
        {
            this.gameObject.SetActive(true);
            yield return StartCoroutine(CenterTextAnimRoutine(text, duration, onComplete));
        }


        private bool isDarkened = false;
        private readonly PresentationScopeSet skillPresentations = new PresentationScopeSet(Debug.LogException);

        private void OnDisable() { CancelTransitions(); }
        internal void CancelSkillPresentations() { skillPresentations.CancelAll(); }

        internal void CancelTransitions()
        {
            skillPresentations.CancelAll();
            StopAllCoroutines();
            isDarkened = false;
            IsDarkenTransitioning = false;
            IsScreenDarkened = false;
            _additionalRoundStartDarkenedCallbacks = null;
            _additionalRoundStartReadyCallbacks = null;
            _roundStartCleanupDone = false;
            if (fullScreenCheckerImage != null) fullScreenCheckerImage.gameObject.SetActive(false);
            if (horizontalLineRt != null) horizontalLineRt.gameObject.SetActive(false);
            if (centerText != null) centerText.gameObject.SetActive(false);
            if (promptText != null) promptText.gameObject.SetActive(false);
            if (hpBetContainer != null) hpBetContainer.SetActive(false);
            Effects.TileClatterEffect.Hide(transform as RectTransform);
        }

        public bool IsDarkenTransitioning { get; private set; }

        /// <summary>
        /// 局頭の黒幕が降りているあいだ true（2026-09-27）。
        ///
        /// **黒幕の裏でセリフ送りが走ると、見えないのに音だけ「ポポポポ」と鳴る。**
        /// それを止めるために <see cref="DialogueUI"/> から見に来る。
        /// 場面をまたいで見る必要があるので static にしてある。
        ///
        /// **必ず降ろすこと。** 立てたまま消えると、そこから先ずっと
        /// セリフ送りが無くなる（同じ作りの `SuppressStartupBgm` で実際にやった）。
        /// `OnDestroy` でも降ろしている。
        /// </summary>
        public static bool IsScreenDarkened { get; private set; }

        public void PlayRoundStartDarken(string text, Action onDarkened = null, Action onReady = null)
        {
            if (isDarkened)
            {
                // isDarkened は暗転の開始時点で立つ。進行中の暗転へ合流した
                // 局頭リセットも、画面を覆いきるまで実行しない。
                if (IsDarkenTransitioning)
                {
                    _additionalRoundStartDarkenedCallbacks += onDarkened;
                    _additionalRoundStartReadyCallbacks += onReady;
                }
                else
                {
                    onDarkened?.Invoke();
                    onReady?.Invoke();
                }
                return;
            }
            isDarkened = true;
            IsScreenDarkened = true;
            IsDarkenTransitioning = true;
            StartCoroutine(RoundStartDarkenRoutine(text, onDarkened, onReady));
        }


        public void PlayRoundStartFadeOut(Action onComplete = null)
        {
            // **暗転していなくても、裏牌の山だけは必ず片付ける（2026-10-01）。**
            //
            // 配牌完了の進行管理はここを1回しか叩かない。
            // 暗転していないと下の `return` で抜けていたため、
            // そのあとに置かれた山を片付ける人が誰もいなくなり、
            // **手牌選択フェイズの盤面に散らばったまま残った**（実機で確認）。
            //
            // 旗も先に立てる。これ以降は「置く側」が置かずに済む
            // （PhaseTransitionUI.Darken / GameUIPhaseController.RoundFlow）。
            MarkRoundStartCleanupDone();

            // **降りている最中なら、上がりきるまで晴らさない（2026-10-04）。**
            //
            // サーバーの配牌が十数msになり、暗転が降りきる前に配牌完了が届くように
            // なった。そのまま晴らしに入ると、暗転が画面を覆いきらないうちに
            // 折り返すので、**そろった盤面が暗転にかぶる前に見えてしまう**
            // （ユーザー報告「暗転する前に全部そろっている画面が映っています」）。
            //
            // ここで待つのは「降りきるまで」だけ。**待ちを増やしているのではなく、
            // 順番を戻しているだけ**で、降りきったあとはすぐ晴らしに入る。
            if (IsDarkenTransitioning)
            {
                if (!_fadeOutPendingUntilDarkened)
                {
                    _fadeOutPendingUntilDarkened = true;
                    StartCoroutine(FadeOutAfterDarkenRoutine(onComplete));
                }
                return;
            }

            if (!isDarkened)
            {
                onComplete?.Invoke();
                return;
            }
            isDarkened = false;
            IsScreenDarkened = false;
            StartCoroutine(RoundStartFadeOutRoutine(onComplete));
        }

        public void ChangeDarkenText(string text)
        {
            if (isDarkened && centerText != null)
            {
                centerText.text = text;
            }
        }


        /// <summary>
        /// ロン後の点数精算演出を再生する。
        /// 掛け金フェイズのHP減少演出と対になる形で、勝者のHPが増加し敗者のHPが減少するアニメーション。
        /// </summary>
        /// <param name="isLocalWin">ローカルプレイヤーが勝者かどうか</param>
        /// <param name="winnerGain">勝者の獲得点数</param>
        /// <param name="loserLoss">敗者の喪失点数</param>
        /// <param name="prevLocalHp">演出開始時のローカルプレイヤーHP（精算前）</param>
        /// <param name="prevEnemyHp">演出開始時の敵プレイヤーHP（精算前）</param>
        /// <param name="newLocalHp">精算後のローカルプレイヤーHP</param>
        /// <param name="newEnemyHp">精算後の敵プレイヤーHP</param>
        /// <param name="resultLabel">表示する精算ラベル（例: "満貫"）</param>
        /// <param name="onComplete">演出完了コールバック</param>
        [Header("Effects Settings")]
        [SerializeField] private Sprite bloodSplatterSprite;
        [SerializeField] private Color dimmerColor = new Color(0, 0, 0, 0.7f);
        /// <summary>
        /// 自分のスキルで出す専用の絵。画面の何割まで使ってよいか。
        ///
        /// **迫力を出すため 0.72/0.62 から上げた（2026-10-02）。**
        /// 絵が横長なので、横長の画面ほど高さ側が先に効く。16:9 で画面幅の
        /// 45%→57%、4:3 で 60%→76% になる。21:9 や縦でもはみ出さないことは
        /// 計算で確かめてある。
        /// </summary>
        private const float CutinSpriteWidthRatio = 0.85f;
        private const float CutinSpriteHeightRatio = 0.78f;

        [SerializeField] private Sprite playerCutinSprite;
        [SerializeField] private Sprite playerTroubledSprite;

        public void PlayScoreSettlementAnimation(
            bool isLocalWin,
            int winnerGain,
            int loserLoss,
            int prevLocalHp,
            int prevEnemyHp,
            int newLocalHp,
            int newEnemyHp,
            string resultLabel,
            Action onComplete)
        {
            StartCoroutine(ScoreSettlementRoutine(
                isLocalWin, winnerGain, loserLoss,
                prevLocalHp, prevEnemyHp,
                newLocalHp, newEnemyHp,
                resultLabel, onComplete));
        }



        public void PlayDrawTransition(Action onMidpoint, Action onComplete)
        {
            StartCoroutine(DrawTransitionRoutine(onMidpoint, onComplete));
        }


        public void PlayPromptText(string text, float duration = 2.0f)
        {
            StartCoroutine(PlayPromptTextRoutine(text, duration));
        }


        public void PlaySkillCutinAnimation(string skillName, bool isLocalPlayer, CharacterData characterData = null, float duration = 2.0f, Action onComplete = null)
        {
            StartCoroutine(PlaySkillCutinAnimationRoutine(skillName, isLocalPlayer, characterData, duration, onComplete));
        }

        public IEnumerator PlaySkillCutinAnimationRoutine(string skillName, bool isLocalPlayer, CharacterData characterData = null, float duration = 2.0f, Action onComplete = null, string subText = null)
        {
            using (var scope = skillPresentations.Begin())
                yield return scope.Run(SkillCutinVisualsRoutine(scope, skillName, isLocalPlayer, characterData, duration, onComplete, subText));
        }

        /// <summary>
        /// 相手のカットインの立ち絵の高さ（Canvas の単位、4:3 のとき）。絵の全体でこの高さ。
        ///
        /// **頭から裾までが映り、絵の下端だけが少し隠れる大きさ**（2026-10-09 のユーザー指示）。
        /// 立ち絵は膝の上で切れている絵なので、下端まで映すと「脚が途中で切れている」のが見えてしまう。
        /// 下端の 6% ほどを、帯と画面の下へ隠す（<see cref="EnemyCutinPortraitPosition"/>）。
        ///
        /// 同じ日の経緯: 1406（膨らんでいた）→ 400（小さいと言われた）→ 1000（ロンと同じ。顔と胸元だけ）
        /// → 480（全身を帯の中に。脚の切れ目が見えると言われた）→ 560。
        /// </summary>
        private const float EnemyCutinPortraitHeight = 560f;

        /// <summary>
        /// 相手のカットインの立ち絵を置く位置（絵の下端の中央。**画面の中心から**、4:3 のとき）。
        ///
        /// **縦は、絵の下端を画面の下（-300）より 34 だけ下へ出す。** 高さ 560 の 6%。
        /// 脚の切れ目がちょうど隠れ、裾のフリルは残る。横は左寄りで、左の翼の先が画面の端に来る。
        /// 高さを変えたら、縦は「-300 − 高さ × 0.06」で出し直すこと。
        /// </summary>
        private static readonly Vector2 EnemyCutinPortraitPosition = new Vector2(-130f, -334f);

        /// <summary>カットインの帯の傾き（度）。立ち絵を帯の中に置くときの座標の向きに使う。</summary>
        private const float CutinStripeTilt = 15f;

        /// <summary>カットインの寸法を決めたときの Canvas の高さ（800x600 の 600）。</summary>
        private const float CutinReferenceHeight = 600f;

        /// <summary>
        /// `Image.SetNativeSize()` が決める大きさ（Canvas の単位）を、スプライトから計算する。
        /// **`sprite.rect`（画素数）をそのまま大きさとして使ってはいけない。**
        /// pixelsPerUnit が 100 でない絵や、取り込みで縮めた絵では、画素数と大きさが一致しない。
        /// </summary>
        private static Vector2 NativeSizeOf(Sprite sprite, Canvas canvas)
        {
            if (sprite == null) return Vector2.zero;
            float reference = canvas != null ? canvas.referencePixelsPerUnit : 100f;
            float ppu = sprite.pixelsPerUnit > 0f ? sprite.pixelsPerUnit : 100f;
            return sprite.rect.size / ppu * reference;
        }

        private IEnumerator SkillCutinVisualsRoutine(PresentationScope scope, string skillName, bool isLocalPlayer, CharacterData characterData, float duration, Action onComplete, string subText)
        {
            ResetVisuals();

            // 1. コンテナ作成
            GameObject container = scope.Own(new GameObject("DeathGameCutinContainer"));
            container.transform.SetParent(transform, false);
            container.transform.SetAsLastSibling();
            RectTransform containerRt = container.AddComponent<RectTransform>();
            containerRt.anchorMin = Vector2.zero;
            containerRt.anchorMax = Vector2.one;
            containerRt.sizeDelta = Vector2.zero;

            Canvas containerCanvas = container.AddComponent<Canvas>();
            container.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            containerCanvas.overrideSorting = true;
            containerCanvas.sortingOrder = UISortingOrders.PhaseTransitionTop;

            // 2. 即時ディマー（背景が少し見えるように半透明）
            GameObject dimmer = new GameObject("Dimmer");
            dimmer.transform.SetParent(containerRt, false);
            Image dimmerImg = dimmer.AddComponent<Image>();
            dimmerImg.color = new Color(0, 0, 0, 0.5f);
            RectTransform dimmerRt = dimmer.GetComponent<RectTransform>();
            dimmerRt.anchorMin = Vector2.zero;
            dimmerRt.anchorMax = Vector2.one;
            dimmerRt.sizeDelta = Vector2.zero;

            // 3. 大迫力の斜め背景エフェクト（左下から右上へ）と血飛沫
            List<RectTransform> bgElements = new List<RectTransform>();
            List<Vector2> bgTargetPos = new List<Vector2>();
            List<Vector2> bgStartPos = new List<Vector2>();

            // 斜めの帯（カットイン）背景
            GameObject bgStripeObj = new GameObject("BgStripe");
            bgStripeObj.transform.SetParent(containerRt, false);
            Image stripeImg = bgStripeObj.AddComponent<Image>();
            
            if (isLocalPlayer)
            {
                stripeImg.color = new Color32(10, 80, 200, 255); // 鮮やかな青
            }
            else
            {
                stripeImg.color = new Color32(180, 10, 10, 255); // 鮮やかな赤
            }

            RectTransform stripeRt = bgStripeObj.GetComponent<RectTransform>();
            // 画面を覆い尽くす長方形から、帯状（バナー）に変更
            stripeRt.sizeDelta = new Vector2(6000f, 600f);
            stripeRt.localRotation = Quaternion.Euler(0, 0, CutinStripeTilt); // 傾きを少し緩やかに
            
            // 下から斜めに突き抜けるように配置
            Vector2 stripeTarget = new Vector2(0, 0);
            Vector2 stripeStart = new Vector2(0, -3000f);
            stripeRt.anchoredPosition = stripeStart;

            // 濃い色の帯が背景に溶けないよう黒で縁取る。
            // 帯はこのあと動かすので、縁も同じ動きに含める。
            var stripeEdge = KillingMahjong.Visuals.UIEdgeOutline.AddBehind(stripeRt, 10f);
            if (stripeEdge != null)
            {
                bgElements.Add(stripeEdge);
                bgStartPos.Add(stripeStart);
                bgTargetPos.Add(stripeTarget);
            }

            bgElements.Add(stripeRt);
            bgStartPos.Add(stripeStart);
            bgTargetPos.Add(stripeTarget);

            // 追加の血飛沫（少しだけ）
            if (bloodSplatterSprite != null && splatterCount > 0)
            {
                for (int i = 0; i < splatterCount; i++)
                {
                    GameObject splatterObj = new GameObject("Splatter_" + i);
                    splatterObj.transform.SetParent(containerRt, false);
                    Image spImg = splatterObj.AddComponent<Image>();
                    spImg.sprite = bloodSplatterSprite;
                    spImg.color = isLocalPlayer ? new Color32(10, 80, 200, 180) : new Color32(180, 10, 10, 180);
                    spImg.preserveAspect = true;

                    RectTransform spRt = splatterObj.GetComponent<RectTransform>();
                    float size = UnityEngine.Random.Range(splatterSizeMin, splatterSizeMax);
                    spRt.sizeDelta = new Vector2(size, size);
                    spRt.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0, 360f));
                    
                    Vector2 spTarget = new Vector2(UnityEngine.Random.Range(-300f, 300f), UnityEngine.Random.Range(-200f, 200f));
                    Vector2 spStart = spTarget + new Vector2(0, -2500f);
                    
                    bgElements.Add(spRt);
                    bgStartPos.Add(spStart);
                    bgTargetPos.Add(spTarget);
                }
            }

            // 4. キャラクター立ち絵
            Sprite bodySprite = null;
            Sprite faceSprite = null;

            // **自分のスキルは専用の絵を出す（2026-10-02 の指示）。**
            // 以前は characterData（＝自分のキャラの立ち絵）が優先され、
            // `playerCutinSprite` は characterData が無いときの控えでしかなかった。
            // 自分が撃つときだけは、立ち絵ではなく専用の絵にする。
            // 顔は重ねない（立ち絵用の差分なので、この絵には合わない）。
            bool usingCutinSprite = false;
            if (isLocalPlayer && playerCutinSprite != null)
            {
                bodySprite = playerCutinSprite;
                usingCutinSprite = true;
            }
            else if (characterData != null)
            {
                var bodyMatch = characterData.bodySprites?.Find(x => x.id == characterData.defaultBodyId);
                bodySprite = bodyMatch != null ? bodyMatch.sprite : characterData.normalSprite;

                var faceMatch = characterData.faceSprites?.Find(x => x.id == characterData.defaultFaceId);
                if (faceMatch != null) faceSprite = faceMatch.sprite;
            }
            else
            {
                bodySprite = isLocalPlayer ? playerCutinSprite : playerTroubledSprite;
            }

            GameObject portraitObj = null;
            RectTransform portraitRt = null;
            Vector2 portraitTargetPos = Vector2.zero;
            Vector2 portraitStartPos = Vector2.zero;

            if (bodySprite != null)
            {
                portraitObj = new GameObject("Portrait");
                portraitObj.transform.SetParent(containerRt, false);
                Image portraitImg = portraitObj.AddComponent<Image>();
                portraitImg.sprite = bodySprite;
                portraitImg.SetNativeSize();

                portraitRt = portraitObj.GetComponent<RectTransform>();

                if (usingCutinSprite)
                {
                    // **専用の絵は画面に収める（2026-10-02）。**
                    // 立ち絵と違って見切れてよい絵ではないし、
                    // CanvasScaler が幅基準（match=0・基準 800x600）なので、
                    // 16:9 では**キャンバスの高さが 450 単位まで縮む**。
                    // 高さ 800 の決め打ちだと、その時点ではみ出す。
                    float areaW = containerRt.rect.width;
                    float areaH = containerRt.rect.height;
                    // 画素数ではなく、SetNativeSize が決めた大きさ（Canvas の単位）で測る。理由は下の相手側と同じ
                    Vector2 native = NativeSizeOf(bodySprite, containerCanvas);
                    float spriteW = Mathf.Max(native.x, 1f);
                    float spriteH = Mathf.Max(native.y, 1f);

                    // 幅・高さの両方で頭打ちにして、狭いほうに合わせる
                    float fit = Mathf.Min(areaW * CutinSpriteWidthRatio / spriteW,
                                          areaH * CutinSpriteHeightRatio / spriteH);
                    portraitRt.localScale = new Vector3(fit, fit, 1f);

                    // 画面中央。看板を掲げる絵なので、寄せずに真ん中で見せる
                    portraitRt.pivot = new Vector2(0.5f, 0.5f);
                    portraitRt.anchorMin = new Vector2(0.5f, 0.5f);
                    portraitRt.anchorMax = new Vector2(0.5f, 0.5f);
                    portraitTargetPos = Vector2.zero;
                    portraitStartPos = portraitTargetPos + new Vector2(0f, -areaH);
                }
                else
                {
                    portraitRt.pivot = new Vector2(0.5f, 0f); // 下端中央

                    // **大きさは Canvas の単位で決める（2026-10-09 に直した）。**
                    //
                    // 以前は「絵の画素数」から倍率を出していた（800 ÷ 画素の高さ）。
                    // ところが SetNativeSize が決める大きさは 画素数 ÷ pixelsPerUnit × 100 で、
                    // 取り込みの上限（maxTextureSize）を下げると**画素数だけが減る**。
                    // 2026-09-27 に上限を 4096 → 1024 にしたとき、倍率が 800/3600 → 800/1024 に変わり、
                    // 立ち絵が 400 → 1406 単位（約3.5倍）に膨らんで、胴体しか映らなくなっていた
                    // （コレクションの試写でユーザーが気づいた）。
                    // SetNativeSize の直後は rect が未確定なことがあるので、スプライトから計算する。
                    float nativeHeight = NativeSizeOf(bodySprite, containerCanvas).y;

                    // **画面の高さに比例させる。** Canvas は幅基準（800 固定）なので、横長の画面では
                    // 高さが 600 より小さくなる（16:9 で 450）。決め打ちだと、そのぶん大きく見える。
                    // 4:3 のとき k=1
                    float k = containerRt.rect.height > 1f ? containerRt.rect.height / CutinReferenceHeight : 1f;
                    float scale = nativeHeight > 0f ? EnemyCutinPortraitHeight * k / nativeHeight : 1f;
                    portraitRt.localScale = new Vector3(scale, scale, 1f);

                    // **立ち絵は帯の中だけに映す（2026-10-09 のユーザー指示）。**
                    // 帯と同じ形の「切り抜き枠」を重ね、立ち絵をその子にする。帯そのものの子にしないのは、
                    // 帯のすぐ上に血飛沫が描かれるため（子にすると、立ち絵の上に血飛沫が乗る）。
                    // 枠は帯と同じ動きをさせるので、立ち絵は帯に乗ったまま下から突き上がってくる
                    var maskObj = new GameObject("PortraitMask", typeof(RectTransform), typeof(Image), typeof(Mask));
                    maskObj.transform.SetParent(containerRt, false);
                    var maskRt = (RectTransform)maskObj.transform;
                    maskRt.sizeDelta = stripeRt.sizeDelta;
                    maskRt.localRotation = stripeRt.localRotation;
                    maskRt.anchoredPosition = stripeStart;
                    maskObj.GetComponent<Image>().raycastTarget = false;
                    maskObj.GetComponent<Mask>().showMaskGraphic = false;   // 枠そのものは描かない
                    bgElements.Add(maskRt);
                    bgStartPos.Add(stripeStart);
                    bgTargetPos.Add(stripeTarget);

                    portraitObj.transform.SetParent(maskRt, false);
                    portraitRt.anchorMin = new Vector2(0.5f, 0.5f);
                    portraitRt.anchorMax = new Vector2(0.5f, 0.5f);

                    // 枠は傾いているので、立ち絵は逆へ同じだけ回してまっすぐ立たせる。
                    // 置く位置も、画面の向きで決めた値を枠の向きへ直す
                    portraitRt.localRotation = Quaternion.Euler(0f, 0f, -CutinStripeTilt);
                    Vector2 onScreen = EnemyCutinPortraitPosition * k;
                    float rad = CutinStripeTilt * Mathf.Deg2Rad;
                    Vector2 inBand = new Vector2(
                        onScreen.x * Mathf.Cos(rad) + onScreen.y * Mathf.Sin(rad),
                        -onScreen.x * Mathf.Sin(rad) + onScreen.y * Mathf.Cos(rad));

                    // 帯の中では動かさない（帯ごと出入りする）
                    portraitTargetPos = inBand;
                    portraitStartPos = inBand;
                }

                portraitRt.anchoredPosition = portraitStartPos;
                
                // 立ち絵にテーマカラーのドロップシャドウ。
                // Shadow は立ち絵の四角を丸ごと複製するので、半透明で塗る面積が1枚ぶん増える
                if (portraitShadow)
                {
                    Shadow pShadow = portraitObj.AddComponent<Shadow>();
                    pShadow.effectColor = isLocalPlayer ? new Color32(0, 100, 255, 150) : new Color32(200, 0, 0, 150);
                    pShadow.effectDistance = new Vector2(20, -20);
                }

                // 顔画像も合成
                if (faceSprite != null)
                {
                    GameObject faceObj = new GameObject("Face");
                    faceObj.transform.SetParent(portraitRt, false);
                    Image faceImg = faceObj.AddComponent<Image>();
                    faceImg.sprite = faceSprite;
                    faceImg.SetNativeSize();

                    RectTransform faceRt = faceObj.GetComponent<RectTransform>();
                    faceRt.pivot = new Vector2(0.5f, 0f);
                    faceRt.anchorMin = new Vector2(0.5f, 0f);
                    faceRt.anchorMax = new Vector2(0.5f, 0f);
                    faceRt.anchoredPosition = Vector2.zero;
                }
            }

            // 5. メインテキスト（少し傾ける）
            GameObject mainTextObj = new GameObject("MainText");
            mainTextObj.transform.SetParent(containerRt, false);
            TextMeshProUGUI mainText = mainTextObj.AddComponent<TextMeshProUGUI>();
            mainText.text = skillName;
            mainText.fontSize = KillingMahjong.Common.UITypography.Title; // スケールを1にする代わりにフォントサイズを大きくする
            mainText.color = new Color32(255, 255, 255, 0); // 白色字
            mainText.fontStyle = FontStyles.Bold;
            mainText.alignment = TextAlignmentOptions.Right;
            if (centerText != null) mainText.font = centerText.font;

            // 黒い影（読みやすさのため常に付ける）
            Shadow txtShadow1 = mainTextObj.AddComponent<Shadow>();
            txtShadow1.effectColor = new Color(0, 0, 0, 1f);
            txtShadow1.effectDistance = new Vector2(10, -10);

            // テーマカラーの影（ズレ）。Shadow 1枚ごとに文字のメッシュが複製されるので、
            // 文字色を毎フレーム変えている間はその枚数ぶん作り直しが走る
            if (textThemeShadow)
            {
                Shadow txtShadow2 = mainTextObj.AddComponent<Shadow>();
                txtShadow2.effectColor = isLocalPlayer ? new Color32(0, 100, 255, 150) : new Color32(200, 0, 0, 150);
                txtShadow2.effectDistance = new Vector2(-10, 8);
            }

            RectTransform mainRt = mainText.GetComponent<RectTransform>();
            mainRt.anchorMin = new Vector2(1f, 1f);
            mainRt.anchorMax = new Vector2(1f, 1f);
            mainRt.pivot = new Vector2(1f, 1f);
            mainRt.sizeDelta = new Vector2(500, 150); // 幅を絞る
            mainRt.anchoredPosition = new Vector2(-100, string.IsNullOrEmpty(subText) ? -120 : -80); // さらに下・左へ移動
            mainRt.localRotation = Quaternion.Euler(0, 0, -10f); // 少し傾ける
            mainRt.localScale = Vector3.one; // スケールは1にする

            // 6. サブテキスト（役の名前など）
            TextMeshProUGUI subTextUI = null;
            CanvasGroup subCg = null;
            if (!string.IsNullOrEmpty(subText))
            {
                GameObject subObj = new GameObject("SubText");
                subObj.transform.SetParent(containerRt, false);
                subCg = subObj.AddComponent<CanvasGroup>();
                subCg.alpha = 0f;
                subTextUI = subObj.AddComponent<TextMeshProUGUI>();
                subTextUI.text = subText;
                subTextUI.fontSize = KillingMahjong.Common.UITypography.Header; // 適切なサイズに
                subTextUI.color = new Color32(255, 255, 255, 255);
                subTextUI.fontStyle = FontStyles.Bold;
                subTextUI.alignment = TextAlignmentOptions.Right;
                if (centerText != null) subTextUI.font = centerText.font;

                Shadow s1 = subObj.AddComponent<Shadow>();
                s1.effectColor = new Color(0, 0, 0, 1f);
                s1.effectDistance = new Vector2(10, -10);

                if (textThemeShadow)
                {
                    Shadow s2 = subObj.AddComponent<Shadow>();
                    s2.effectColor = isLocalPlayer ? new Color32(0, 100, 255, 150) : new Color32(200, 0, 0, 150);
                    s2.effectDistance = new Vector2(-10, 8);
                }

                RectTransform subRt = subTextUI.GetComponent<RectTransform>();
                subRt.anchorMin = new Vector2(1f, 1f);
                subRt.anchorMax = new Vector2(1f, 1f);
                subRt.pivot = new Vector2(1f, 1f);
                subRt.sizeDelta = new Vector2(500, 100);
                subRt.anchoredPosition = new Vector2(-100, -180); // メインテキストの下へ
                subRt.localRotation = Quaternion.Euler(0, 0, -10f); // メインテキストと同じ傾き
                subRt.localScale = Vector3.one;
            }

            // --- 暴力的なアニメーション開始 ---
            float t = 0;
            float impactDuration = 0.15f;

            // スライドを廃止し、背景要素と立ち絵は下から「ばっ！」と突き上げる
            for (int i = 0; i < bgElements.Count; i++) bgElements[i].anchoredPosition = bgStartPos[i];
            if (portraitRt != null) portraitRt.anchoredPosition = portraitStartPos;

            // 最初の1フレームを空けて、生成直後の Canvas 再構築を演出の外へ出す。
            // ここを入れないと、生成コストが演出の1フレーム目に乗って出だしが硬くなる。
            yield return null;

            // 画面揺れ
            StartCoroutine(ScreenShakeRoutine(0.2f, 15f));

            while (t < impactDuration)
            {
                float progress = t / impactDuration;
                // 「下からばっ！と突き上げる」ので、最初に速く動いて着地で減速するイーズアウトを使う。
                // ここを Pow(progress, 3f) のイーズインにすると、移動距離が 3000px あるぶん
                // 序盤は画面外でほとんど動いて見えず、最後の3割で一気に飛び込む。
                // 「少し出て、止まって、一気に全部出る」というガクついた見え方はこれが原因だった。
                // ロン演出のスタンプ表現（RonAnimationUI）も同じくイーズアウトで揃えてある。
                float easeIn = 1f - Mathf.Pow(1f - progress, 3f);

                // 文字が叩きつけられる演出（スケール5から1へ）
                mainRt.localScale = Vector3.LerpUnclamped(new Vector3(5f, 5f, 1f), Vector3.one, easeIn);
                mainText.color = new Color32(255, 255, 255, (byte)(255 * progress));

                // 背景と立ち絵の下からのスライドイン
                for (int i = 0; i < bgElements.Count; i++)
                {
                    bgElements[i].anchoredPosition = Vector2.LerpUnclamped(bgStartPos[i], bgTargetPos[i], easeIn);
                }
                if (portraitRt != null)
                {
                    portraitRt.anchoredPosition = Vector2.LerpUnclamped(portraitStartPos, portraitTargetPos, easeIn);
                }

                if (subCg != null)
                {
                    subCg.alpha = progress; // サブテキストはフェードインのみ（叩きつけない）
                }
                
                t += Time.deltaTime;
                yield return null;
            }

            mainRt.localScale = Vector3.one;
            mainText.color = Color.white;

            // 着弾の瞬間に揺れ（ダメ押し）
            StartCoroutine(ScreenShakeRoutine(0.15f, 20f));

            // 少し待機（文字を見せる時間）
            float waitTime = Mathf.Max(0, duration - impactDuration - 0.2f);
            yield return new WaitForSeconds(waitTime);

            // 退出アニメーション（一瞬でガラスが割れるように消える、または画面外へ吹き飛ぶ）
            t = 0;
            float outDuration = 0.2f;
            while (t < outDuration)
            {
                float progress = t / outDuration;
                float easeIn = progress * progress * progress;

                dimmerImg.color = new Color(0, 0, 0, 0.85f * (1f - progress));

                // 外側に吹き飛ぶ
                for (int i = 0; i < bgElements.Count; i++)
                {
                    bgElements[i].anchoredPosition = Vector2.Lerp(bgTargetPos[i], bgStartPos[i], easeIn);
                }
                if (portraitRt != null)
                {
                    portraitRt.anchoredPosition = Vector2.Lerp(portraitTargetPos, portraitStartPos, easeIn);
                }
                
                // メインテキストはさらに傾きながら下へ落ちる
                mainRt.anchoredPosition = new Vector2(0, (string.IsNullOrEmpty(subText) ? 50 : 150) - (1000 * easeIn));
                mainRt.localRotation = Quaternion.Euler(0, 0, -15f - (30f * easeIn));

                if (subCg != null)
                {
                    subCg.alpha = 1f - progress;
                }
                
                t += Time.deltaTime;
                yield return null;
            }

            Destroy(container);
            onComplete?.Invoke();
        }
    }
}
