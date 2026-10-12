using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

public class CustomCursor : MonoBehaviour
{
    [SerializeField] private Texture2D handCursor; 
    [Tooltip("画像のどのピクセルをクリックの判定(先端)にするか。\n(0,0)は左上。右に行くほどXが、下に行くほどYが増えます。")]
    [SerializeField] private Vector2 hotspot = new Vector2(26, 24);
    [Tooltip("画面上でのカーソルの大きさ（ピクセル）")]
    [SerializeField] private float cursorSize = 64f;

    [Header("クリック時の傾き")]
    [Tooltip("押している間の回転角（度）。マイナスで時計回り。0で無効")]
    [SerializeField] private float clickRotationAngle = -12f;

    private GameObject cursorCanvasObj;
    private RectTransform cursorRect;

    /// <summary>
    /// OS のカーソルを消してよいか。
    ///
    /// **エディタでは既定で消さない（2026-08-15 の要望）。**
    /// ゲームビューをクリックすると本物のカーソルまで消えてしまい、
    /// 開発中はどこを指しているのか分からなくなるため。
    /// 手の絵（UICursorCanvas）はそのまま出るので、見た目の確認自体はできる。
    ///
    /// **ビルドの挙動は変えない。** `#if UNITY_EDITOR` の外では常に true。
    /// 本番と同じ見た目を確かめたいときは
    /// `Tools/開発用/ゲーム中も OS のカーソルを表示` で切り替える。
    /// </summary>
    private static bool ShouldHideOsCursor
    {
#if UNITY_EDITOR
        get { return !KeepOsCursorInEditor; }
#else
        get { return true; }
#endif
    }

#if UNITY_EDITOR
    public const string KeepCursorPrefKey = "KillingMahjong.KeepOsCursorInEditor";

    // **EditorPrefs を毎フレーム読まない。** Update から呼ばれるので、
    // 一度だけ読んでキャッシュし、メニュー側が書き換えたときに更新する
    private static bool _keepCached;
    private static bool _keepLoaded;

    public static bool KeepOsCursorInEditor
    {
        get
        {
            if (!_keepLoaded)
            {
                _keepCached = UnityEditor.EditorPrefs.GetBool(KeepCursorPrefKey, true);
                _keepLoaded = true;
            }
            return _keepCached;
        }
        set
        {
            _keepCached = value;
            _keepLoaded = true;
            UnityEditor.EditorPrefs.SetBool(KeepCursorPrefKey, value);
        }
    }
#endif

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
        // OS標準のカーソルを消す（エディタでは既定で消さない。ShouldHideOsCursor を見よ）
        if (ShouldHideOsCursor) Cursor.visible = false;

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
            var mouse = UnityEngine.InputSystem.Mouse.current;

            bool pressed = mouse != null && mouse.leftButton.isPressed;
            ApplyPointerState(mouse != null ? mouse.position.ReadValue() : (Vector2)cursorRect.position, pressed);

            // 何かの拍子にカーソルが出てしまうのを毎フレーム押さえ込む。
            // **エディタで「表示したまま」にしているときは逆に出し続ける。**
            // ここを素通りさせるだけだと、他所の Cursor.visible = false に負けて消える
            if (ShouldHideOsCursor)
            {
                if (Cursor.visible) Cursor.visible = false;
            }
            else
            {
                if (!Cursor.visible) Cursor.visible = true;
            }
        }
    }

    /// <summary>
    /// ホットスポット（指先）を入力座標に固定したまま、押下中だけ手を傾ける。
    /// 手首回りの位置補正は加えない。通常・押下・解放でクリック地点が変わらない。
    /// </summary>
    private void ApplyPointerState(Vector2 pointerPosition, bool pressed)
    {
        cursorRect.position = pointerPosition;
        cursorRect.localRotation = Quaternion.Euler(0f, 0f, pressed ? clickRotationAngle : 0f);
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
