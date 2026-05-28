using UnityEngine;

public class CustomCursor : MonoBehaviour
{
    // Inspectorから手の絵のテクスチャをアタッチします
    [SerializeField] private Texture2D handCursor; 

    [Tooltip("画像のどのピクセルをクリックの判定(先端)にするか。\n(0,0)は左上。右に行くほどXが、下に行くほどYが増えます。")]
    [SerializeField] private Vector2 hotspot = new Vector2(0, 0);

    void Start()
    {
        // カーソルを手の絵に変更
        Cursor.SetCursor(handCursor, hotspot, CursorMode.Auto);
    }

    // 元のマウスカーソルに戻す時の処理
    public void ResetCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}