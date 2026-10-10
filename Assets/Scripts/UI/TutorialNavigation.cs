using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>途中終了と、全編を完了して次へ進む帰還を区別する。</summary>
    public static class TutorialNavigation
    {
        public const string ReturnKey = "Title_ReturnToRoom";
        private const string PendingKey = "Tutorial_PendingReturn";

        public static void Begin(int destination)
        {
            Cancel();
            PlayerPrefs.SetInt(PendingKey, destination);
            PlayerPrefs.Save();
        }

        public static void Complete()
        {
            int destination = PlayerPrefs.GetInt(PendingKey, 0);
            PlayerPrefs.DeleteKey(PendingKey);
            if (destination != 0) PlayerPrefs.SetInt(ReturnKey, destination);
            PlayerPrefs.Save();
        }

        public static void Cancel()
        {
            PlayerPrefs.DeleteKey(PendingKey);
            PlayerPrefs.DeleteKey(ReturnKey);
            PlayerPrefs.DeleteKey("Tutorial_RequestedStartRound");
            PlayerPrefs.Save();
        }
    }
}
