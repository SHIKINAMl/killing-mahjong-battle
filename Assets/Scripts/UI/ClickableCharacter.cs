using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    [System.Serializable]
    public class CharacterClickArea
    {
        [Tooltip("部位の名前（例：Head, Body, Leg など）\n空欄にすると全体のデフォルトになります。")]
        public string areaName = "Body";
        
        [Tooltip("クリック判定のサイズ（ワールド座標）")]
        public Vector2 size = new Vector2(1, 1);
        
        [Tooltip("中心からのオフセット（ローカル座標）")]
        public Vector2 offset = Vector2.zero;
    }

    /// <summary>
    /// 敵キャラクターの SpriteRenderer にアタッチして、
    /// クリック部位ごとの判定を行うためのスクリプト。
    /// UIレイヤーに遮られないよう、Update で直接範囲判定を行う。
    /// </summary>
    public class ClickableCharacter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private EnemyInfoUI enemyInfoUI;

        [Header("Click Area Settings")]
        [Tooltip("部位ごとのクリック判定リスト。上にあるものほど優先して判定されます。")]
        [SerializeField] private List<CharacterClickArea> clickAreas = new List<CharacterClickArea>();

        [Header("Debug")]
        [Tooltip("Game View 上でクリック判定範囲を半透明で表示する")]
        [SerializeField] private bool showDebugArea = true;
        [SerializeField] private Color debugAreaColor = new Color(0f, 1f, 0f, 0.25f);

        private DialogueUI dialogueUI;
        private SpriteRenderer spriteRenderer;
        private List<GameObject> debugOverlays = new List<GameObject>();

        private void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();

            // DialogueUI の参照を自動取得する
            dialogueUI = FindFirstObjectByType<DialogueUI>(FindObjectsInactive.Include);

            // EnemyInfoUI が未設定の場合、親階層から自動取得
            if (enemyInfoUI == null)
            {
                enemyInfoUI = GetComponentInParent<EnemyInfoUI>();
            }

            Debug.Log($"[ClickableCharacter] Start完了 - enemyInfoUI={(enemyInfoUI != null ? "OK" : "NULL")} / spriteRenderer={(spriteRenderer != null ? "OK" : "NULL")}");

            // もしリストが空なら、後方互換のためにデフォルトの全体枠を1つ追加
            if (clickAreas.Count == 0)
            {
                var defaultArea = new CharacterClickArea { areaName = "" };
                if (spriteRenderer != null)
                {
                    defaultArea.size = spriteRenderer.bounds.size;
                    defaultArea.offset = (Vector2)(spriteRenderer.bounds.center - transform.position);
                }
                clickAreas.Add(defaultArea);
            }

            // デバッグ用の半透明オーバーレイを生成
            CreateDebugOverlays();
        }

        private void CreateDebugOverlays()
        {
            if (!showDebugArea) return;

            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            Sprite whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

            for (int i = 0; i < clickAreas.Count; i++)
            {
                var area = clickAreas[i];
                GameObject overlay = new GameObject($"ClickArea_DebugOverlay_{area.areaName}");
                overlay.transform.SetParent(transform, false);

                var sr = overlay.AddComponent<SpriteRenderer>();
                sr.sprite = whiteSprite;
                sr.color = debugAreaColor;
                sr.sortingOrder = 999; // 最前面

                debugOverlays.Add(overlay);
            }

            UpdateDebugOverlays();
        }

        private void UpdateDebugOverlays()
        {
            if (debugOverlays.Count == 0 || debugOverlays.Count != clickAreas.Count) return;

            for (int i = 0; i < clickAreas.Count; i++)
            {
                var area = clickAreas[i];
                var overlay = debugOverlays[i];

                Vector2 size = area.size;
                Vector2 offset = area.offset;

                overlay.transform.localPosition = new Vector3(offset.x, offset.y, -0.01f);
                overlay.transform.localScale = new Vector3(size.x, size.y, 1f);
            }
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (Camera.main == null) return;

            Vector2 screenPos = mouse.position.ReadValue();
            float zDistance = Camera.main.WorldToScreenPoint(transform.position).z;
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDistance));

            // リストの上から順に判定（重なっている場合は上の要素が優先される）
            foreach (var area in clickAreas)
            {
                if (IsInsideClickArea(worldPos, area))
                {
                    OnClicked(area.areaName);
                    break; // 1箇所クリックしたら終了
                }
            }
        }

        private bool IsInsideClickArea(Vector3 worldPos, CharacterClickArea area)
        {
            Vector2 center = (Vector2)transform.position + area.offset;
            float halfW = area.size.x * 0.5f;
            float halfH = area.size.y * 0.5f;

            return worldPos.x >= center.x - halfW && worldPos.x <= center.x + halfW &&
                   worldPos.y >= center.y - halfH && worldPos.y <= center.y + halfH;
        }

        private void OnClicked(string areaName)
        {
            Debug.Log($"[ClickableCharacter] クリック検知！部位: {areaName}");

            if (enemyInfoUI == null)
            {
                Debug.LogWarning("[ClickableCharacter] enemyInfoUI が null です！");
                return;
            }

            int randomIdx = Random.Range(1, 21); // 1〜20
            
            // 部位名が設定されている場合は、プレフィックスとして追加
            // 例: areaNameが"Head"の場合 -> "クリックされた時_Head1"
            // 空欄の場合は従来通り -> "クリックされた時1"
            string condition = string.IsNullOrEmpty(areaName) 
                ? $"クリックされた時{randomIdx}" 
                : $"クリックされた時_{areaName}{randomIdx}";

            var entry = Managers.DialogueManager.Instance.GetDialogueEntry(condition);
            
            if (entry != null)
            {
                if (dialogueUI != null && !string.IsNullOrEmpty(entry.Dialogue1))
                {
                    dialogueUI.ShowText(entry.Dialogue1.Contains("「") ? entry.Dialogue1 : $"「{entry.Dialogue1}」");
                }
                if (!string.IsNullOrEmpty(entry.Expression) || !string.IsNullOrEmpty(entry.Pose)) 
                {
                    enemyInfoUI.PlayReactionWithVisualId(entry.Pose, entry.Expression, Random.Range(3.0f, 5.0f));
                }
                else 
                {
                    enemyInfoUI.PlayBounceAnimation(0.3f);
                }
            }
            else
            {
                // エントリーが見つからなかった場合のフォールバック
                // 部位専用のセリフが無い場合は、従来の全体用条件で再検索を試みる
                string fallbackCondition = $"クリックされた時{randomIdx}";
                var fallbackEntry = Managers.DialogueManager.Instance.GetDialogueEntry(fallbackCondition);

                if (fallbackEntry != null)
                {
                    if (dialogueUI != null && !string.IsNullOrEmpty(fallbackEntry.Dialogue1))
                    {
                        dialogueUI.ShowText(fallbackEntry.Dialogue1.Contains("「") ? fallbackEntry.Dialogue1 : $"「{fallbackEntry.Dialogue1}」");
                    }
                    if (!string.IsNullOrEmpty(fallbackEntry.Expression) || !string.IsNullOrEmpty(fallbackEntry.Pose)) 
                    {
                        enemyInfoUI.PlayReactionWithVisualId(fallbackEntry.Pose, fallbackEntry.Expression, Random.Range(3.0f, 5.0f));
                    }
                    else 
                    {
                        enemyInfoUI.PlayBounceAnimation(0.3f);
                    }
                }
                else
                {
                    // 最終フォールバック
                    string clickDialogue = enemyInfoUI.PlayReaction(ReactionTrigger.Click, Random.Range(3.0f, 5.0f));
                    enemyInfoUI.PlayBounceAnimation(0.3f);
                    if (dialogueUI != null && clickDialogue != null)
                    {
                        dialogueUI.ShowText(clickDialogue);
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (clickAreas == null || clickAreas.Count == 0) return;

            foreach (var area in clickAreas)
            {
                Vector2 center = (Vector2)transform.position + area.offset;
                Vector2 size = area.size;

                Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
                Gizmos.DrawCube(new Vector3(center.x, center.y, transform.position.z), new Vector3(size.x, size.y, 0.1f));
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(new Vector3(center.x, center.y, transform.position.z), new Vector3(size.x, size.y, 0.1f));
            }
        }
    }
}
