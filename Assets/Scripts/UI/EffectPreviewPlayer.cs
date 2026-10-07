using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>固定IDで演出だけを試写する。通信・判定・戦績を通らず、毎回舞台を作り直す。</summary>
    public sealed partial class EffectPreviewPlayer : MonoBehaviour
    {
        public static EffectPreviewPlayer Current { get; private set; }
        public static bool IsOpen => Current != null;
        public string EffectId { get; private set; }
        public string State { get; private set; }
        public int CompletedPlays { get; private set; }
        private Scene sourceScene, previewScene;
        private EffectPreviewRig rig;
        private bool loop, closing;
        private readonly List<Behaviour> hidden = new List<Behaviour>();
        private readonly List<Renderer> hiddenRenderers = new List<Renderer>();
        private Action closed;
        private PresentationScope scope;
        private TextMeshProUGUI stateLabel, loopLabel;

        public static EffectPreviewPlayer Open(string id, bool repeat = false, Action onClosed = null)
        {
            GameEffectCatalog.Find(id); // 不明なIDは、画面を隠す前に弾く。
            if (Current != null) Current.Close();
            if (Resources.Load<GameObject>("Presentation/EffectPreviewRig") == null)
                throw new InvalidOperationException("演出試写舞台がありません。Tools/演出 から更新してください。");
            var go = new GameObject("EffectPreviewPlayer", typeof(RectTransform));
            var player = go.AddComponent<EffectPreviewPlayer>();
            Current = player;
            player.EffectId = id; player.loop = repeat; player.closed = onClosed;
            player.sourceScene = SceneManager.GetActiveScene();
            player.HideOriginal();
            player.previewScene = SceneManager.CreateScene("EffectPreview");
            SceneManager.SetActiveScene(player.previewScene);
            player.BuildControls();
            player.StartCoroutine(player.Run());
            return player;
        }

        private void HideOriginal()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (canvas.enabled && canvas.GetComponentInParent<ScreenTint>() == null && canvas.GetComponentInParent<HpDamageGlitch>() == null)
                { hidden.Add(canvas); canvas.enabled = false; }
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera.enabled) { hidden.Add(camera); camera.enabled = false; }
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (renderer.enabled) { hiddenRenderers.Add(renderer); renderer.enabled = false; }
        }

        private IEnumerator Run()
        {
            do
            {
                State = "preparing";
                // 前の再生で変えたHP・牌・立ち絵も、保存済みの舞台から取り直す。
                CleanupPresentation();
                foreach (var root in previewScene.GetRootGameObjects()) Destroy(root);
                yield return null;
                var prefab = Resources.Load<GameObject>("Presentation/EffectPreviewRig");
                var rootObject = Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(rootObject, previewScene);
                rootObject.SetActive(true);
                rig = rootObject.GetComponent<EffectPreviewRig>();
                rig.Initialize();
                yield return null;
                Canvas.ForceUpdateCanvases();
                State = "playing"; stateLabel.text = "再生中";
                scope = new PresentationScope(Debug.LogException);
                // 入れ子のIEnumeratorの例外も捕捉し、失敗しても「一覧へ」で必ず戻れる。
                yield return Guard(PlayEffect());
                CleanupPresentation();
                if (State != "error") { State = "complete"; CompletedPlays++; stateLabel.text = "再生終了"; }
                yield return new WaitForSecondsRealtime(1f);
            } while (loop && State != "error");
        }

        private IEnumerator Guard(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                var top = stack.Peek(); object next = null; bool moved = false;
                try { moved = top.MoveNext(); if (moved) next = top.Current; }
                catch (Exception e) { State = "error"; stateLabel.text = "再生できませんでした"; Debug.LogException(e); }
                if (State == "error") break;
                if (!moved) { (top as IDisposable)?.Dispose(); stack.Pop(); }
                else if (next is IEnumerator nested) stack.Push(nested);
                else yield return next;
            }
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        }

        public void Replay()
        {
            StopAllCoroutines(); CleanupPresentation();
            StartCoroutine(Run());
        }

        private void CleanupPresentation()
        {
            scope?.Dispose(); scope = null;
            if (rig != null) { rig.phase.CancelTransitions(); rig.ron.CancelPresentation(); }
            RonChanceEffect.HideCurrent(); TutorialHighlightUI.HideCurrent();
            ScreenQuake.Stop(); ScreenTint.Clear(0f);
        }

        public void Close()
        {
            if (closing) return;
            closing = true; StopAllCoroutines(); CleanupPresentation();
            if (sourceScene.IsValid() && sourceScene.isLoaded) SceneManager.SetActiveScene(sourceScene);
            if (previewScene.IsValid() && previewScene.isLoaded) SceneManager.UnloadSceneAsync(previewScene);
            foreach (var item in hidden) if (item != null) item.enabled = true;
            foreach (var item in hiddenRenderers) if (item != null) item.enabled = true;
            Current = null; closed?.Invoke();
            Destroy(gameObject);
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
        }
        private void OnDestroy() { if (!closing) Close(); }

        private void BuildControls()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800, 600); scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var blocker = new GameObject("InputBlocker", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            blocker.transform.SetParent(transform, false); blocker.color = Color.clear;
            blocker.rectTransform.anchorMin = Vector2.zero; blocker.rectTransform.anchorMax = Vector2.one;
            blocker.rectTransform.sizeDelta = Vector2.zero;
            var header = new GameObject("Header", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            header.transform.SetParent(transform, false); header.color = new Color32(28, 18, 26, 245);
            header.rectTransform.sizeDelta = new Vector2(800, 38); header.rectTransform.anchoredPosition = new Vector2(0, 281);
            var entry = GameEffectCatalog.Find(EffectId);
            Text(header.transform, entry.Name, new Vector2(-80, 8), new Vector2(620, 21), 16);
            Text(header.transform, entry.Id, new Vector2(-80, -10), new Vector2(620, 17), 12);
            stateLabel = Text(header.transform, "準備中", new Vector2(322, 0), new Vector2(145, 22), 15);
            Control("もう一度", -255, Replay);
            loopLabel = Control(loop ? "繰り返し：入" : "繰り返し：切", 0, () => {
                loop = !loop; loopLabel.text = loop ? "繰り返し：入" : "繰り返し：切";
                if (loop && State == "complete") Replay();
            });
            Control("一覧へ戻る", 255, Close);
        }

        private TextMeshProUGUI Control(string label, float x, Action action)
        {
            var image = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Image>();
            image.transform.SetParent(transform, false); image.color = new Color32(52, 30, 42, 255);
            image.rectTransform.sizeDelta = new Vector2(190, 29); image.rectTransform.anchoredPosition = new Vector2(x, -283);
            image.GetComponent<Button>().onClick.AddListener(() => action());
            return Text(image.transform, label, Vector2.zero, new Vector2(190, 29), 16);
        }
        private TextMeshProUGUI Text(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false); text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position;
            text.font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed");
            text.text = value; text.fontSize = fontSize; text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            return text;
        }
    }
}
