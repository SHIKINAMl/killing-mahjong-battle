using UnityEngine;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class PlayerInfoUI : MonoBehaviour
    {
        [Header("HP Display")]
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private UnityEngine.UI.Image hpFillImage; // 追加: 人型のHPメーター用画像
        private int maxHp = 20000; // 最大HP（割合計算用）

        [Header("Boost Bonus")]
        [SerializeField] private TextMeshProUGUI boostBonusText; // 動的生成も可

        [Header("Ready Mark")]
        [SerializeField] private GameObject readyBoxContainer;
        [SerializeField] private GameObject readyCheckImage;

        [Header("Zoom Target")]
        [SerializeField] private Transform zoomTarget; // 追加：拡大させたい子オブジェクトを指定
        [SerializeField] private Vector3 zoomedLocalPos = new Vector3(0, 50f, 0f);
        [SerializeField] private Vector3 zoomedScale = new Vector3(1.2f, 1.2f, 1.0f);
        [SerializeField] private Vector3 zoomOffsetUI = new Vector3(-1200f, 100f, -500f); // ズーム時に手前に出すためVector3に変更
        [SerializeField] private Vector3 zoomOffsetWorld = new Vector3(-4.0f, 1.0f, -2.0f); // 3D時の移動量

        [Header("Prefabs")]
        [SerializeField] private GameObject damagePopupPrefab;

        [Tooltip("HP増減ポップアップの出現基準。未設定なら zoomTarget（スマホ）を使う。")]
        [SerializeField] private RectTransform damagePopupAnchor;

        [Header("Character Portrait")]
        [SerializeField] private SpriteRenderer characterRenderer;
        [SerializeField] private SpriteRenderer faceRenderer; // 追加：表情レイヤー用
        [SerializeField] private CharacterData characterData; // キャラクター管理データ

        [Header("Timer UI")]
        [SerializeField] private TimerUI timerUI; // インスペクターからセットする

        public CharacterData CurrentCharacterData => characterData;

        private int currentHp = 20000; // 暫定の初期HP
        private Sprite normalSprite;
        private Sprite discardSprite;
        private Sprite normalFaceSprite; // 通常時の顔画像
        
        private Coroutine bounceCoroutine;
        private Coroutine zoomCoroutine;
        private Coroutine blinkCoroutine;
        private Vector3 originalPosition;
        
        // ズーム用
        private Vector3 originalLocalPos;
        private Vector3 originalScale;
        private bool isZoomedIn = false; // 追加：ズーム状態を管理

        private bool isInitialized = false;

        private void OnEnable()
        {
            if (blinkCoroutine == null)
            {
                blinkCoroutine = StartCoroutine(BlinkRoutine());
            }
        }

        private void OnDisable()
        {
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }
        }

        private void LateUpdate()
        {
            // アニメーター等で強制的に戻されてしまうのを防ぐため、ズーム中は毎フレーム最後に上書きする
            if (isZoomedIn)
            {
                var myCanvas = GetComponent<Canvas>();
                if (myCanvas != null && myCanvas.sortingOrder != UISortingOrders.InfoPanelHighlight)
                {
                    myCanvas.sortingOrder = UISortingOrders.InfoPanelHighlight;
                }
            }
        }

        private System.Collections.IEnumerator BlinkRoutine()
        {
            while (true)
            {
                if (characterData == null || !characterData.enableBlink || faceRenderer == null)
                {
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                float waitTime = Random.Range(characterData.blinkIntervalMin, characterData.blinkIntervalMax);
                yield return new WaitForSeconds(waitTime);

                if (faceRenderer.sprite != normalFaceSprite)
                    continue;

                Sprite fullBlink = characterData.faceSprites?.Find(x => x.id == characterData.blinkFaceId)?.sprite;

                if (fullBlink != null)
                {
                    if (faceRenderer.sprite == normalFaceSprite)
                    {
                        faceRenderer.sprite = fullBlink;
                        yield return new WaitForSeconds(0.12f);
                    }

                    if (faceRenderer.sprite == fullBlink)
                    {
                        faceRenderer.sprite = normalFaceSprite;
                    }
                }
            }
        }

        private void Awake()
        {
            if (characterData != null)
            {
                ApplyCharacterData(characterData);
            }
            else if (characterRenderer != null)
            {
                normalSprite = characterRenderer.sprite;
                if (faceRenderer != null) normalFaceSprite = faceRenderer.sprite;
            }

            InitializeOriginalTransform();
        }

        private void ApplyCharacterData(CharacterData data)
        {
            if (data == null) return;

            characterData = data;
            normalSprite = data.normalSprite;
            discardSprite = data.discardSprite;
            
            Sprite defaultBody = null;
            Sprite defaultFace = null;
            
            if (data.bodySprites != null && data.bodySprites.Count > 0)
            {
                var match = data.bodySprites.Find(x => x.id == data.defaultBodyId);
                if (match != null) defaultBody = match.sprite;
                else defaultBody = data.bodySprites[0].sprite;
                normalSprite = defaultBody; // 上書き
            }
            
            if (data.faceSprites != null && data.faceSprites.Count > 0)
            {
                var match = data.faceSprites.Find(x => x.id == data.defaultFaceId);
                if (match != null) defaultFace = match.sprite;
                else defaultFace = data.faceSprites[0].sprite;
                normalFaceSprite = defaultFace;
            }

            if (characterRenderer != null && normalSprite != null)
            {
                characterRenderer.sprite = normalSprite;
            }
            
            if (faceRenderer != null && normalFaceSprite != null)
            {
                faceRenderer.sprite = normalFaceSprite;
            }
        }

        private void InitializeOriginalTransform()
        {
            if (isInitialized) return;

            if (characterRenderer != null)
            {
                originalPosition = characterRenderer.transform.localPosition;
            }

            Transform targetObj = zoomTarget != null ? zoomTarget : transform;
            originalLocalPos = targetObj.localPosition;
            originalScale = targetObj.localScale;

            // UI要素が前面に出て牌のクリック判定を吸い取るのを防ぐため、当たり判定を無効化する
            // ただし、ボタンとして機能すべき要素（自身や親にButtonコンポーネントを持つ）は除外する
            var graphics = GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            foreach (var g in graphics)
            {
                if (g.GetComponentInParent<UnityEngine.UI.Button>(true) != null)
                {
                    continue;
                }
                g.raycastTarget = false;
            }

            isInitialized = true;
        }

        private void Start()
        {
            InitializeOriginalTransform();

            // **ここで先に作っておく。** ダメージを受けてから作ったのでは、
            // 混ぜるための「過去の画面」が1枚も溜まっていない。
            // 作った時点から保存が始まるので、最初の一撃から効く
            HpDamageGlitch.Ensure();
        }

        public void StartTurnTimer(float duration)
        {
            if (timerUI != null)
            {
                timerUI.StartTimer(duration);
            }
        }

        public void StopTurnTimer()
        {
            if (timerUI != null)
            {
                timerUI.StopTimer();
            }
        }

        public void SetMaxHP(int max)
        {
            maxHp = max;
            // TutorialManager.ApplyHpToUI から同じ値で何度も呼ばれるので、
            // ここで到達最高HPを引き下げてはいけない（メーターの分母が戻ってしまう）。
            hpPeak = Mathf.Max(hpPeak, max);
            // 新しい対局が始まるのでビネットの抑止を解除する
            heartbeatSuppressed = false;
        }

        /// <summary>新しい対局の開始時に呼ぶ。メーターの分母（到達最高HP）も引き直す。</summary>
        public void ResetHpMeter(int max)
        {
            maxHp = max;
            hpPeak = max;
            heartbeatSuppressed = false;
        }

        // ロンで血を奪うと開始HPを超えるため、到達した最高HPまでメーターの分母を広げる。
        // 分母を開始HP固定にすると fillAmount が1で頭打ちになり、
        // 33000 → 14000 のような大きな減少がメーター上で見えなくなる。
        // 瀕死ビネットとダメージSEの判定は「絶対量としてどれだけ残っているか」なので maxHp のまま。
        private int hpPeak;
        private int MeterMax => Mathf.Max(1, Mathf.Max(maxHp, hpPeak));

        [Header("Effects")]
        [SerializeField] private KillingMahjong.UI.Effects.HeartbeatEffect heartbeatEffect;

        // 決着後に SetHP が呼ばれてビネットが復活しないようにするフラグ
        private bool heartbeatSuppressed = false;

        public void SetHP(int hp)
        {
            // 初回セットアップ時はポップアップを出さないように、現在値が0かつhpがmaxHpと同じならスキップするなどの制御が必要ですが、
            // 今回は通信で更新されたときだけ出したいので、初期化かどうかの簡易判定を入れます。
            bool isFirstSetup = (currentHp == 0 && hp > 0);
            int diff = hp - currentHp;

            currentHp = hp;
            if (hpText != null)
            {
                // 数字のみ表示する
                hpText.text = currentHp.ToString();
            }
            
            // 人型メーターの割合を更新する
            if (hp > hpPeak) hpPeak = hp;
            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = (float)hp / MeterMax;
            }

            if (!isFirstSetup && diff != 0)
            {
                HpPopup.Report(diff, currentHp, maxHp);
            }

            // 減ったときだけノイズを走らせる。増えた（勝った）ときは出さない
            if (!isFirstSetup && diff < 0)
            {
                HpDamageGlitch.Play(diff, maxHp);
            }

            // --- 瀕死ハートビートエフェクトの更新 ---
            if (heartbeatEffect != null && !heartbeatSuppressed)
            {
                heartbeatEffect.UpdateHeartbeat(currentHp, maxHp);
            }
        }

        /// <summary>
        /// 決着時など、HPに関係なくビネットを消したいときに呼ぶ。
        /// ビネット(91)は勝敗Canvas(55)より手前なので、消さないと結果画面に被る。
        /// ロン演出中は SetHP が毎フレーム呼ばれるため、次の SetMaxHP まで再開を抑止する。
        /// </summary>
        public void StopHeartbeatEffect()
        {
            heartbeatSuppressed = true;
            if (heartbeatEffect != null)
            {
                heartbeatEffect.StopEffect();
            }
        }

        /// <summary>
        /// HP増減のポップアップとSEをまとめる。ロン演出中は毎フレーム SetHP が呼ばれるため、
        /// 変化が落ち着くまで溜めてから1回だけ表示する（HpPopupPresenter 側で処理）。
        /// </summary>
        private HpPopupPresenter hpPopup;
        private HpPopupPresenter HpPopup
        {
            get
            {
                if (hpPopup == null)
                {
                    // このコンポーネントは全画面のルートCanvasに付いているので、
                    // transform を基準にすると相手側と同じ画面中央に出てしまう。
                    // HPが見えているスマホ（HPPanel）を基準にする。
                    RectTransform anchor = damagePopupAnchor != null
                        ? damagePopupAnchor
                        : zoomTarget as RectTransform;

                    hpPopup = new HpPopupPresenter(this, transform as RectTransform, anchor,
                                                   damagePopupPrefab, new Vector2(0, 60f), isLocalPlayer: true);
                }
                return hpPopup;
            }
        }


        public void ReduceHp(int amount)
        {
            currentHp -= amount;
            SetHP(currentHp);
        }

        public void SetCharacterSprite(Sprite sprite)
        {
            // 旧互換として残す
            if (characterRenderer != null && sprite != null)
            {
                characterRenderer.sprite = sprite;
            }
        }

        public void SetBodyPose(string poseId)
        {
            if (characterData == null || characterRenderer == null) return;
            var match = characterData.bodySprites?.Find(x => x.id == poseId);
            if (match != null && match.sprite != null)
            {
                characterRenderer.sprite = match.sprite;
            }
        }

        public void SetFaceExpression(string expressionId)
        {
            if (characterData == null || faceRenderer == null) return;
            var match = characterData.faceSprites?.Find(x => x.id == expressionId);
            if (match != null && match.sprite != null)
            {
                faceRenderer.sprite = match.sprite;
            }
        }

        /// <summary>
        /// 打牌する時の画像（あるいは元の画像）に切り替える
        /// </summary>
        public void SetDiscardingState(bool isDiscarding)
        {
            if (characterRenderer != null)
            {
                // 実行中に変更された場合も反映されるように、characterDataから直接読み取る
                Sprite targetDiscardSprite = (characterData != null && characterData.discardSprite != null) ? characterData.discardSprite : discardSprite;
                Sprite targetNormalSprite = (characterData != null && characterData.normalSprite != null) ? characterData.normalSprite : normalSprite;

                if (isDiscarding && targetDiscardSprite != null)
                {
                    characterRenderer.sprite = targetDiscardSprite;
                }
                else if (!isDiscarding && targetNormalSprite != null)
                {
                    characterRenderer.sprite = targetNormalSprite;
                }
            }
        }

        public void PlayBounceAnimation(float duration)
        {
            if (characterRenderer == null) return;
            if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
            bounceCoroutine = StartCoroutine(BounceRoutine(duration));
        }

        private System.Collections.IEnumerator BounceRoutine(float duration)
        {
            float elapsed = 0f;
            float bounceSpeed = 15f;
            float bounceHeight = 0.5f; // SpriteRenderer用に調整 
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float yOffset = Mathf.Abs(Mathf.Sin(elapsed * bounceSpeed)) * bounceHeight;
                characterRenderer.transform.localPosition = originalPosition + new Vector3(0, yOffset, 0);
                yield return null;
            }
            
            characterRenderer.transform.localPosition = originalPosition;
            bounceCoroutine = null;
        }

        /// <summary>強調表示時の前面化と復元。プロジェクトルールに従いルートCanvasの overrideSorting のみを操作する。</summary>
        private readonly CanvasSortingScope _sortingScope = new CanvasSortingScope();

        private void BringToFront(Transform target)
        {
            if (target == null) return;

            // ルートのCanvasのみを手前に出す（子Canvasを一律上書きすると表示順が壊れるため）
            _sortingScope.BringToFront(target.gameObject, UISortingOrders.InfoPanelHighlight);

            // 手やスマホ本体などのSpriteRendererを手前に持ってくる
            var sprites = target.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var s in sprites)
            {
                s.sortingOrder = UISortingOrders.InfoPanelHighlight;
            }
        }

        private void ResetSorting(Transform target)
        {
            if (target == null) return;

            _sortingScope.Restore(target.gameObject);

            var sprites = target.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var s in sprites)
            {
                s.sortingOrder = 0;
            }
        }


        // --- ズーム演出（指定したオブジェクトを巨大化し、少し手前・上に浮かせる） ---
        public System.Collections.IEnumerator ZoomInRoutine(float duration = 0.4f, float targetScaleMulti = 2.5f)
        {
            Debug.Log($"[PlayerInfoUI] ZoomInRoutine called! Target scale: {targetScaleMulti}");
            if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);
            
            isZoomedIn = true; // ズーム開始

            Transform targetObj = zoomTarget != null ? zoomTarget : transform;
            
            // ズーム対象が何であれ、PlayerInfoUI全体を最前面に出す
            BringToFront(transform);

            // 強制的にCanvasのSortOrderを引き上げる（インスペクターの値のままになる現象の回避）
            var myCanvas = GetComponent<Canvas>();
            if (myCanvas != null)
            {
                myCanvas.overrideSorting = true;
                myCanvas.sortingOrder = UISortingOrders.InfoPanelHighlight;
            }

            // 0の場合の安全対策
            if (originalScale == Vector3.zero) originalScale = Vector3.one;

            // ズーム中は揺れを止める
            var floatAnims = GetComponentsInChildren<FloatingAnimator>(true);
            foreach (var anim in floatAnims)
            {
                anim.enabled = false;
            }

            // UI用（ピクセル単位）か、3D用かで移動量を変える必要がある
            RectTransform rt = targetObj.GetComponent<RectTransform>();
            Vector3 targetPos;
            if (rt != null)
            {
                targetPos = Vector3.zero;
            }
            else
            {
                targetPos = Vector3.zero;
            }

            Vector3 targetScale = originalScale * targetScaleMulti;

            float t = 0;
            while (t < duration)
            {
                float progress = t / duration;
                float eased = progress * progress * (3f - 2f * progress); // 滑らかに

                targetObj.localPosition = Vector3.Lerp(originalLocalPos, targetPos, eased);
                targetObj.localScale = Vector3.Lerp(originalScale, targetScale, eased);

                t += Time.deltaTime;
                yield return null;
            }

            targetObj.localPosition = targetPos;
            targetObj.localScale = targetScale;
        }

        public void ResetZoomImmediate()
        {
            isZoomedIn = false; // ズーム終了
            if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);
            Transform targetObj = zoomTarget != null ? zoomTarget : transform;
            var floatAnims = GetComponentsInChildren<FloatingAnimator>(true);
            foreach (var anim in floatAnims)
            {
                anim.enabled = true;
            }

            targetObj.localPosition = originalLocalPos;
            targetObj.localScale = originalScale;
            ResetSorting(transform);

            // ズーム解除時に確実に通常時の値へ戻す
            var myCanvas = GetComponent<Canvas>();
            if (myCanvas != null)
            {
                myCanvas.sortingOrder = UISortingOrders.InfoPanelNormal;
            }
        }

        public System.Collections.IEnumerator ResetZoomRoutine(float duration = 0.3f)
        {
            isZoomedIn = false; // ズーム終了
            if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);

            Transform targetObj = zoomTarget != null ? zoomTarget : transform;

            Vector3 startPos = targetObj.localPosition;
            Vector3 startScale = targetObj.localScale;

            float t = 0;
            while (t < duration)
            {
                float progress = t / duration;
                float eased = progress * progress * (3f - 2f * progress);

                targetObj.localPosition = Vector3.Lerp(startPos, originalLocalPos, eased);
                targetObj.localScale = Vector3.Lerp(startScale, originalScale, eased);

                t += Time.deltaTime;
                yield return null;
            }

            targetObj.localPosition = originalLocalPos;
            targetObj.localScale = originalScale;
            var floatAnims = GetComponentsInChildren<FloatingAnimator>(true);
            foreach (var anim in floatAnims)
            {
                anim.enabled = true;
            }
            ResetSorting(transform);

            // ズーム解除時に確実に通常時の値へ戻す
            var myCanvas = GetComponent<Canvas>();
            if (myCanvas != null)
            {
                myCanvas.sortingOrder = UISortingOrders.InfoPanelNormal;
            }
        }

        private ReadyBadge readyBadge;

        /// <summary>
        /// 「準備完了」の札。シーンの ReadyBoxContainer を実行時に組み直して使う。
        /// スマホ（zoomTarget）の真下に置く。
        /// </summary>
        private ReadyBadge EnsureReadyBadge()
        {
            if (readyBadge == null)
            {
                readyBadge = ReadyBadge.Attach(
                    readyBoxContainer, readyCheckImage,
                    zoomTarget as RectTransform, isSelf: true);
            }
            return readyBadge;
        }

        public void ShowReadyBox(bool show)
        {
            var badge = EnsureReadyBadge();
            if (badge != null)
            {
                badge.SetVisible(show);
                if (show) badge.SetReady(false); // 出した時点では未確定
                return;
            }

            // 札を作れなかったとき（参照未設定）は従来どおりの出し入れに落とす
            if (readyBoxContainer != null) readyBoxContainer.SetActive(show);
            if (readyCheckImage != null) readyCheckImage.SetActive(false);
        }

        public void SetReadyCheck(bool isReady)
        {
            var badge = EnsureReadyBadge();
            if (badge != null)
            {
                badge.SetReady(isReady);
                return;
            }

            if (readyCheckImage != null) readyCheckImage.SetActive(isReady);
        }

        /// <summary>ベット中のスマホ拡大に隠れるので、拡大している間だけ札を伏せる。</summary>
        public void SetReadyBoxSuppressed(bool suppressed)
        {
            var badge = EnsureReadyBadge();
            if (badge != null) badge.SetSuppressed(suppressed);
        }

        private TurnGlow turnGlow;

        /// <summary>
        /// 自分の手番のときスマホを青く脈打たせる。
        /// スマホ（zoomTarget = HPPanel）は FloatingAnimator で揺れているので、
        /// その中に影絵を敷いて揺れごと追従させる。
        /// </summary>
        public void SetTurnGlow(bool on)
        {
            if (turnGlow == null)
            {
                turnGlow = TurnGlow.Attach(zoomTarget as RectTransform, isSelf: true);
            }
            if (turnGlow != null) turnGlow.SetOn(on);
        }
    }
}
