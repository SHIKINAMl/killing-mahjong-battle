using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace KillingMahjong.UI
{
    public class ConfirmationDialogUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Button okButton;
        [SerializeField] private Button noButton;

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
            canvas.sortingOrder = 9999; // 非常に高い値にして手牌や他のCanvasより手前に出す

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
                messageText.enableAutoSizing = false;
                messageText.fontSize = 25; // テキストが被らないように25に変更
                // テキストの行数で全体が上下に動くのを防ぐため、上揃え(Top)に変更
                messageText.alignment = TextAlignmentOptions.Top;
                messageText.overflowMode = TextOverflowModes.Overflow; // 文字が潰れるのを防ぐ

                RectTransform textRt = messageText.GetComponent<RectTransform>();
                if (textRt != null)
                {
                    // アンカーをStretchに設定
                    textRt.anchorMin = new Vector2(0, 0);
                    textRt.anchorMax = new Vector2(1, 1);
                    // Left, Bottomの設定 (Bottomはボタンと被らないよう適度に空ける)
                    textRt.offsetMin = new Vector2(20, 200);
                    // Right, Topの設定 (上揃えなので、上から適度に余白を設ける)
                    textRt.offsetMax = new Vector2(-20, -150);
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
            if (messageText != null) messageText.text = message;
            onConfirmAction = onConfirm;
            onCancelAction = onCancel;
            
            gameObject.SetActive(true);
            transform.SetAsLastSibling(); // 手牌UIなどより手前(最前面)に表示
        }

        public void ShowDialogWithWaits(string message, int[] waitTileIds, Action onConfirm, Action onCancel)
        {
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

            // 指定された通り、画面中央から上に135の位置で固定
            waitTilesContainer.anchoredPosition = new Vector2(0, 135f);

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
                    
                    float actualTileWidth = tileWidth * scale; // スケールに応じた実際の幅
                    float actualSpacing = spacing * scale;
                    float actualTotalWidth = (waitTileIds.Length * actualTileWidth) + ((waitTileIds.Length - 1) * actualSpacing);
                    float actualStartX = -actualTotalWidth / 2f + actualTileWidth / 2f;
                    
                    rt.anchoredPosition = new Vector2(actualStartX + i * (actualTileWidth + actualSpacing), 0);
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
            }
        }

        private void ClearWaits()
        {
            foreach (var t in activeWaitTiles)
            {
                if (t != null) Destroy(t);
            }
            activeWaitTiles.Clear();
        }

        public void HideDialog()
        {
            gameObject.SetActive(false);
            ClearWaits();
        }

        private void OnOkClicked()
        {
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
}
