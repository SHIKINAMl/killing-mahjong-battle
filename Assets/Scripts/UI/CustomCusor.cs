using UnityEngine;

public class CustomCursor : MonoBehaviour
{
    // Inspectorから手の絵のテクスチャをアタッチします
    [SerializeField] private Texture2D handCursor; 

    void Start()
    {
        // 画像のどのピクセルをクリックの判定(先端)にするかを決めます。
        // (0,0)は画像の左上です。指先に合わせたい場合は (16, 16) など調整してください。
        Vector2 hotspot = new Vector2(0, 0);

        // カーソルを手の絵に変更
        Cursor.SetCursor(handCursor, hotspot, CursorMode.Auto);
    }

    // 元のマウスカーソルに戻す時の処理
    public void ResetCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}