using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KillingMahjong.Tests
{
    [Category("DebugRoomNavigation")]
    public class DebugRoomNavigationTests
    {
        private const string TitleScene = "タイトルシーン";
        private const string RoomScene = "部屋シーン";
        private const string ReturnKey = "Title_ReturnToRoom";
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private float previousTimeScale;
        private bool previousRunInBackground, hadReturnKey;
        private int previousReturn;

        private static Type MainType(string name)
        {
            return Type.GetType("KillingMahjong.UI." + name + ", Assembly-CSharp", true);
        }

        private static Component Loading
        {
            get { return (Component)MainType("LoadingManager").GetProperty("Instance").GetValue(null); }
        }

        private static object Field(Component component, string name)
        {
            return component.GetType().GetField(name, Members).GetValue(component);
        }

        private static void Call(Component component, string method, params object[] args)
        {
            component.GetType().GetMethod(method, Members).Invoke(component, args);
        }

        private static void Click(string name)
        {
            // 本体は Assembly-CSharp のため、実際の Button/onClick を反射経由で操作する。
            var buttonType = Type.GetType("UnityEngine.UI.Button, UnityEngine.UI", true);
            var button = Resources.FindObjectsOfTypeAll(buttonType).Cast<Component>()
                .Single(c => c.name == name && c.gameObject.activeInHierarchy && c.gameObject.scene.IsValid());
            Assert.That((bool)buttonType.GetProperty("interactable").GetValue(button), Is.True);
            var onClick = buttonType.GetProperty("onClick").GetValue(button);
            onClick.GetType().GetMethod("Invoke").Invoke(onClick, null);
        }

        private static IEnumerator WaitForScreen(string sceneName, string readyObject)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var canvas = (GameObject)Field(Loading, "loadingCanvasObj");
                if (SceneManager.GetActiveScene().name == sceneName && GameObject.Find(readyObject) != null
                    && !canvas.activeSelf)
                {
                    Assert.That((int)Field(Loading, "showCount"), Is.Zero);
                    var image = (Component)Field(Loading, "fadeBgImage");
                    var color = (Color)image.GetType().GetProperty("color").GetValue(image);
                    Assert.That(color.a, Is.Zero);
                    yield break;
                }
                yield return null;
            }
            Assert.Fail("Screen/loader did not become ready: " + sceneName);
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousTimeScale = Time.timeScale;
            previousRunInBackground = Application.runInBackground;
            hadReturnKey = PlayerPrefs.HasKey(ReturnKey);
            previousReturn = PlayerPrefs.GetInt(ReturnKey);
            PlayerPrefs.DeleteKey(ReturnKey);
            Application.runInBackground = true;
            Time.timeScale = 1f;
            Call(Loading, "ForceHide");
            yield return SceneManager.LoadSceneAsync(TitleScene);
            yield return WaitForScreen(TitleScene, "TitleClickToTutorial");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            Call(Loading, "ForceHide");
            // 各試験をタイトルで終え、保存済みの一回限りの帰還要求を元に戻す。
            yield return SceneManager.LoadSceneAsync(TitleScene);
            yield return WaitForScreen(TitleScene, "TitleClickToTutorial");
            if (hadReturnKey) PlayerPrefs.SetInt(ReturnKey, previousReturn);
            else PlayerPrefs.DeleteKey(ReturnKey);
            Time.timeScale = previousTimeScale;
            Application.runInBackground = previousRunInBackground;
        }

        [UnityTest]
        public IEnumerator RepeatedRoomReturnsRestoreTitleInputAndClearLoading()
        {
            for (int i = 0; i < 3; i++)
            {
                Click("デバッグ部屋");
                yield return WaitForScreen(RoomScene, "RoomMenu_Title");
                Click("RoomMenu_Title");
                yield return WaitForScreen(TitleScene, "TitleClickToTutorial");
                var title = (Component)UnityEngine.Object.FindFirstObjectByType(MainType("TitleUIManager"));
                Assert.That((bool)title.GetType().GetProperty("StartsInRoom").GetValue(title), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator RoomRoundTripCompletesWhileGameTimeIsPaused()
        {
            Time.timeScale = 0f;
            Click("デバッグ部屋");
            yield return WaitForScreen(RoomScene, "RoomMenu_Title");
            Click("RoomMenu_Title");
            yield return WaitForScreen(TitleScene, "TitleClickToTutorial");
        }

        [UnityTest]
        public IEnumerator DirectTitleLoadAlsoClearsAnExistingBlackLoadingScreen()
        {
            Click("デバッグ部屋");
            yield return WaitForScreen(RoomScene, "RoomMenu_Title");
            // 設定画面等の同期ロード経路でも、移動先の Start が後始末をする。
            Call(Loading, "FadeOutScreen", new object[] { null });
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.That(((GameObject)Field(Loading, "loadingCanvasObj")).activeSelf, Is.True);
            SceneManager.LoadScene(TitleScene);
            yield return WaitForScreen(TitleScene, "TitleClickToTutorial");
        }
    }
}
