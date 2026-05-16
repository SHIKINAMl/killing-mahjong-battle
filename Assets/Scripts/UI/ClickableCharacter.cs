using UnityEngine;
using UnityEngine.InputSystem;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 敵キャラクターの SpriteRenderer にアタッチして、
    /// クリックで敵キャラクターを切り替えるためのスクリプト。
    /// UIレイヤーに遮られないよう、Update で直接範囲判定を行う。
    /// </summary>
    public class ClickableCharacter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EnemyInfoUI enemyInfoUI;

        [Header("Click Area Settings")]
        [Tooltip("クリック判定の範囲サイズ（ワールド座標）。0の場合は SpriteRenderer の Bounds を使用します。")]
        [SerializeField] private Vector2 clickAreaSize = Vector2.zero;
        
        [Tooltip("クリック判定の中心オフセット（ローカル座標）")]
        [SerializeField] private Vector2 clickAreaOffset = Vector2.zero;

        [Header("Debug")]
        [Tooltip("Game View 上でクリック判定範囲を半透明で表示する")]
        [SerializeField] private bool showDebugArea = true;
        [SerializeField] private Color debugAreaColor = new Color(0f, 1f, 0f, 0.25f);

        private DialogueUI dialogueUI;
        private SpriteRenderer spriteRenderer;
        private GameObject debugOverlay;

        private void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            // DialogueUI の参照を自動取得する
            dialogueUI = FindFirstObjectByType<DialogueUI>();

            // EnemyInfoUI が未設定の場合、親階層から自動取得を試みる
            if (enemyInfoUI == null)
            {
                enemyInfoUI = GetComponentInParent<EnemyInfoUI>();
            }

            Debug.Log($"[ClickableCharacter] Start完了 - enemyInfoUI={(enemyInfoUI != null ? "OK" : "NULL")} / spriteRenderer={(spriteRenderer != null ? "OK" : "NULL")}");

            // デバッグ用の半透明オーバーレイを生成
            CreateDebugOverlay();
        }

        /// <summary>
        /// Game View 上でクリック判定範囲を表示するための半透明オーバーレイを生成する
        /// </summary>
        private void CreateDebugOverlay()
        {
            if (!showDebugArea) return;

            // 1x1 の白ピクセルスプライトをコードで生成
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            Sprite whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

            // 子オブジェクトとして半透明スプライトを配置
            debugOverlay = new GameObject("ClickArea_DebugOverlay");
            debugOverlay.transform.SetParent(transform, false);

            var sr = debugOverlay.AddComponent<SpriteRenderer>();
            sr.sprite = whiteSprite;
            sr.color = debugAreaColor;
            sr.sortingOrder = 999; // 最前面に表示

            UpdateDebugOverlay();
        }

        /// <summary>
        /// オーバーレイの位置とサイズをクリック判定範囲に合わせる
        /// </summary>
        private void UpdateDebugOverlay()
        {
            if (debugOverlay == null) return;

            Vector2 size = clickAreaSize;
            Vector2 offset = clickAreaOffset;

            if (size.x <= 0 || size.y <= 0)
            {
                if (spriteRenderer != null)
                {
                    // SpriteRenderer の Bounds をローカルに変換
                    size = spriteRenderer.bounds.size;
                    offset = (Vector2)(spriteRenderer.bounds.center - transform.position);
                }
                else
                {
                    size = new Vector2(2f, 2f); // フォールバック
                }
            }

            debugOverlay.transform.localPosition = new Vector3(offset.x, offset.y, -0.01f);
            debugOverlay.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        private void Update()
        {
            // 新Input Systemでマウスクリック（左ボタン）を検知
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (Camera.main == null) return;

            // マウスのスクリーン座標をワールド座標に変換
            // パースペクティブカメラの場合、スプライトのZ深度に合わせた距離を使う必要がある
            Vector2 screenPos = mouse.position.ReadValue();
            float zDistance = Camera.main.WorldToScreenPoint(transform.position).z;
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDistance));

            // デバッグ: クリック位置と判定範囲を毎回表示
            Vector2 center = (Vector2)transform.position + clickAreaOffset;
            Debug.Log($"[ClickableCharacter] クリック位置: screen={screenPos}, world=({worldPos.x:F2}, {worldPos.y:F2}) / 判定中心=({center.x:F2}, {center.y:F2}) / 判定サイズ={clickAreaSize} / transform.pos={transform.position}");

            // クリック判定
            if (IsInsideClickArea(worldPos))
            {
                OnClicked();
            }
        }

        /// <summary>
        /// ワールド座標がクリック判定範囲内かどうかを判定する
        /// </summary>
        private bool IsInsideClickArea(Vector3 worldPos)
        {
            Vector2 center = (Vector2)transform.position + clickAreaOffset;

            // clickAreaSize が設定されている場合はそちらを使う
            if (clickAreaSize.x > 0 && clickAreaSize.y > 0)
            {
                float halfW = clickAreaSize.x * 0.5f;
                float halfH = clickAreaSize.y * 0.5f;
                return worldPos.x >= center.x - halfW && worldPos.x <= center.x + halfW &&
                       worldPos.y >= center.y - halfH && worldPos.y <= center.y + halfH;
            }

            // 未設定の場合は SpriteRenderer の Bounds を使用
            if (spriteRenderer != null)
            {
                Bounds b = spriteRenderer.bounds;
                return worldPos.x >= b.min.x && worldPos.x <= b.max.x &&
                       worldPos.y >= b.min.y && worldPos.y <= b.max.y;
            }

            return false;
        }

        private void OnClicked()
        {
            Debug.Log("[ClickableCharacter] クリック検知！");

            if (enemyInfoUI == null)
            {
                Debug.LogWarning("[ClickableCharacter] enemyInfoUI が null です！インスペクターで設定してください。");
                return;
            }

            // キャラクターを切り替える
            enemyInfoUI.CycleEnemy();
            Debug.Log($"[ClickableCharacter] CycleEnemy 実行完了。現在のキャラ: {enemyInfoUI.CurrentCharacterData?.characterName}");

            // クリック時のリアクションセリフを表示する
            string clickDialogue = enemyInfoUI.GetClickDialogue();
            if (dialogueUI != null && clickDialogue != null)
            {
                dialogueUI.ShowText(clickDialogue);
            }
        }

        /// <summary>
        /// エディタ上でクリック判定範囲を視覚的に確認するためのギズモ
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Vector2 center = (Vector2)transform.position + clickAreaOffset;
            Vector2 size = clickAreaSize;

            if (size.x <= 0 || size.y <= 0)
            {
                var sr = GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    center = sr.bounds.center;
                    size = sr.bounds.size;
                }
            }

            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawCube(new Vector3(center.x, center.y, transform.position.z), new Vector3(size.x, size.y, 0.1f));
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(new Vector3(center.x, center.y, transform.position.z), new Vector3(size.x, size.y, 0.1f));
        }
    }
}
