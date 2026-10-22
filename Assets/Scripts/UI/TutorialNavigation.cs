using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>途中終了と、全編を完了して次へ進む帰還を区別する。</summary>
    public static class TutorialNavigation
    {
        public const string ReturnKey = "Title_ReturnToRoom";
        public const int RevivalToMatchMenu = 3;

        /// <summary>チュートリアルを最後まで終えたとき（復活の白い光を通らない経路）。部屋で対局を選ぶ画面を開く。</summary>
        public const int OpenMatchMenuInRoom = 4;

        /// <summary>チュートリアルを終えたあとに移る部屋（タイトルの「デバッグ部屋」と同じ）。2026-10-23 ユーザー指示</summary>
        public const string DebugRoomSceneName = "部屋シーン";
        private const string PendingKey = "Tutorial_PendingReturn";

        public static void Begin(int destination)
        {
            Cancel();
            PlayerPrefs.SetInt(PendingKey, destination);
            PlayerPrefs.Save();
        }

        public static void Complete(int destinationOverride = 0)
        {
            int destination = destinationOverride != 0 ? destinationOverride : PlayerPrefs.GetInt(PendingKey, 0);
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
