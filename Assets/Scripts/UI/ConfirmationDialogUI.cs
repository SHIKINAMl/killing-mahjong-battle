using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class ConfirmationDialogUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button okButton;
        [SerializeField] private Button noButton;

        [Header("Effect Sounds")]
        [SerializeField] private AudioClip explodeSound;
        [SerializeField] private AudioClip coinSound;

        [Header("Wait Tiles Settings")]
        [SerializeField] private RectTransform waitTilesContainer;
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private TileResourceManager tileResourceManager;
        private System.Collections.Generic.List<GameObject> activeWaitTiles = new System.Collections.Generic.List<GameObject>();

        private Action onConfirmAction;
        private Action onCancelAction;

        private void Awake()
        {
            if (okButton != null) okButton.onClick.AddListener(OnOkClicked);
            if (noButton != null) noButton.onClick.AddListener(OnNoClicked);

            // --- UIの被り・レイアウト崩れ対策 ---
            // 確実な最前面表示のため、Canvasコンポーネントを追加してSortingOrderを高く設定する
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = UISortingOrders.ConfirmationDialog;

            // ボタンのクリック判定が効くようにGraphicRaycasterを追加
            GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            // 自身にImageが無ければ追加して、画面全体を覆う半透明背景にする
            Image bg = GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
            }
            // レトロポップテーマに合わせたネイビー（濃い青）に変更
            bg.color = new Color32(42, 52, 87, 240);

            // 画面全体を覆うようにRectTransformを設定
            RectTransform rectTransform = GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
            }

            // テキストが巨大化するのを防ぎ、適切なサイズ・配置にする
            if (messageText != null)
            {
                messageText.enableAutoSizing = true;
                messageText.fontSizeMin = 10;
                messageText.fontSizeMax = KillingMahjong.Common.UITypography.BodySmall; // テキストが被らないように小さめのサイズに統一
                // テキストの行数で全体が上下に動くのを防ぐため、上揃え(Top)に変更
                messageText.alignment = TextAlignmentOptions.Top;
                messageText.overflowMode = TextOverflowModes.Overflow; // 文字が潰れるのを防ぐ

                RectTransform textRt = messageText.GetComponent<RectTransform>();
                if (textRt != null)
                {
                    // アンカーをStretchに設定
                    textRt.anchorMin = new Vector2(0, 0);
                    textRt.anchorMax = new Vector2(1, 1);
                    // Left, Bottomの設定(Bottomはボタンと被らないように適度に空ける)
                    textRt.offsetMin = new Vector2(20, 200);
                    // Right, Topの設定(上揃えなので、上から適度に余白を設ける)
                    textRt.offsetMax = new Vector2(-20, -180);
                }
            }

            // ボタンの位置もテキストと被らないように調整
            if (okButton != null)
            {
                RectTransform okRt = okButton.GetComponent<RectTransform>();
                if (okRt != null)
                {
                    okRt.anchorMin = new Vector2(0.55f, 0.2f);
                    okRt.anchorMax = new Vector2(0.85f, 0.35f);
                    okRt.offsetMin = Vector2.zero;
                    okRt.offsetMax = Vector2.zero;
                }
            }

            if (noButton != null)
            {
                RectTransform noRt = noButton.GetComponent<RectTransform>();
                if (noRt != null)
                {
                    noRt.anchorMin = new Vector2(0.15f, 0.2f);
                    noRt.anchorMax = new Vector2(0.45f, 0.35f);
                    noRt.offsetMin = Vector2.zero;
                    noRt.offsetMax = Vector2.zero;
                }
            }
        }

        public void ShowDialog(string message, Action onConfirm, Action onCancel)
        {
            if (okButton != null) okButton.interactable = true;
            if (noButton != null) noButton.interactable = true;
            if (messageText != null) messageText.text = message;
            onConfirmAction = onConfirm;
            onCancelAction = onCancel;
            
            gameObject.SetActive(true);
            transform.SetAsLastSibling(); // 手牌UIなどより手前(最前面)に表示

            if (KillingMahjong.Managers.AudioManager.Instance != null)
            {
                KillingMahjong.Managers.AudioManager.Instance.PlayUIPopupSE();
            }
        }

        /// <summary>待ち牌1枚ぶんの情報。カーソルを合わせたときのオーバーレイに使う。</summary>
        public struct WaitInfo
        {
            /// <summary>待ち牌の牌ID</summary>
            public int TileId;
            /// <summary>その牌で和了ったときの役名（「清一色 / 平和」のように連結済み）</summary>
            public string YakuText;
            /// <summary>「跳満」など。満たないときは「満貫未満」</summary>
            public string RankText;
        }

        private WaitInfo[] _waitInfos;

        public void ShowDialogWithWaits(string message, int[] waitTileIds, Action onConfirm, Action onCancel)
        {
            ShowDialogWithWaits(message, null, waitTileIds, onConfirm, onCancel);
        }

        /// <summary>
        /// 待ち牌を並べて確認を取る。
        /// 役名は並べず、**牌にカーソルを合わせたときだけ**手牌と役をオーバーレイで出す（要望18）。
        /// </summary>
        public void ShowDialogWithWaits(string message, WaitInfo[] waits, int[] waitTileIds,
                                        Action onConfirm, Action onCancel)
        {
            _waitInfos = waits;
            ShowDialog(message, onConfirm, onCancel);
            DisplayWaits(waitTileIds);
        }

        private void DisplayWaits(int[] waitTileIds)
        {
            ClearWaits();
            if (waitTileIds == null || waitTileIds.Length == 0) return;

            // WaitTilesContainerの作成と設定
            if (waitTilesContainer == null)
            {
                GameObject containerObj = new GameObject("WaitTilesContainer");
                waitTilesContainer = containerObj.AddComponent<RectTransform>();
                waitTilesContainer.SetParent(transform, false);
                // 画面中央基準のアンカーに戻す
                waitTilesContainer.anchorMin = new Vector2(0.5f, 0.5f);
                waitTilesContainer.anchorMax = new Vector2(0.5f, 0.5f);
                waitTilesContainer.pivot = new Vector2(0.5f, 0.5f);
            }

            // 指定された位置に配置
            waitTilesContainer.anchoredPosition = new Vector2(0, 100f);

            // PrefabとResourceManagerの取得 (インスペクターで設定されていない場合、シーンから取得)
            if (tilePrefab == null || tileResourceManager == null)
            {
                var waitUI = UnityEngine.Object.FindFirstObjectByType<WaitUI>(FindObjectsInactive.Include);
                if (waitUI != null)
                {
                    var pField = waitUI.GetType().GetField("tilePrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (pField != null) tilePrefab = pField.GetValue(waitUI) as GameObject;

                    var rField = waitUI.GetType().GetField("tileResourceManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (rField != null) tileResourceManager = rField.GetValue(waitUI) as TileResourceManager;
                }
            }

            // 配置パラメータ
            float tileWidth = 35f; // スケール後のおおよその幅(必要に応じて微調整)
            float spacing = 2f;
            float totalWidth = (waitTileIds.Length * tileWidth) + ((waitTileIds.Length - 1) * spacing);
            float startX = -totalWidth / 2f + tileWidth / 2f;

            for (int i = 0; i < waitTileIds.Length; i++)
            {
                int id = waitTileIds[i];
                if (tilePrefab == null) break;
                GameObject obj = Instantiate(tilePrefab, waitTilesContainer);
                activeWaitTiles.Add(obj);
                
                float scale = waitTileIds.Length > 6 ? 0.6f : 1.0f;
                // 手動レイアウトなのでスケールをそのまま適用し、アンカーを中央にする
                RectTransform rt = obj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    
                    rt.anchoredPosition = new Vector2(startX + i * (tileWidth + spacing), 0);
                    rt.localScale = new Vector3(scale, scale, 1f);
                }

                TileVisual visual = obj.GetComponent<TileVisual>();
                if (visual != null && tileResourceManager != null)
                {
                    visual.SetTile(id, tileResourceManager.GetTileSprite(id));
                    if (KillingMahjong.Managers.BoardStateManager.Instance != null && 
                        KillingMahjong.Managers.BoardStateManager.Instance.NonManganWaitTiles.Contains(id))
                    {
                        visual.SetAlpha(0.3f);
                    }
                    else 
                    {
                        visual.SetAlpha(1.0f);
                    }
                }

                var interaction = obj.GetComponent<TileInteraction>();
                if (interaction != null) Destroy(interaction);

                // カーソルを合わせたら、その牌を入れた手牌と役をオーバーレイで出す
                int hovered = id;
                var relay = obj.AddComponent<WaitTileHoverRelay>();
                relay.OnEnter = () => ShowHandPreview(hovered);
                relay.OnExit = HideHandPreview;
            }
        }

        // ==================== カーソルを合わせたときのオーバーレイ ====================

        private GameObject previewObj;
        private RectTransform previewTiles;
        private TextMeshProUGUI previewYakuText;
        private readonly System.Collections.Generic.List<GameObject> previewTileObjs =
            new System.Collections.Generic.List<GameObject>();

        private void ShowHandPreview(int waitTileId)
        {
            if (tilePrefab == null || tileResourceManager == null) return;

            EnsurePreviewBuilt();

            // 選んだ13枚 ＋ その待ち牌。並びは手牌と同じ昇順にして見比べやすくする
            var hand = new System.Collections.Generic.List<int>();
            var board = KillingMahjong.Managers.BoardStateManager.Instance;
            if (board != null && board.CurrentHandTiles != null) hand.AddRange(board.CurrentHandTiles);
            hand.Sort((a, b) =>
            {
                int ba = a & 0x1F, bb = b & 0x1F;
                return ba != bb ? ba.CompareTo(bb) : a.CompareTo(b);
            });

            ClearPreviewTiles();

            // 手牌13枚 → はっきり間を空けて → 和了牌
            const float w = 26f, gap = 1f, extra = 18f;
            float total = hand.Count * (w + gap) + extra + w;
            float x = -total / 2f + w / 2f;

            for (int i = 0; i < hand.Count; i++)
            {
                SpawnPreviewTile(hand[i], x);
                x += w + gap;
            }
            x += extra;
            SpawnPreviewTile(waitTileId, x);

            string yaku = "役なし";
            string rank = "";
            if (_waitInfos != null)
            {
                foreach (var info in _waitInfos)
                {
                    if (info.TileId != waitTileId) continue;
                    if (!string.IsNullOrEmpty(info.YakuText)) yaku = info.YakuText;
                    rank = info.RankText;
                    break;
                }
            }
            if (previewYakuText != null)
                previewYakuText.text = string.IsNullOrEmpty(rank) ? yaku : $"{yaku}　{rank}";

            previewObj.SetActive(true);
            previewObj.transform.SetAsLastSibling();
        }

        private void SpawnPreviewTile(int id, float x)
        {
            var obj = Instantiate(tilePrefab, previewTiles);
            previewTileObjs.Add(obj);

            var rt = obj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(x, 0f);
                rt.localScale = new Vector3(0.75f, 0.75f, 1f);
            }

            var visual = obj.GetComponent<TileVisual>();
            if (visual != null) visual.SetTile(id, tileResourceManager.GetTileSprite(id));

            var inter = obj.GetComponent<TileInteraction>();
            if (inter != null) Destroy(inter);
        }

        private void HideHandPreview()
        {
            if (previewObj != null) previewObj.SetActive(false);
        }

        private void ClearPreviewTiles()
        {
            foreach (var t in previewTileObjs) if (t != null) Destroy(t);
            previewTileObjs.Clear();
        }

        private void EnsurePreviewBuilt()
        {
            if (previewObj != null) return;

            previewObj = new GameObject("HandPreview");
            var rt = previewObj.AddComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(500f, 92f);
            rt.anchoredPosition = new Vector2(0f, 0f);

            // 下の説明文が透けると読めなくなるので、完全に塗りつぶす
            var bg = previewObj.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.04f, 0.06f, 1f);
            bg.raycastTarget = false; // 牌のホバー判定を邪魔しない

            var ol = previewObj.AddComponent<Outline>();
            ol.effectColor = new Color(1f, 1f, 1f, 0.55f);
            ol.effectDistance = new Vector2(2f, -2f);

            var tilesObj = new GameObject("Tiles", typeof(RectTransform));
            previewTiles = tilesObj.GetComponent<RectTransform>();
            previewTiles.SetParent(rt, false);
            previewTiles.anchorMin = previewTiles.anchorMax = new Vector2(0.5f, 0.5f);
            previewTiles.pivot = new Vector2(0.5f, 0.5f);
            previewTiles.anchoredPosition = new Vector2(0f, 16f);

            var textObj = new GameObject("Yaku", typeof(RectTransform));
            var trt = textObj.GetComponent<RectTransform>();
            trt.SetParent(rt, false);
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 0f);
            trt.pivot = new Vector2(0.5f, 0f);
            trt.offsetMin = new Vector2(6f, 4f);
            trt.offsetMax = new Vector2(-6f, 26f);

            previewYakuText = textObj.AddComponent<TextMeshProUGUI>();
            previewYakuText.alignment = TextAlignmentOptions.Center;
            previewYakuText.color = Color.white;
            previewYakuText.raycastTarget = false;
            previewYakuText.enableAutoSizing = true;
            previewYakuText.fontSizeMin = 10f;
            previewYakuText.fontSizeMax = 16f;

            previewObj.SetActive(false);
        }

        private void ClearWaits()
        {
            foreach (var t in activeWaitTiles)
            {
                if (t != null) Destroy(t);
            }
            activeWaitTiles.Clear();
            ClearPreviewTiles();
            HideHandPreview();
        }

        public void HideDialog()
        {
            gameObject.SetActive(false);
            ClearWaits();
        }

        private void OnOkClicked()
        {
            if (okButton != null) okButton.interactable = false;
            if (noButton != null) noButton.interactable = false;
            
            StartCoroutine(OkClickRoutine());
        }

        private IEnumerator OkClickRoutine()
        {
            // --- SE再生（ドーパミン音） ---
            if (KillingMahjong.Managers.AudioManager.Instance != null)
            {
                // 用意されたSEがあれば鳴らす。今回は仮の音源として実装
                if (explodeSound != null) KillingMahjong.Managers.AudioManager.Instance.PlaySE(explodeSound);
                if (coinSound != null) KillingMahjong.Managers.AudioManager.Instance.PlaySE(coinSound);
            }

            // --- 画面フラッシュ演出 ---
            GameObject flashObj = new GameObject("FlashPanel");
            flashObj.transform.SetParent(transform.parent, false); // ダイアログの親（全体Canvas）に配置
            flashObj.transform.SetAsLastSibling(); // 一番手前にする
            
            Image flashImg = flashObj.AddComponent<Image>();
            flashImg.color = new Color(1f, 1f, 1f, 1f); // 完全な白
            
            RectTransform flashRt = flashObj.GetComponent<RectTransform>();
            flashRt.anchorMin = Vector2.zero;
            flashRt.anchorMax = Vector2.one;
            flashRt.offsetMin = Vector2.zero;
            flashRt.offsetMax = Vector2.zero;

            // フェードアウト
            float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                flashImg.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }

            Destroy(flashObj);

            // --- 終了処理 ---
            gameObject.SetActive(false);
            ClearWaits();
            onConfirmAction?.Invoke();
        }

        private void OnNoClicked()
        {
            gameObject.SetActive(false);
            ClearWaits();
            onCancelAction?.Invoke();
        }
    }

    /// <summary>待ち牌にカーソルが乗ったかどうかを外へ流すだけの小物。</summary>
    public class WaitTileHoverRelay : MonoBehaviour,
        UnityEngine.EventSystems.IPointerEnterHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        public Action OnEnter;
        public Action OnExit;

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) { OnEnter?.Invoke(); }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { OnExit?.Invoke(); }
    }
}
