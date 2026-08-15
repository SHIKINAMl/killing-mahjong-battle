using UnityEngine;
using UnityEditor;

public class PlayerPrefsClear
{
    // 一時的に非表示。Player Settings の Scripting Define Symbols に KM_ALL_TOOLS を足すと戻る
#if KM_ALL_TOOLS
    [MenuItem("Tools/Clear PlayerPrefs")]
#endif
    public static void ClearAllPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("PlayerPrefs（保存されたコンフィグやセーブデータ）を完全にリセットしました！");
    }
}
