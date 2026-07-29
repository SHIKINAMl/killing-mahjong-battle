using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

public class CustomCursor : MonoBehaviour
{
    [SerializeField] private Texture2D handCursor; 
    [Tooltip("画像のどのピクセルをクリックの判定(先端)にするか。\n(0,0)は左上。右に行くほどXが、下に行くほどYが増えます。")]
    [SerializeField] private Vector2 hotspot = new Vector2(0, 0);
    [Tooltip("画面上でのカーソルの大きさ（ピクセル）")]
    [SerializeField] private float cursorSize = 64f;

    private GameObject cursorCanvasObj;
    private RectTransform cursorRect;

    void Start()
    {
        // 既存のハードウェアカーソル設定をリセット
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        if (handCursor != null)
        {
            CreateUICursor();
        }
    }

    private void CreateUICursor()
    {
        // OS標準のカーソルを消す
        Cursor.visible = false;

        // 全てのUIの上に描画するための専用Canvasを作成
        cursorCanvasObj = new GameObject("UICursorCanvas");
        Canvas canvas = cursorCanvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = UISortingOrders.MouseCursor; // 一番手前に表示
        
        cursorCanvasObj.AddComponent<CanvasScaler>(); // 解像度に依存しないようにする
        
        // カーソル自体がクリックをブロックしないようにRaycasterはオフ
        var raycaster = cursorCanvasObj.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        GameObject cursorImageObj = new GameObject("CursorImage");
        cursorImageObj.transform.SetParent(cursorCanvasObj.transform, false);

        Image img = cursorImageObj.AddComponent<Image>();
        img.sprite = Sprite.Create(handCursor, new Rect(0, 0, handCursor.width, handCursor.height), new Vector2(0, 1));
        img.raycastTarget = false;

        cursorRect = cursorImageObj.GetComponent<RectTransform>();
        
        // ホットスポットの計算（画像サイズに対する割合）
        float pivotX = hotspot.x / handCursor.width;
        float pivotY = 1f - (hotspot.y / handCursor.height);
        cursorRect.pivot = new Vector2(pivotX, pivotY);
        
        // 巨大化バグを防ぐため、指定したサイズに固定
        cursorRect.sizeDelta = new Vector2(cursorSize, cursorSize);
    }

    void Update()
    {
        if (cursorRect != null)
        {
            // マウス座標に追従 (新しいInput System対応)
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                cursorRect.position = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            }
            
            // エディタ等で一時的にカーソルが表示されてしまうのを防ぐ
            if (Cursor.visible)
            {
                Cursor.visible = false;
            }
        }
    }

    // 元のマウスカーソルに戻す時の処理
    public void ResetCursor()
    {
        Cursor.visible = true;
        if (cursorCanvasObj != null)
        {
            Destroy(cursorCanvasObj);
        }
    }
    
    void OnDestroy()
    {
        // シーン遷移や破棄時にカーソルを元に戻す
        Cursor.visible = true;
    }
}