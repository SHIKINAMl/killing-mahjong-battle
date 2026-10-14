using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Managers.Tutorial;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>既存素材で流局を再現する動画空間。通信・HP・戦績・対局の盤面には触れない。</summary>
    public sealed class TutorialDrawReplayUI : MonoBehaviour
    {
        public const string EffectId = "tutorial.draw_replay";
        public int StepIndex { get; private set; }
        public bool IsPaused { get; private set; }
        public string NormalText => normal != null ? normal.text : "";
        private RectTransform viewport, progress;
        private TextMeshProUGUI normal, marker, paused;
        private Button advance;
        private TutorialSubtitleUI subtitles;
        private PhaseTransitionUI phase;
        private TileResourceManager tiles;
        private PixelShapeGraphic noise;
        private TutorialReplayBoard board;
        private bool advanced, automatic;
        private float noiseNext;

        public static TutorialDrawReplayUI Create(Transform owner = null)
        {
            var go = new GameObject("TutorialDrawReplay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(TutorialDrawReplayUI));
            if (owner != null) go.transform.SetParent(owner, false);
            var ui = go.GetComponent<TutorialDrawReplayUI>(); ui.Build(); return ui;
        }

        private RectTransform Box(string name, Transform parent, Vector2 size, Vector2 pos, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.sizeDelta = size; rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = color; go.GetComponent<Image>().raycastTarget = false; return rt;
        }
        private TextMeshProUGUI Label(string name, Transform parent, Vector2 size, Vector2 pos, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.sizeDelta = size; rt.anchoredPosition = pos;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed") ?? TMP_Settings.defaultFontAsset;
            t.fontSize = fontSize; t.color = Color.white; t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false; return t;
        }
        private void Art(string name, string path, Vector2 size, Vector2 position)
        {
            Box(name, viewport, size, position, Color.white).GetComponent<Image>().sprite = Resources.Load<Sprite>(path);
        }
        private void Build()
        {
            var c = GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.overrideSorting = true; c.sortingOrder = 90;
            if(transform.parent is RectTransform) Stretch((RectTransform)transform);
            var s = GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(800, 600); s.matchWidthOrHeight = .5f;
            Box("Dimmer", transform, new Vector2(800,600), Vector2.zero, new Color(0,0,0,.85f));
            Box("PhoneFrame", transform, new Vector2(820,620), Vector2.zero, new Color32(25,25,28,255));
            viewport = Box("VideoViewport", transform, new Vector2(800,600), Vector2.zero, Color.black);
            viewport.gameObject.AddComponent<RectMask2D>();
            Art("Room","2",new Vector2(820,615),Vector2.zero);
            Art("Girl","女の子/通常時身体",new Vector2(600,600),new Vector2(0,15));
            Art("Face","女の子/笑顔",new Vector2(600,600),new Vector2(0,15));
            Art("Table","マージャン卓",new Vector2(820,615),Vector2.zero);
            var prefab = Resources.Load<GameObject>("Presentation/EffectPreviewRig");
            var rig = prefab != null ? prefab.GetComponent<EffectPreviewRig>() : null;
            if (rig == null) throw new InvalidOperationException("流局の試写用素材がありません。");
            tiles = rig.tiles;
            var boardPrefab = Resources.Load<GameObject>("Presentation/TutorialReplayBoard");
            if (boardPrefab == null) throw new InvalidOperationException("流局の盤面素材を更新してください。");
            board = Instantiate(boardPrefab.GetComponent<TutorialReplayBoard>(), viewport, false);
            board.Initialize(tiles);
            var p = Instantiate(rig.phase.gameObject, transform); phase = p.GetComponent<PhaseTransitionUI>(); p.SetActive(true);
            // 独立Canvasのプレハブを子Canvasへ置くと、保存時の0サイズ・0スケールが残る。
            var phaseRect=(RectTransform)p.transform;
            phaseRect.localScale=Vector3.one; phaseRect.localRotation=Quaternion.identity;
            phaseRect.anchorMin=Vector2.zero; phaseRect.anchorMax=Vector2.one;
            phaseRect.offsetMin=Vector2.zero; phaseRect.offsetMax=Vector2.zero;
            foreach(var cv in p.GetComponentsInChildren<Canvas>(true)) { cv.overrideSorting=true; cv.sortingOrder=91; }
            foreach(var g in p.GetComponentsInChildren<Graphic>(true)) g.raycastTarget=false;
            phase.CancelTransitions();
            normal = Label("ReplayNormalText",viewport,new Vector2(285,92),new Vector2(155,58),15);
            normal.alignment=TextAlignmentOptions.TopLeft;
            marker=Label("ReplayNormalMarker",transform,new Vector2(24,24),new Vector2(295,30),20);
            marker.text="▼"; marker.color=new Color32(224,40,40,255); marker.gameObject.SetActive(false);
            var n=new GameObject("ReplayNoise",typeof(RectTransform),typeof(PixelShapeGraphic));
            n.transform.SetParent(viewport,false); ((RectTransform)n.transform).sizeDelta=new Vector2(800,600);
            noise=n.GetComponent<PixelShapeGraphic>(); noise.raycastTarget=false;
            Box("SeekTrack",transform,new Vector2(764,3),new Vector2(0,-288),new Color32(72,72,72,255));
            progress=Box("SeekProgress",transform,new Vector2(1,3),new Vector2(-382,-288),new Color32(224,40,40,255));
            progress.pivot=new Vector2(0,.5f);
            var catcher=Box("ReplayAdvanceCatcher",transform,new Vector2(800,600),Vector2.zero,new Color(0,0,0,.001f));
            catcher.GetComponent<Image>().raycastTarget=true; advance=catcher.gameObject.AddComponent<Button>();
            advance.transition=Selectable.Transition.None; advance.onClick.AddListener(()=>advanced=true); catcher.gameObject.SetActive(false);
            subtitles=TutorialSubtitleUI.Create(); subtitles.transform.SetParent(transform,false);
            Stretch((RectTransform)subtitles.transform);
            // 親を変えたあとで独立した描画順を指定する。
            var subtitleCanvas=subtitles.GetComponent<Canvas>();
            subtitleCanvas.overrideSorting=true; subtitleCanvas.sortingOrder=Common.UISortingOrders.TutorialSubtitle;
            var pauseLayer=new GameObject("ReplayPauseLayer",typeof(RectTransform),typeof(Canvas));
            pauseLayer.transform.SetParent(transform,false);
            var pc=pauseLayer.GetComponent<Canvas>(); pc.renderMode=RenderMode.ScreenSpaceOverlay;
            pc.overrideSorting=true; pc.sortingOrder=93;
            var pauseRect=(RectTransform)pauseLayer.transform;
            pauseRect.anchorMin=Vector2.zero; pauseRect.anchorMax=Vector2.one;
            pauseRect.offsetMin=Vector2.zero; pauseRect.offsetMax=Vector2.zero; pauseRect.localScale=Vector3.one;
            paused=Label("ReplayPaused",pauseLayer.transform,new Vector2(160,70),new Vector2(300,240),24);
            paused.text="Ⅱ\n再生停止"; paused.gameObject.SetActive(false);
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
            rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero;
            rect.localScale=Vector3.one; rect.localRotation=Quaternion.identity;
        }
        private IEnumerator Step(string text,bool subtitle,bool loud=false)
        {
            StepIndex++; progress.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,764f*StepIndex/10f); advanced=false;
            if(subtitle) { marker.gameObject.SetActive(false); advance.gameObject.SetActive(false); subtitles.Show("先輩",text,()=>advanced=true); }
            else {
                string formatted=TutorialEmphasis.Apply(text);
                if(loud) formatted=(TutorialEmphasis.UsesBoldFont?"<font=\"PixelMplus10_TutorialBold\">":"<b>")+"<size=+5>"+text+"</size>"+(TutorialEmphasis.UsesBoldFont?"</font>":"</b>");
                normal.text="「"+formatted+"」"; marker.gameObject.SetActive(true); advance.gameObject.SetActive(true);
            }
            Debug.Log("[TutorialDrawReplay] Step "+StepIndex+(subtitle?" subtitle: ":" normal: ")+text);
            if(automatic) {
                yield return new WaitForSecondsRealtime(1.8f);
                if(subtitle) subtitles.CompleteLine(); else advanced=true;
            }
            else yield return new WaitUntil(()=>advanced);
            marker.gameObject.SetActive(false); advance.gameObject.SetActive(false);
        }
        public IEnumerator Play(bool autoAdvance=false)
        {
            automatic=autoAdvance; IsPaused=false; StepIndex=0; transform.localScale=Vector3.one*.4f;
            for(float t=0;t<1;t+=Time.unscaledDeltaTime) { transform.localScale=Vector3.one*Mathf.SmoothStep(.4f,1,t); yield return null; }
            transform.localScale=Vector3.one;
            // PhaseTransitionUI.Start が通常対局の層へ戻すので、初期化後に動画内の層を適用する。
            foreach(var cv in phase.GetComponentsInChildren<Canvas>(true)) {
                cv.enabled=true; cv.overrideSorting=true; cv.sortingOrder=91;
            }
            yield return Step("おっ立ち上がったね",true);
            yield return Step("今からアタシが流局するから見ててよ",true);
            yield return Step("では惨めに最後の牌を出しなさい？",false);
            yield return Step("ざぁーこ♡",false,true);
            yield return board.DiscardLastTile();
            bool done=false; phase.PlayDrawTransition(ShowNewWall,()=>done=true);
            float limit=Time.realtimeSinceStartup+12; yield return new WaitUntil(()=>done||Time.realtimeSinceStartup>=limit);
            if(!done) {phase.CancelTransitions();ShowNewWall();}
            bool light=false; phase.PlayRoundStartFadeOut(()=>light=true);
            limit=Time.realtimeSinceStartup+12;
            yield return new WaitUntil(()=>light||Time.realtimeSinceStartup>=limit);
            if(!light) phase.CancelTransitions();
            bool handReady=false; StartCoroutine(BuildHand(()=>handReady=true));
            yield return Step("はーいここ注目",true);
            yield return Step("流局すると一旦ゲームが仕切り直しになるよ",true);
            yield return Step("手牌をイチから組み直してもう一度ってワケだねぇ",true);
            yield return Step("まぁ正直手牌選択までは普通のゲームと同じかな",true);
            yield return new WaitUntil(()=>handReady);
            bool dark=false; phase.PlayTransition("対局開始",null,new BettingCompletedInfo {
                LocalBet=2000,EnemyBet=2000,LocalHpBefore=20000,EnemyHpBefore=20000,
                LocalHpAfter=18000,EnemyHpAfter=18000,HasServerHealth=true
            },()=>dark=true,()=>{},false);
            limit=Time.realtimeSinceStartup+12; yield return new WaitUntil(()=>dark||Time.realtimeSinceStartup>=limit); yield return null;
            phase.StopAllCoroutines(); IsPaused=true; StepIndex=10;
            progress.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,764); paused.gameObject.SetActive(true);
            advance.gameObject.SetActive(true); advanced=false; Debug.Log("[TutorialDrawReplay] paused at betting screen");
            if(automatic) yield return new WaitForSecondsRealtime(2);
            else yield return new WaitUntil(()=>advanced);
        }
        private void ShowNewWall()
        {
            board.ShowNewWall();
        }
        private IEnumerator BuildHand(Action completed)
        {
            yield return board.BuildHand(completed);
        }
        private void Update()
        {
            if(noise==null||IsPaused||Time.unscaledTime<noiseNext) return;
            noiseNext=Time.unscaledTime+.12f; noise.Begin();
            for(int i=0;i<18;i++) {
                // 表示用のノイズ。対局や台本で使うRandomの状態は変更しない。
                float x=Mathf.Sin((i+1)*31+Time.unscaledTime*7)*390,y=Mathf.Cos((i+1)*17+Time.unscaledTime*5)*290;
                noise.Box(x,y,x+3,y+1,new Color(1,1,1,.07f));
            }
            noise.End();
        }
        public void Close() { StopAllCoroutines(); if(phase!=null) phase.CancelTransitions(); if(subtitles!=null) subtitles.Hide(); Destroy(gameObject); }
        private void OnDisable() { StopAllCoroutines(); if(phase!=null) phase.CancelTransitions(); if(subtitles!=null) subtitles.Hide(); }
    }
}
