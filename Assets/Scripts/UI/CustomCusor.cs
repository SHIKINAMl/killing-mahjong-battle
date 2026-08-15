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

    [Header("クリック時の傾き")]
    [Tooltip("押している間の回転角（度）。マイナスで時計回り。0で無効")]
    [SerializeField] private float clickRotationAngle = -12f;
    [Tooltip("回転の軸。画像に対する割合で (0,0)=左上 / (1,1)=右下。\n" +
             "既定の (0.5, 1) は下端中央＝手首あたり。ここを軸に指先が振れる。")]
    [SerializeField] private Vector2 rotationAnchor = new Vector2(0.5f, 1f);

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

            // 押している間だけ傾ける。補間せず、押した瞬間に角度へ飛ばす
            bool pressed = mouse != null && mouse.leftButton.isPressed;
            float angle = pressed ? clickRotationAngle : 0f;
            cursorRect.localRotation = Quaternion.Euler(0f, 0f, angle);

            // マウス座標に追従 (新しいInput System対応)
            if (mouse != null)
            {
                cursorRect.position = mouse.position.ReadValue() + GetAnchorRotationOffset(angle);
            }

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
    /// 回転の軸を pivot（＝ホットスポット＝指先）から rotationAnchor（＝手首）へ移すための位置補正。
    ///
    /// RectTransform は必ず pivot を中心に回るので、そのままでは指先が固定されて
    /// 手の根元が振れてしまう。軸を手首側へ移したいので、
    /// 「pivot 回りの回転」を「anchor 回りの回転」に付け替える平行移動を足す。
    ///
    ///   pivot 回り: x → O + R(x-O)   anchor 回り: x → A + R(x-A)
    ///   差分       : A + R(O-A) - O = v - R·v   （v = A - O）
    ///
    /// この補正の結果、押している間は指先がマウス座標から少しズレて振れる（狙った演出）。
    /// クリック判定そのものはOSのマウス座標なので影響しない。
    /// </summary>
    private Vector2 GetAnchorRotationOffset(float angleDeg)
    {
        if (Mathf.Approximately(angleDeg, 0f)) return Vector2.zero;

        // pivot と同じ座標系（左下原点の割合）に直してから、ピクセルの差に変換する
        Vector2 anchor01 = new Vector2(rotationAnchor.x, 1f - rotationAnchor.y);
        Vector2 v = (anchor01 - cursorRect.pivot) * cursorSize;

        float rad = angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        Vector2 rotated = new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);

        return v - rotated;
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