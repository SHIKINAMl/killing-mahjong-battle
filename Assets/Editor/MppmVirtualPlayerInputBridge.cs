using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace KillingMahjong.EditorTools
{
    /// <summary>
    /// MPPM Virtual Player 用の入力ブリッジ（Editor専用・ビルドには含まれない）。
    ///
    /// 背景:
    /// MPPMのクローンエディタは -noMainWindow で起動されるため、Windowsのネイティブ入力
    /// デバイス列挙が行われず、Input System に Mouse/Keyboard が1つも登録されない。
    /// その結果、Virtual Player のGame View内でUIをクリックしても反応しない。
    ///
    /// 対処:
    /// エディタUI層(UIToolkit)にはOSのポインタイベントが正常に届いているため、
    /// Game View 上のポインタイベントを捕捉し、仮想 Mouse デバイスの状態イベントに
    /// 変換して Input System へ流し込む。座標変換には GameView 内部の
    /// gameMouseOffset / gameMouseScale（Unity自身がエディタ→ゲーム座標変換に使う値）を用いる。
    ///
    /// ネイティブの Mouse が存在する環境（メインエディタ）では何もしない。
    /// </summary>
    [InitializeOnLoad]
    internal static class MppmVirtualPlayerInputBridge
    {
        private static readonly Type GameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        private static readonly PropertyInfo GameMouseOffsetProp =
            GameViewType?.GetProperty("gameMouseOffset", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly PropertyInfo GameMouseScaleProp =
            GameViewType?.GetProperty("gameMouseScale", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private static readonly PropertyInfo TargetRenderSizeProp =
            GameViewType?.GetProperty("targetRenderSize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        private const string HookMarker = "mppm-input-bridge-hooked";

        private static Mouse _virtualMouse;
        private static double _nextScanTime;

        static MppmVirtualPlayerInputBridge()
        {
            if (GameViewType == null) return;
            EditorApplication.update += Update;
        }

        /// <summary>ネイティブMouseが1つも無い（=クローンエディタ）場合のみブリッジが必要</summary>
        private static bool BridgeNeeded()
        {
            foreach (var device in InputSystem.devices)
            {
                if (device is Mouse && device.native) return false;
            }
            return true;
        }

        private static void Update()
        {
            // 毎フレーム走査する必要はないので1秒間隔
            if (EditorApplication.timeSinceStartup < _nextScanTime) return;
            _nextScanTime = EditorApplication.timeSinceStartup + 1.0;

            if (!BridgeNeeded()) return;

            if (_virtualMouse == null || !_virtualMouse.added)
            {
                _virtualMouse = InputSystem.AddDevice<Mouse>("MPPM Bridge Mouse");
                Debug.Log("[MppmInputBridge] 仮想Mouseデバイスを登録しました (クローンエディタ用入力ブリッジ有効)");
            }

            // 開いている全GameViewへフック（ウィンドウ再生成に備えて定期チェック）
            foreach (var obj in Resources.FindObjectsOfTypeAll(GameViewType))
            {
                var gameView = obj as EditorWindow;
                if (gameView == null) continue;
                var root = gameView.rootVisualElement;
                if (root == null || (root.userData as string) == HookMarker) continue;
                root.userData = HookMarker;

                var captured = gameView;
                root.RegisterCallback<PointerMoveEvent>(e => Forward(captured, e.position, null), TrickleDown.TrickleDown);
                root.RegisterCallback<PointerDownEvent>(e => Forward(captured, e.position, true), TrickleDown.TrickleDown);
                root.RegisterCallback<PointerUpEvent>(e => Forward(captured, e.position, false), TrickleDown.TrickleDown);
            }
        }

        private static void Forward(EditorWindow gameView, Vector3 panelPosition, bool? leftButtonDown)
        {
            if (_virtualMouse == null || !_virtualMouse.added) return;
            if (!EditorApplication.isPlaying) return;

            try
            {
                var offset = (Vector2)GameMouseOffsetProp.GetValue(gameView);
                var scale = (float)GameMouseScaleProp.GetValue(gameView);
                var renderSize = (Vector2)TargetRenderSizeProp.GetValue(gameView);

                // Unity内部と同じ変換: (ビュー座標 + offset) * scale → ゲーム座標(上原点)
                Vector2 gamePos = ((Vector2)panelPosition + offset) * scale;
                // Input System のマウス座標は下原点なのでYを反転
                var position = new Vector2(gamePos.x, renderSize.y - gamePos.y);

                // ゲーム画面の外(ツールバーや黒帯)は無視
                if (position.x < 0 || position.y < 0 || position.x > renderSize.x || position.y > renderSize.y) return;

                var state = new MouseState { position = position };
                bool pressed = leftButtonDown ?? _virtualMouse.leftButton.isPressed;
                state = state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, pressed);
                InputSystem.QueueStateEvent(_virtualMouse, state);
            }
            catch (Exception)
            {
                // 内部APIの変更等で失敗しても、エディタ動作には影響させない
            }
        }
    }
}
