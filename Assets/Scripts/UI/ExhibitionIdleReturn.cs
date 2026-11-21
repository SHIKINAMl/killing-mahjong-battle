using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace KillingMahjong.UI
{
    /// <summary>展示経路だけで離席を検知する。演出や通信受信では操作時間を更新しない。</summary>
    public sealed class ExhibitionIdleReturn : MonoBehaviour
    {
        /// <summary>
        /// 何秒操作が無ければ「〇秒後に最初からやり直します」を出すか。このあと <see cref="warningSeconds"/> 待ってリセットする。
        /// 2026-10-23 のユーザー指示で 5 分 → 2 分 30 秒 → 「1分30秒にして」。
        /// 最後に触ってから 1 分 30 秒でリセットされるよう、知らせを出すまでを 75 秒にする（知らせの 15 秒と合わせて 90 秒）。
        /// シーンには置かず実行時に足す部品なので、ここの値がそのまま効く
        /// </summary>
        [SerializeField] private float idleSeconds = 75f;
        [SerializeField] private float warningSeconds = 15f;
        private float lastInput;
        private SessionPrompt prompt;

        /// <summary>
        /// 起動したときから見張る（2026-10-23。ユーザーから「チュートリアルで放置してもタイトルに戻らない」）。
        /// 以前はタイトルを一度通ったときだけ見張り始めていたので、チュートリアルのシーンから始めると
        /// （エディタでそのシーンを再生したときなど）いつまでも戻らなかった
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AttachToScene(SceneManager.GetActiveScene());
        }

        /// <summary>タイトルから呼ぶ。いまは起動したときから見張っているので、念のため付け直すだけ</summary>
        public static void EnableSession()
        {
            AttachToScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { AttachToScene(scene); }

        private static void AttachToScene(Scene scene)
        {
            // 部屋シーンも見張る。チュートリアルを終えると部屋で対局を選ぶ画面が開くので、
            // そこで立ち去られても戻れるように（2026-10-23 ユーザー指示）
            if (scene.name != "タイトルシーン" && scene.name != "OpeningScene" && scene.name != "UIテストシーン"
                && scene.name != TutorialNavigation.DebugRoomSceneName) return;
            if (FindFirstObjectByType<ExhibitionIdleReturn>() == null)
                new GameObject("ExhibitionIdleReturn").AddComponent<ExhibitionIdleReturn>();
        }

        private void OnEnable() { lastInput = Time.realtimeSinceStartup; }

        private void Update()
        {
            if (HasInput()) ContinueSession();
            float idle = Time.realtimeSinceStartup - lastInput;
            if (idle < idleSeconds) return;
            // 待機中のタイトルは、そのまま次の来場者を迎えられる。
            var titleTarget = GameObject.Find("TitleClickToTutorial");
            if (titleTarget != null) { lastInput = Time.realtimeSinceStartup; return; }
            if (prompt == null)
                prompt = SessionPrompt.Show("", "続ける", ContinueSession);
            float remaining = idleSeconds + warningSeconds - idle;
            prompt.SetMessage("操作がないため、" + Mathf.CeilToInt(Mathf.Max(0f, remaining)) +
                "秒後に最初からやり直します。\n続ける場合は操作してください。");
            if (remaining <= 0f)
            {
                enabled = false;
                // 次の来場者のために、名前・記録・チュートリアルの進み具合も消して起動し直す
                prompt.ResetForNextVisitor();
            }
        }

        private void ContinueSession()
        {
            lastInput = Time.realtimeSinceStartup;
            if (prompt != null) Destroy(prompt.gameObject);
            prompt = null;
        }

        private static bool HasInput()
        {
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed ||
                mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.scroll.ReadValue().sqrMagnitude > 0f)) return true;
            if (Keyboard.current != null && Keyboard.current.anyKey.isPressed) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) return true;
            var pad = Gamepad.current;
            return pad != null && (pad.buttonSouth.isPressed || pad.buttonEast.isPressed ||
                pad.dpad.ReadValue().sqrMagnitude > 0f || pad.leftStick.ReadValue().sqrMagnitude > 0.1f);
        }
    }
}
