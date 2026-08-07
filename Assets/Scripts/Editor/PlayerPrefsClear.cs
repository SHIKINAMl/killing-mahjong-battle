using UnityEngine;
using UnityEditor;

public class PlayerPrefsClear
{
    [MenuItem("Tools/Clear PlayerPrefs")]
    public static void ClearAllPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("PlayerPrefs（保存されたコンフィグやセーブデータ）を完全にリセットしました！");
    }
}
