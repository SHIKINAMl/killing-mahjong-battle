using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    /// <summary>
    /// ゲーム画面の状況を監視し、実況コメントを右から左に流すUIクラス。
    /// ニコニコ動画風の「弾幕コメント」スタイルで実況テキストが流れます。
    /// 
    /// 使い方: 
    ///   任意のGameObjectにこのコンポーネントをアタッチするだけでOK。
    ///   UIは全てコード内で動的に生成されます。
    ///   NetworkMessageHandler のイベントに直接購読するため、PhaseManager は不要です。
    /// </summary>
    public class CommentaryFlowUI : MonoBehaviour
    {
        [Header("コメント流し設定")]
        [SerializeField] private float flowSpeed = 300f;
        [SerializeField] private int maxLanes = 5;
        [SerializeField] private float laneHeight = 50f;
        [SerializeField] private float minCommentInterval = 0.5f;

        [Header("フォント設定")]
        [SerializeField] private TMP_FontAsset customFont;
        [SerializeField] private float fontSize = 36f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color excitedColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color cheerColor = new Color(0.4f, 1f, 0.6f, 1f);

        [Header("表示位置")]
        [SerializeField] private float topMarginRatio = 0.05f;
        [SerializeField] private float bottomMarginRatio = 0.40f;

        [Header("デバッグ")]
        [SerializeField] private bool enableDebugLog = true;

        // --- 内部参照 ---
        private Canvas rootCanvas;
        private RectTransform canvasRt;
        private RectTransform flowContainer;
        private List<RectTransform> activeComments = new List<RectTransform>();
        private float[] laneLastUsedTime;
        private float lastSpawnTime = -10f;
        private int commentCounter = 0;

        // フェーズ追跡
        private RoundStatus lastRoundStatus = RoundStatus.None;
        private int lastWallCount = -1;
        private bool subscribed = false;

        // 実況バリエーション
        private static readonly string[] HandSelectionComments = {
            "手牌を選んでいくぞ！",
            "どの牌を手に入れるか…",
            "ここが勝負の分かれ目だ",
            "いい牌を引けるか!?",
            "運命の手牌選択…！",
        };

        private static readonly string[] DiscardPhaseComments = {
            "打牌フェーズだ！攻めろ！",
            "切り合いが始まる！",
            "さあ、読み合いの時間だ",
            "ここからが本番だ！",
        };

        private static readonly string[] PlayerDiscardComments = {
            "プレイヤーが{0}を切った！",
            "{0}切り！大丈夫か!?",
            "{0}を打牌！思い切ったな",
        };

        private static readonly string[] EnemyDiscardComments = {
            "相手が{0}を切ってきた！",
            "{0}が出た！チャンスか!?",
            "おっと、{0}切りだと…！",
            "相手の{0}…これは怪しい",
        };

        private static readonly string[] RonComments = {
            "ロンきたあああ！！",
            "ロン！！大物手だ！！",
            "うおおお！ロン！！",
            "出たー！ロン宣言！",
            "ロンだ！勝負あり！！",
        };

        private static readonly string[] DrawComments = {
            "流局だ…惜しかった",
            "流局！次こそは…",
            "引き分けか…！",
            "どちらも届かず…流局",
        };

        private static readonly string[] BettingComments = {
            "賭け金を決めろ！",
            "ベッティングタイム！",
            "いくら賭ける？勝負だ！",
            "ここで攻めるか守るか…",
        };

        private static readonly string[] WallDecreasingComments = {
            "残りの牌が減ってきた…",
            "山が薄くなってきたぞ",
            "もう後がない！",
            "終盤戦突入だ…！",
        };

        private static readonly string[] CheerComments = {
            "がんばれー！",
            "いけるいける！",
            "ナイス！",
            "うまい！",
            "それだ！",
        };

        private void Awake()
        {
            laneLastUsedTime = new float[maxLanes];
            for (int i = 0; i < maxLanes; i++)
                laneLastUsedTime[i] = -999f;
        }

        private void Start()
        {
            SetupUI();
            StartCoroutine(WaitAndSubscribe());
        }

        private void OnDestroy()
        {
            UnsubscribeAll();
            // 動的に作った専用Canvasも破棄
            if (rootCanvas != null)
                Destroy(rootCanvas.gameObject);
        }

        /// <summary>
        /// NetworkMessageHandler のシングルトンが起動するまで待ってから購読する
        /// </summary>
        private IEnumerator WaitAndSubscribe()
        {
            // NetworkMessageHandler が立ち上がるまで待機
            float timeout = 10f;
            float waited = 0f;
            while (NetworkMessageHandler.Instance == null && waited < timeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            if (NetworkMessageHandler.Instance == null)
            {
                Debug.LogError("[CommentaryFlowUI] NetworkMessageHandler が見つかりません！");
                yield break;
            }

            SubscribeAll();
            Log("初期化完了 - NetworkMessageHandler に購読しました");

            // テスト用: 起動直後に1つ流して動作確認
            SpawnComment("実況システム起動！", excitedColor);
        }

        private void SubscribeAll()
        {
            if (subscribed) return;
            var net = NetworkMessageHandler.Instance;
            if (net == null) return;

            net.OnPhaseStatusChanged += OnPhaseChanged;
            net.OnTileDiscarded += OnTileDiscarded;
            net.OnAgari += OnAgari;
            net.OnDraw += OnDraw;
            net.OnGameStarted += OnGameStarted;
            net.OnBettingComplete += OnBettingComplete;
            net.OnHandSelectionAccepted += OnHandSelectionAccepted;

            // BoardStateManager のイベントも購読
            if (BoardStateManager.Instance != null)
            {
                BoardStateManager.Instance.OnBoardStateRebuilt += OnBoardRebuilt;
                BoardStateManager.Instance.OnTileMovedToHand += OnTileMovedToHand;
            }

            subscribed = true;
        }

        private void UnsubscribeAll()
        {
            if (!subscribed) return;

            if (NetworkMessageHandler.Instance != null)
            {
                var net = NetworkMessageHandler.Instance;
                net.OnPhaseStatusChanged -= OnPhaseChanged;
                net.OnTileDiscarded -= OnTileDiscarded;
                net.OnAgari -= OnAgari;
                net.OnDraw -= OnDraw;
                net.OnGameStarted -= OnGameStarted;
                net.OnBettingComplete -= OnBettingComplete;
                net.OnHandSelectionAccepted -= OnHandSelectionAccepted;
            }

            if (BoardStateManager.Instance != null)
            {
                BoardStateManager.Instance.OnBoardStateRebuilt -= OnBoardRebuilt;
                BoardStateManager.Instance.OnTileMovedToHand -= OnTileMovedToHand;
            }

            subscribed = false;
        }

        // =========================================================
        //  UI 構築
        // =========================================================

        private void SetupUI()
        {
            // 専用の Screen Space - Overlay Canvas を動的に作成する
            // 他のCanvasに依存すると、RenderMode/Camera/Scale等の問題で
            // コメントが表示されない場合があるため、自前で確実なCanvasを用意する
            GameObject canvasObj = new GameObject("CommentaryFlowCanvas");
            canvasObj.layer = LayerMask.NameToLayer("UI");

            rootCanvas = canvasObj.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 999; // 最前面に表示

            // CanvasScaler で解像度に合わせてスケール
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasRt = canvasObj.GetComponent<RectTransform>();

            // コメントが流れるコンテナを作成
            GameObject containerObj = new GameObject("CommentaryFlowContainer");
            containerObj.transform.SetParent(canvasObj.transform, false);

            flowContainer = containerObj.AddComponent<RectTransform>();
            flowContainer.anchorMin = Vector2.zero;
            flowContainer.anchorMax = Vector2.one;
            flowContainer.sizeDelta = Vector2.zero;
            flowContainer.anchoredPosition = Vector2.zero;

            // レイキャストをブロックしない（ゲーム操作を邪魔しない）
            CanvasGroup cg = containerObj.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // Canvas全体もレイキャストをブロックしない
            CanvasGroup canvasCg = canvasObj.AddComponent<CanvasGroup>();
            canvasCg.blocksRaycasts = false;
            canvasCg.interactable = false;

            Log("専用 Overlay Canvas + コンテナを作成しました");
        }

        // =========================================================
        //  イベントハンドラ
        // =========================================================

        private void OnGameStarted()
        {
            SpawnComment("対戦相手が見つかった！", excitedColor);
        }

        private void OnPhaseChanged(RoundStatus newPhase)
        {
            if (newPhase == lastRoundStatus) return;
            Log($"フェーズ変更: {lastRoundStatus} -> {newPhase}");
            lastRoundStatus = newPhase;

            switch (newPhase)
            {
                case RoundStatus.Dealing:
                    SpawnComment("配牌中…ドキドキ", normalColor);
                    break;
                case RoundStatus.HandSelection:
                    SpawnComment(RandomPick(HandSelectionComments), excitedColor);
                    break;
                case RoundStatus.Betting:
                    SpawnComment(RandomPick(BettingComments), excitedColor);
                    break;
                case RoundStatus.TurnDecision:
                    SpawnComment("先攻後攻の決定だ！", normalColor);
                    break;
                case RoundStatus.Discard:
                    SpawnComment(RandomPick(DiscardPhaseComments), excitedColor);
                    StartCoroutine(PeriodicCheerDuringDiscard());
                    break;
            }
        }

        private void OnTileDiscarded(int tileId, bool isLocalPlayer)
        {
            TileData tile = new TileData(tileId);
            string tileName = tile.GetTileName();

            if (isLocalPlayer)
            {
                SpawnComment(string.Format(RandomPick(PlayerDiscardComments), tileName), normalColor);
            }
            else
            {
                SpawnComment(string.Format(RandomPick(EnemyDiscardComments), tileName), excitedColor);
            }
        }

        private void OnAgari(bool isLocalWin)
        {
            SpawnComment(RandomPick(RonComments), dangerColor, 1.3f);
            StartCoroutine(BurstComments(RonComments, dangerColor, 3, 0.5f));
        }

        private void OnDraw(KillingMahjong.EngineData.DrawPlayerData[] drawData)
        {
            SpawnComment(RandomPick(DrawComments), normalColor, 1.2f);
        }

        private void OnBettingComplete(int playerBet, int enemyBet, int playerHp, int enemyHp)
        {
            SpawnComment($"賭け金決定！ {playerBet} vs {enemyBet}", excitedColor);
        }

        private void OnHandSelectionAccepted()
        {
            SpawnComment("手牌決定！いざ勝負！", cheerColor);
        }

        private void OnBoardRebuilt()
        {
            if (BoardStateManager.Instance == null) return;
            int wallCount = BoardStateManager.Instance.CurrentWallTiles.Count;

            if (wallCount > 0 && wallCount <= 5 && lastWallCount > 5)
            {
                SpawnComment(RandomPick(WallDecreasingComments), dangerColor);
            }
            else if (wallCount > 0 && wallCount <= 10 && lastWallCount > 10)
            {
                SpawnComment("山が残り少ない…！", normalColor);
            }

            lastWallCount = wallCount;
        }

        private void OnTileMovedToHand(int tileId)
        {
            if (lastRoundStatus == RoundStatus.HandSelection && Random.value < 0.25f)
            {
                TileData tile = new TileData(tileId);
                string[] comments = {
                    $"{tile.GetTileName()}を選択！",
                    $"{tile.GetTileName()}か…いいね！",
                    $"おっ、{tile.GetTileName()}！",
                };
                SpawnComment(RandomPick(comments), cheerColor, 0.9f);
            }
        }

        // =========================================================
        //  コメント生成・アニメーション
        // =========================================================

        /// <summary>
        /// コメントを生成して右から左に流す
        /// </summary>
        public void SpawnComment(string text, Color color, float sizeMultiplier = 1.0f)
        {
            if (flowContainer == null || canvasRt == null) return;
            if (string.IsNullOrEmpty(text)) return;

            // 連投防止
            if (Time.time - lastSpawnTime < minCommentInterval) return;
            lastSpawnTime = Time.time;

            Log($"コメント生成: {text}");

            // レーン選択
            int lane = FindAvailableLane();
            laneLastUsedTime[lane] = Time.time;

            // テキストオブジェクト生成
            GameObject commentObj = new GameObject($"Comment_{commentCounter++}");
            commentObj.transform.SetParent(flowContainer, false);

            RectTransform rt = commentObj.AddComponent<RectTransform>();

            TextMeshProUGUI tmp = commentObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.color = color;
            tmp.fontSize = fontSize * sizeMultiplier;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;

            if (customFont != null)
                tmp.font = customFont;

            // アウトライン（影）で視認性UP
            // MaterialPropertyBlock 方式だと TMP では効かないため、直接 fontSharedMaterial を複製
            tmp.fontMaterial = new Material(tmp.fontMaterial);
            tmp.fontMaterial.EnableKeyword("UNDERLAY_ON");
            tmp.fontMaterial.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.85f));
            tmp.fontMaterial.SetFloat("_UnderlayOffsetX", 1.5f);
            tmp.fontMaterial.SetFloat("_UnderlayOffsetY", -1.5f);
            tmp.fontMaterial.SetFloat("_UnderlaySoftness", 0.3f);

            // テキストサイズを計算
            tmp.ForceMeshUpdate();
            Vector2 textSize = tmp.GetPreferredValues(text);

            // RectTransform 設定: 右端の外から開始
            rt.sizeDelta = new Vector2(textSize.x + 20f, laneHeight);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchorMin = new Vector2(1, 1); // 右上基準
            rt.anchorMax = new Vector2(1, 1);

            // Y位置: レーンに応じて配置
            float canvasHeight = canvasRt.rect.height;
            float topMargin = canvasHeight * topMarginRatio;
            float bottomMargin = canvasHeight * bottomMarginRatio;
            float availableHeight = canvasHeight - topMargin - bottomMargin;
            float laneSpacing = availableHeight / Mathf.Max(1, maxLanes);
            float yPos = -topMargin - (lane * laneSpacing) - (laneSpacing * 0.5f);

            rt.anchoredPosition = new Vector2(0, yPos);

            activeComments.Add(rt);
            StartCoroutine(FlowAnimation(rt, commentObj, textSize.x));
        }

        /// <summary>
        /// テキストを右→左にスクロールさせるコルーチン
        /// </summary>
        private IEnumerator FlowAnimation(RectTransform rt, GameObject obj, float textWidth)
        {
            if (rt == null || obj == null) yield break;

            float canvasWidth = canvasRt.rect.width;
            float totalDistance = canvasWidth + textWidth + 100f;
            float startX = 0f;
            float endX = -totalDistance;

            float duration = totalDistance / flowSpeed;
            float elapsed = 0f;

            while (elapsed < duration && obj != null && rt != null)
            {
                float progress = elapsed / duration;
                float x = Mathf.Lerp(startX, endX, progress);
                rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);

                // フェードイン/アウト
                TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    float alpha = 1f;
                    if (progress < 0.05f) alpha = progress / 0.05f;
                    else if (progress > 0.90f) alpha = (1f - progress) / 0.1f;
                    Color c = tmp.color;
                    c.a = Mathf.Clamp01(alpha);
                    tmp.color = c;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (rt != null) activeComments.Remove(rt);
            if (obj != null) Destroy(obj);
        }

        private int FindAvailableLane()
        {
            float oldestTime = float.MaxValue;
            int bestLane = 0;

            for (int i = 0; i < maxLanes; i++)
            {
                if (Time.time - laneLastUsedTime[i] > 2.0f)
                    return i;
                if (laneLastUsedTime[i] < oldestTime)
                {
                    oldestTime = laneLastUsedTime[i];
                    bestLane = i;
                }
            }
            return bestLane;
        }

        // =========================================================
        //  演出ヘルパー
        // =========================================================

        private IEnumerator BurstComments(string[] pool, Color color, int count, float interval)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForSeconds(interval);
                SpawnComment(RandomPick(pool), color, Random.Range(0.9f, 1.4f));
            }
        }

        private IEnumerator PeriodicCheerDuringDiscard()
        {
            while (lastRoundStatus == RoundStatus.Discard)
            {
                yield return new WaitForSeconds(Random.Range(10f, 18f));
                if (lastRoundStatus != RoundStatus.Discard) break;

                if (Random.value < 0.5f)
                {
                    SpawnComment(RandomPick(CheerComments), cheerColor, 0.9f);
                }
                else if (BoardStateManager.Instance != null)
                {
                    int wallCount = BoardStateManager.Instance.CurrentWallTiles.Count;
                    if (wallCount <= 15 && wallCount > 0)
                    {
                        SpawnComment($"山の残り{wallCount}枚…！", dangerColor);
                    }
                }
            }
        }

        // =========================================================
        //  ユーティリティ
        // =========================================================

        private string RandomPick(string[] pool)
        {
            if (pool == null || pool.Length == 0) return "";
            return pool[Random.Range(0, pool.Length)];
        }

        private void Log(string msg)
        {
            if (enableDebugLog)
                Debug.Log($"[CommentaryFlowUI] {msg}");
        }
    }
}
