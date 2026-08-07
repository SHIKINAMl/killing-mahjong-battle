using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public class EnemyInfoUI : MonoBehaviour
    {
        [Header("Enemy HP Display")]
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private UnityEngine.UI.Image hpFillImage; 
        private int maxHp = 20000; 
        private int currentHp = 20000;
        
        [Header("Boost Bonus")]
        [SerializeField] private TextMeshProUGUI boostBonusText; // 動的生成も可
        
        [Header("Zoom Target")]
        [SerializeField] private Transform zoomTarget; // 追加：拡大させたい子オブジェクトを指定
        [SerializeField] private Vector2 zoomOffsetUI = new Vector2(-1200f, 100f); // UI時の移動量
        [SerializeField] private Vector3 zoomOffsetWorld = new Vector3(-4.0f, 1.0f, -2.0f); // 3D時の移動量

        [Header("Character Portrait")]
        [SerializeField] private SpriteRenderer characterRenderer;
        [SerializeField] private SpriteRenderer faceRenderer; // 追加：表情レイヤー用

        [Header("Death Animation")]
        [Tooltip("死亡演出で体が落ちる距離（ワールド単位）。立ち絵の高さは約17.65。" +
                 "20だと上端が画面下ぎりぎり（viewport -0.04）なので、余裕を見て24にしている")]
        [SerializeField] private float deathFallDistance = 24f;
        [Tooltip("死亡演出で落ち切るまでの時間（秒）")]
        [SerializeField] private float deathFallDuration = 1.2f;
        [Header("Ready Mark")]
        [SerializeField] private GameObject readyBoxContainer;
        [SerializeField] private GameObject readyCheckImage;
        [SerializeField] private CharacterData characterData; // キャラクター管理データ
        [SerializeField] private float bounceDuration = 0.5f; // 上下する時間（インスペクターで設定可能）
        [SerializeField] private float bounceHeight = 0.5f;   // 上下する高さ（インスペクターで設定可能）

        [Header("Available Enemies")]
        [SerializeField] private CharacterData[] availableEnemies; // インスペクターで登録する敵キャラクターリスト
        private int currentEnemyIndex = -1; // -1 = デフォルトの characterData を使用中

        [Header("Enemy Panel Settings")]
        [SerializeField] private GameObject enemyPanel; // 敵パネルの参照

        [Header("Prefabs")]
        [Tooltip("HP増減のポップアップ。未設定でも実行時に簡易版が生成される。")]
        [SerializeField] private GameObject damagePopupPrefab;

        [Tooltip("HP増減ポップアップの出現基準。未設定なら zoomTarget → enemyPanel の順で使う。")]
        [SerializeField] private RectTransform damagePopupAnchor;

        private Sprite normalSprite;
        private Sprite discardSprite;
        private Sprite normalFaceSprite; // 通常時の顔画像
        
        private Coroutine bounceCoroutine;
        private Coroutine reactionCoroutine;
        private Coroutine zoomCoroutine;
        private Coroutine blinkCoroutine;
        private Vector3 originalPosition;

        // ズーム用
        private Vector3 originalLocalPos;
        private Vector3 originalScale;

        /// <summary>
        /// 現在選択されている CharacterData を取得する
        /// </summary>
        public CharacterData CurrentCharacterData => characterData;

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

        /// <summary>
        /// 倒れる演出。顔が消えて、体が下へ落ちていく。
        ///
        /// まばたき・表情差し替え・バウンドはどれも顔と体を触るので、先に全部止める。
        /// 止めないと落下中に顔が戻ったり、バウンドが体を元の位置へ引き戻したりする。
        /// 呼んだあとは立ち絵が画面外にあるので、そのままシーン遷移する前提。
        /// </summary>
        public System.Collections.IEnumerator PlayDeathRoutine()
        {
            if (blinkCoroutine != null) { StopCoroutine(blinkCoroutine); blinkCoroutine = null; }
            if (reactionCoroutine != null) { StopCoroutine(reactionCoroutine); reactionCoroutine = null; }
            if (bounceCoroutine != null) { StopCoroutine(bounceCoroutine); bounceCoroutine = null; }

            if (faceRenderer != null) faceRenderer.enabled = false;

            if (characterRenderer == null) yield break;

            Transform body = characterRenderer.transform;
            Vector3 start = body.position;
            Vector3 end = start + Vector3.down * deathFallDistance;

            float duration = Mathf.Max(0.01f, deathFallDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                // 落下は加速させる（等速だと「沈んでいく」ように見えて力が抜ける）
                body.position = Vector3.Lerp(start, end, p * p);
                yield return null;
            }
            body.position = end;
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

                if (reactionCoroutine != null || faceRenderer.sprite != normalFaceSprite)
                    continue;

                Sprite fullBlink = characterData.faceSprites?.Find(x => x.id == characterData.blinkFaceId)?.sprite;

                if (fullBlink != null)
                {
                    if (faceRenderer.sprite == normalFaceSprite && reactionCoroutine == null)
                    {
                        faceRenderer.sprite = fullBlink;
                        yield return new WaitForSeconds(0.12f);
                    }

                    if (faceRenderer.sprite == fullBlink && reactionCoroutine == null)
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
        }

        private void Start()
        {
            if (characterRenderer != null)
            {
                originalPosition = characterRenderer.transform.localPosition;
            }

            // UI要素が前面に出て牌のクリック判定を吸い取るのを防ぐため、当たり判定を無効化する
            var graphics = GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            foreach (var g in graphics)
            {
                g.raycastTarget = false;
            }
        }

        /// <summary>
        /// 次の敵キャラクターに切り替える。
        /// availableEnemies リストの中を順番にループする。
        /// </summary>
        public void CycleEnemy()
        {
            if (availableEnemies == null || availableEnemies.Length == 0) return;

            currentEnemyIndex = (currentEnemyIndex + 1) % availableEnemies.Length;
            ApplyCharacterData(availableEnemies[currentEnemyIndex]);
        }

        /// <summary>
        /// 指定した CharacterData を適用して画像を切り替える
        /// </summary>
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

        public string PlayReaction(ReactionTrigger trigger, float duration = 3.0f, string formatArg = "")
        {
            if (characterData == null || characterData.reactions == null || characterData.reactions.Count == 0)
                return null;

            // 条件に合うリアクションを全て抽出
            var matches = characterData.reactions.FindAll(r => r.trigger == trigger);
            if (matches.Count == 0) return null;

            // ランダムに1つ選ぶ
            var reaction = matches[Random.Range(0, matches.Count)];

            // 画像が設定されていれば一時的に変更
            if (!string.IsNullOrEmpty(reaction.faceExpressionId))
            {
                var match = characterData.faceSprites?.Find(x => x.id == reaction.faceExpressionId);
                var bodyMatch = characterData.bodySprites?.Find(x => x.id == reaction.bodyExpressionId);

                if ((match != null && match.sprite != null) || (bodyMatch != null && bodyMatch.sprite != null))
                {
                    if (reactionCoroutine != null) StopCoroutine(reactionCoroutine);
                    reactionCoroutine = StartCoroutine(TemporaryVisualRoutine(bodyMatch?.sprite, match?.sprite, duration));
                }
            }

            string text = reaction.dialogueText;
            Debug.Log($"[EnemyInfoUI] PlayReaction: trigger={trigger}, matchFound=true, dialogueText='{text}'");
            
            if (!string.IsNullOrEmpty(formatArg) && !string.IsNullOrEmpty(text))
            {
                text = string.Format(text, formatArg);
            }

            return string.IsNullOrEmpty(text) ? null : $"「{text}」";
        }

        private System.Collections.IEnumerator TemporaryVisualRoutine(Sprite newBody, Sprite newFace, float duration)
        {
            Sprite originalFace = (faceRenderer != null) ? normalFaceSprite : null;
            Sprite originalBody = normalSprite;

            if (faceRenderer != null && newFace != null)
                faceRenderer.sprite = newFace;
            
            if (characterRenderer != null && newBody != null)
                characterRenderer.sprite = newBody;

            yield return new WaitForSeconds(duration);

            if (faceRenderer != null && originalFace != null)
                faceRenderer.sprite = originalFace;
            
            if (characterRenderer != null && originalBody != null)
                characterRenderer.sprite = originalBody;
            
            reactionCoroutine = null;
        }

        public void PlayReactionWithVisual(Sprite newBody, Sprite newFace, float duration)
        {
            if (reactionCoroutine != null) StopCoroutine(reactionCoroutine);
            reactionCoroutine = StartCoroutine(TemporaryVisualRoutine(newBody, newFace, duration));
            PlayBounceAnimation(0.3f); // 0.3秒で一瞬跳ねる
        }

        public void PlayReactionWithVisualId(string bodyId, string faceId, float duration)
        {
            if ((string.IsNullOrEmpty(bodyId) && string.IsNullOrEmpty(faceId)) || characterData == null)
            {
                PlayBounceAnimation(0.3f);
                return;
            }
            
            Sprite newBody = null;
            if (!string.IsNullOrEmpty(bodyId))
            {
                var bodyMatch = characterData.bodySprites?.Find(x => x.id == bodyId);
                if (bodyMatch != null) newBody = bodyMatch.sprite;
            }

            Sprite newFace = null;
            if (!string.IsNullOrEmpty(faceId))
            {
                var faceMatch = characterData.faceSprites?.Find(x => x.id == faceId);
                if (faceMatch != null) newFace = faceMatch.sprite;
            }

            if (newBody != null || newFace != null)
            {
                PlayReactionWithVisual(newBody, newFace, duration);
            }
            else
            {
                PlayBounceAnimation(0.3f);
                Debug.LogWarning($"[EnemyInfoUI] Neither body '{bodyId}' nor face '{faceId}' found in CharacterData!");
            }
        }

        public void SetMaxHP(int max)
        {
            maxHp = max;
            // 同じ値で繰り返し呼ばれるので分母を引き下げない（PlayerInfoUI と同じ理由）
            hpPeak = Mathf.Max(hpPeak, max);
        }

        /// <summary>新しい対局の開始時に呼ぶ。メーターの分母（到達最高HP）も引き直す。</summary>
        public void ResetHpMeter(int max)
        {
            maxHp = max;
            hpPeak = max;
        }

        // PlayerInfoUI と同じ理由でメーターの分母だけ最高HPまで広げる（ダメージSEの判定は maxHp のまま）。
        private int hpPeak;
        private int MeterMax => Mathf.Max(1, Mathf.Max(maxHp, hpPeak));

        public void SetHP(int hp)
        {
            // 初回セットアップ（0 → 初期HP）ではポップアップを出さない。PlayerInfoUI と同じ判定。
            bool isFirstSetup = (currentHp == 0 && hp > 0);
            int diff = hp - currentHp;

            currentHp = hp;
            // 誰の血かを添える。理由と大きさの根拠は PlayerInfoUI.HpOwnerLabelScale を参照
            if (hpText != null) hpText.text = $"<size={PlayerInfoUI.HpOwnerLabelScale}>相手 </size>{currentHp}";

            // 人型メーターの割合を更新する
            if (hp > hpPeak) hpPeak = hp;
            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = (float)hp / MeterMax;
            }

            // 与えたダメージが敵側に一切表示されず、手応えが片側だけだったため追加。
            if (!isFirstSetup && diff != 0)
            {
                HpPopup.Report(diff, currentHp, maxHp);
            }
        }

        /// <summary>ロン演出中の毎フレーム更新をまとめて1回の表示にする（HpPopupPresenter 側で処理）。</summary>
        private HpPopupPresenter hpPopup;
        private HpPopupPresenter HpPopup
        {
            get
            {
                if (hpPopup == null)
                {
                    // PlayerInfoUI と同様、transform は全画面のルートCanvas。
                    // HPが見えている血袋（EnemyPanel）を基準にする。
                    RectTransform anchor = damagePopupAnchor;
                    if (anchor == null) anchor = zoomTarget as RectTransform;
                    if (anchor == null && enemyPanel != null) anchor = enemyPanel.transform as RectTransform;

                    hpPopup = new HpPopupPresenter(this, transform as RectTransform, anchor,
                                                   damagePopupPrefab, new Vector2(0, 60f), isLocalPlayer: false);
                }
                return hpPopup;
            }
        }



        public void SetPanelVisible(bool visible)
        {
            if (enemyPanel != null)
            {
                enemyPanel.SetActive(visible);
            }
            if (!visible)
            {
                ShowReadyBox(false);
            }
        }

        private ReadyBadge readyBadge;

        /// <summary>
        /// 「準備完了」の札。シーンの ReadyBoxContainer を実行時に組み直して使う。
        /// 点滴（EnemyPanel）の真下に置く。
        /// </summary>
        private ReadyBadge EnsureReadyBadge()
        {
            if (readyBadge == null)
            {
                RectTransform anchor = (enemyPanel != null)
                    ? enemyPanel.GetComponent<RectTransform>() : null;
                readyBadge = ReadyBadge.Attach(
                    readyBoxContainer, readyCheckImage, anchor, isSelf: false);
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

        /// <summary>相手の札も拡大したスマホの裏に入るので、自分側と同じタイミングで伏せる。</summary>
        public void SetReadyBoxSuppressed(bool suppressed)
        {
            var badge = EnsureReadyBadge();
            if (badge != null) badge.SetSuppressed(suppressed);
        }

        private TurnGlow turnGlow;
        private TurnCharacterGlow characterGlow;

        /// <summary>
        /// 相手の手番のとき点滴を赤く脈打たせる。
        /// 血袋も FloatingAnimator で揺れているので、EnemyPanel の中に影絵を敷いて
        /// 揺れごと追従させる。血袋の形は絵そのものなので矩形の実測は要らない。
        ///
        /// 女の子（立ち絵）も同時に光らせる。点滴は画面の隅にあって気づきにくいため。
        /// 以前は画面のふちに枠を出していたが、盤面が狭く見えるのでこちらへ替えた。
        /// </summary>
        public void SetTurnGlow(bool on)
        {
            if (turnGlow == null)
            {
                RectTransform panel = (enemyPanel != null)
                    ? enemyPanel.GetComponent<RectTransform>() : null;
                turnGlow = TurnGlow.Attach(panel, isSelf: false);
            }
            if (turnGlow != null) turnGlow.SetOn(on);

            if (characterGlow == null)
            {
                characterGlow = TurnCharacterGlow.Attach(characterRenderer);
            }
            if (characterGlow != null) characterGlow.SetOn(on);
        }

        public void SetCharacterSprite(Sprite sprite)
        {
            // SetBodyPoseかSetFaceExpressionを本来は推奨するが、旧互換として残す
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

        public void SetDiscardingState(bool isDiscarding)
        {
            if (characterRenderer != null)
            {
                // インスペクターで実行中に変更された場合も反映されるように、characterDataから直接読み取る
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

        public void PlayBounceAnimation(float duration = 0.5f)
        {
            if (characterRenderer == null) return;
            if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
            // 引数で渡されたduration（例: 5秒）に関わらず、インスペクターで設定した時間で一瞬跳ねる
            bounceCoroutine = StartCoroutine(BounceRoutine(bounceDuration));
        }

        private System.Collections.IEnumerator BounceRoutine(float durationToBounce)
        {
            float elapsed = 0f;
            // durationToBounce の時間内で Sin(0) から Sin(PI) まで推移するように速度を計算 (1回跳ねる)
            float bounceSpeed = Mathf.PI / durationToBounce;
            
            while (elapsed < durationToBounce)
            {
                elapsed += Time.deltaTime;
                float yOffset = Mathf.Sin(elapsed * bounceSpeed) * bounceHeight;
                characterRenderer.transform.localPosition = originalPosition + new Vector3(0, yOffset, 0);
                yield return null;
            }
            
            characterRenderer.transform.localPosition = originalPosition;
            bounceCoroutine = null;
        }

        // --- ズーム演出（指定したオブジェクトを巨大化し、少し手前・上に浮かせる） ---
        public System.Collections.IEnumerator ZoomInRoutine(float duration = 0.4f, float targetScaleMulti = 2.5f)
        {
            if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);

            Transform targetObj = zoomTarget != null ? zoomTarget : transform;

            // ズーム中は揺れを止める（右に戻ってしまうバグ対策）
            var floatAnims = GetComponentsInChildren<FloatingAnimator>(true);
            foreach (var anim in floatAnims)
            {
                anim.enabled = false;
            }

            // ズーム開始直前の位置とサイズを記憶する
            originalLocalPos = targetObj.localPosition;
            originalScale = targetObj.localScale;
            if (originalScale == Vector3.zero) originalScale = Vector3.one;

            // UI用（ピクセル単位）か、3D用かで移動量を変える必要がある
            RectTransform rt = targetObj.GetComponent<RectTransform>();
            float moveX = (rt != null) ? zoomOffsetUI.x : zoomOffsetWorld.x;
            float moveY = (rt != null) ? zoomOffsetUI.y : zoomOffsetWorld.y;
            float moveZ = (rt != null) ? 0f : zoomOffsetWorld.z;

            Vector3 targetPos = originalLocalPos + new Vector3(moveX, moveY, moveZ);
            Vector3 targetScale = originalScale * targetScaleMulti;

            float t = 0;
            while (t < duration)
            {
                float progress = t / duration;
                float eased = progress * progress * (3f - 2f * progress);

                targetObj.localPosition = Vector3.Lerp(originalLocalPos, targetPos, eased);
                targetObj.localScale = Vector3.Lerp(originalScale, targetScale, eased);

                t += Time.deltaTime;
                yield return null;
            }

            targetObj.localPosition = targetPos;
            targetObj.localScale = targetScale;
        }

        public System.Collections.IEnumerator ResetZoomRoutine(float duration = 0.3f)
        {
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

            // ズーム終了後に揺れを再開する
            var floatAnims = GetComponentsInChildren<FloatingAnimator>(true);
            foreach (var anim in floatAnims)
            {
                anim.enabled = true;
                anim.UpdateInitialPosition();
            }
        }
    }
}
