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
    public sealed partial class TutorialDrawReplayUI : MonoBehaviour
    {
        public const string EffectId = "tutorial.draw_replay";
        public int StepIndex { get; private set; }
        public bool IsPaused { get; private set; }
        public string NormalText => normal != null ? normal.text : "";
        private RectTransform viewport, progress;
        private TextMeshProUGUI normal, marker, paused, turnCounter;
        private Material ownedMaterial;
        private Button advance;
        private TutorialSubtitleUI subtitles;
        private PhaseTransitionUI phase;
        private TileResourceManager tiles;
        private PixelShapeGraphic noise;
        private TutorialReplayBoard board;
        private bool advanced, automatic;
        private float noiseNext;
        private Image girlBody, girlFace;
        private bool tauntBobbing;
        private float tauntStarted;
        private static readonly Vector2 CharacterPosition = new Vector2(0, 15);

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
            Art("Face","女の子/通常顔",new Vector2(600,600),new Vector2(0,15));
            girlBody = viewport.Find("Girl").GetComponent<Image>();
            girlFace = viewport.Find("Face").GetComponent<Image>();
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
            normal = CloneSceneDialogue();
            if (normal == null)
            {
                normal = Label("ReplayNormalText",viewport,new Vector2(285,92),new Vector2(155,58),15);
                normal.alignment=TextAlignmentOptions.TopLeft;
            }
            marker=Label("ReplayNormalMarker",transform,new Vector2(24,24),new Vector2(295,30),20);
            // ▼は本編のセリフと同じ色（以前は赤で、字幕寄りの見た目になっていた）
            marker.text="▼"; marker.color=normal.color; marker.gameObject.SetActive(false);
            CloneSceneTurnCounter();
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
        /// <summary>
        /// 動画の中の通常セリフは、本編のセリフ欄（<see cref="DialogueUI"/> の吹き出し）を写して使う
        /// （2026-10-23 プランナー指摘「セリフUIの形式が字幕表示に引っ張られてる」）。
        /// 以前は自前の白い文字だけで、LOG の印も無く、字幕と見分けがつきにくかった。
        /// 写すのは見た目だけ。押せる部品と動きは外す。本編の欄が見つからなければ null（自前の文字に戻す）。
        /// </summary>
        private TextMeshProUGUI CloneSceneDialogue()
        {
            var dialogue = FindFirstObjectByType<DialogueUI>(FindObjectsInactive.Include);
            var panel = dialogue != null ? dialogue.PanelForCopy : null;
            var sourceText = dialogue != null ? dialogue.TextForCopy : null;
            if (panel == null || sourceText == null) return null;
            int textIndex = IndexPath(panel.transform, sourceText.transform);
            if (textIndex < 0) return null;
            var copy = Instantiate(panel, viewport, true);
            copy.name = "ReplayDialoguePanel";
            copy.SetActive(true);
            foreach (var m in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(m is Graphic) && !(m is BaseMeshEffect) && !(m is LayoutElement) && !(m is TMP_SubMeshUI)) Destroy(m);
            foreach (var g in copy.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            var text = FindByIndexPath(copy.transform, textIndex);
            var t = text != null ? text.GetComponent<TextMeshProUGUI>() : null;
            if (t == null) return null;
            t.text = "";
            t.maxVisibleCharacters = int.MaxValue;
            // 本編のセリフと同じ黒い影（DialogueUI.ApplyShadow と同じ値）。写しの元が一度も喋っていないと
            // 影がまだ付いていないので、ここで付ける。材質は写し専用にして本編へ伝えない
            var mat = new Material(t.fontSharedMaterial);
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", Color.black);
            mat.SetFloat("_UnderlayOffsetX", DialogueUI.UnderlayOffsetX);
            mat.SetFloat("_UnderlayOffsetY", DialogueUI.UnderlayOffsetY);
            mat.SetFloat("_UnderlayDilate", DialogueUI.UnderlayDilate);
            mat.SetFloat("_UnderlaySoftness", DialogueUI.UnderlaySoftness);
            t.fontSharedMaterial = mat;
            ownedMaterial = mat;
            t.UpdateMeshPadding();
            return t;
        }

        /// <summary>
        /// 打牌数（「後：十七打目」）を動画の中にも出す（2026-10-23 プランナー指摘「動画空間の時、打牌数をカウントするUIを表示するはず」）。
        /// 本編の河の打牌数の文字を写す。流局の話なので、両者17打ち終える最後の1打の場面として「十七打目」にする
        /// </summary>
        private void CloneSceneTurnCounter()
        {
            RectTransform source = null;
            foreach (var river in FindObjectsByType<RiverUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (river != null && river.TurnTextRect != null) { source = river.TurnTextRect; break; }
            if (source == null)
            {
                // 河が無い場面（演出コレクションの試写など）では、本編の位置と大きさに合わせた文字で出す
                turnCounter = Label("ReplayTurnCounter", viewport, new Vector2(300, 48), new Vector2(-240, 170), 32);
                turnCounter.alignment = TextAlignmentOptions.Left;
                turnCounter.color = new Color32(240, 232, 214, 255);
                turnCounter.outlineWidth = .2f;
                turnCounter.outlineColor = new Color32(30, 18, 14, 255);
                SetTurnCounter(17);
                return;
            }
            var copy = Instantiate(source.gameObject, viewport, true);
            copy.name = "ReplayTurnCounter";
            foreach (var m in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(m is Graphic) && !(m is BaseMeshEffect) && !(m is TMP_SubMeshUI)) Destroy(m);
            turnCounter = copy.GetComponent<TextMeshProUGUI>();
            if (turnCounter == null) { Destroy(copy); return; }
            turnCounter.raycastTarget = false;
            SetTurnCounter(17);
            copy.SetActive(true);
        }

        /// <summary>動画の中の打牌数を出す。0 なら隠す。</summary>
        private void SetTurnCounter(int turn)
        {
            if (turnCounter == null) return;
            turnCounter.gameObject.SetActive(turn > 0);
            if (turn > 0) turnCounter.text = "後：" + KanjiNumber(turn) + "打目";
        }

        private static string KanjiNumber(int n)
        {
            string[] digits = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九" };
            if (n < 10) return digits[n];
            return (n / 10 == 1 ? "" : digits[n / 10]) + "十" + digits[n % 10];
        }

        /// <summary>子をたどる道筋を、兄弟の番号を桁に詰めた数で返す（深さ3まで）。見つからなければ -1。</summary>
        private static int IndexPath(Transform root, Transform target)
        {
            var path = new System.Collections.Generic.List<int>();
            for (var t = target; t != root; t = t.parent)
            {
                if (t == null) return -1;
                path.Insert(0, t.GetSiblingIndex());
            }
            if (path.Count > 3) return -1;
            int code = 0;
            foreach (int i in path) code = code * 100 + i + 1;
            return code;
        }

        private static Transform FindByIndexPath(Transform root, int code)
        {
            var path = new System.Collections.Generic.List<int>();
            for (; code > 0; code /= 100) path.Insert(0, code % 100 - 1);
            var t = root;
            foreach (int i in path) { if (i < 0 || i >= t.childCount) return null; t = t.GetChild(i); }
            return t;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
            rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero;
            rect.localScale=Vector3.one; rect.localRotation=Quaternion.identity;
        }
        // シークバーは、最後のセリフでちょうど半分まで進んだところで終わる
        // （2026-10-15 のフロー図「動画のシークバーはまだ半分くらい残して終了」）。動画は途中で閉じられる
        private const int StepCount = 18;
        private int stepCount = StepCount;
        private float progressStart, progressEnd = .5f;
        private IEnumerator Step(string text,bool subtitle,bool loud=false)
        {
            StepIndex++; progress.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                764f * Mathf.Lerp(progressStart, progressEnd, Mathf.Min(StepIndex, stepCount) / (float)stepCount)); advanced=false;
            if(subtitle) { marker.gameObject.SetActive(false); advance.gameObject.SetActive(false); subtitles.Show("先輩",text,()=>advanced=true); }
            else {
                string formatted=TutorialEmphasis.Apply(text);
                if(loud) formatted=(TutorialEmphasis.UsesBoldFont?"<font=\"PixelMplus10_TutorialBold\">":"<b>")+"<size=+5>"+text+"</size>"+(TutorialEmphasis.UsesBoldFont?"</font>":"</b>");
                normal.text="「"+formatted+"」"; marker.gameObject.SetActive(true); advance.gameObject.SetActive(true);
            }
            Debug.Log("[TutorialDrawReplay] Step "+StepIndex+(subtitle?" subtitle: ":" normal: ")+text);
            if(automatic) {
                yield return new WaitForSecondsRealtime(loud ? 3.6f : 1.8f);
                if(subtitle) subtitles.CompleteLine(); else advanced=true;
            }
            else yield return new WaitUntil(()=>advanced);
            marker.gameObject.SetActive(false); advance.gameObject.SetActive(false);
        }
        public IEnumerator Play(bool autoAdvance=false)
        {
            tauntBobbing = false;
            SetCharacter("通常時身体", "通常顔");
            SetCharacterPosition(CharacterPosition);
            automatic=autoAdvance; IsPaused=false; StepIndex=0; transform.localScale=Vector3.one*.4f;
            stepCount=StepCount; progressStart=0f; progressEnd=.5f;
            for(float t=0;t<1;t+=Time.unscaledDeltaTime) { transform.localScale=Vector3.one*Mathf.SmoothStep(.4f,1,t); yield return null; }
            transform.localScale=Vector3.one;
            // PhaseTransitionUI.Start が通常対局の層へ戻すので、初期化後に動画内の層を適用する。
            foreach(var cv in phase.GetComponentsInChildren<Canvas>(true)) {
                cv.enabled=true; cv.overrideSorting=true; cv.sortingOrder=91;
            }
            yield return Step("おっ立ち上がったね",true);
            yield return Step("今からアタシが流局するから見ててよ",true);
            SetCharacter("ピース身体", "どや顔");
            yield return Step("では惨めに最後の牌を出しなさい？",false);
            SetCharacter("通常時身体", "笑顔");
            tauntStarted = Time.unscaledTime; tauntBobbing = true;
            yield return Step("ざぁーこ♡",false,true);
            tauntBobbing = false;
            Vector2 from = girlBody.rectTransform.anchoredPosition;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime * 5)
            {
                SetCharacterPosition(Vector2.Lerp(from, CharacterPosition, Mathf.SmoothStep(0, 1, t)));
                yield return null;
            }
            SetCharacterPosition(CharacterPosition);
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
            // 賭け金が出てくる黒い画面で止める。ここから先は 2026-10-15 のフロー図で足された、
            // 流局すると賭け金を自分で決められない（前と同じ額を強制的に賭けさせられる）話
            phase.StopAllCoroutines(); IsPaused=true; paused.gameObject.SetActive(true);
            Debug.Log("[TutorialDrawReplay] paused at betting screen");
            yield return Step("はーいここストップ！",true);
            yield return Step("今賭け金を決めるとこがスキップされたのがわかったかな？",true);
            yield return Step("普通のゲームだと対局前に自分で賭け金を決めるよね",true);
            yield return Step("でも流局すると前に賭けた額と同じ額を強制的に賭けさせられるんだ！",true);
            yield return Step("この画面だと……",true);
            // プレイヤー側の賭け金にハイライト（動画の中なので、動画の画面の上に枠を出す）
            var betFrame=ShowFrame(phase.PlayerBetRect);
            // X は動画で先輩が賭けている額（BettingCompletedInfo.LocalBet）。前に X、今回も X で合計 2X
            yield return Step($"アタシは前に{ReplayBet}賭けてたから今回の流局で{TutorialEmphasis.Open}合計{ReplayBet*2}賭ける{TutorialEmphasis.Close}って感じかな",true);
            if(betFrame!=null) Destroy(betFrame);
            yield return Step("ちなみに流局が起こる回数に制限はないから…このまま連続で流局すれば",true);
            yield return Step($"アタシはまた次の流局で{ReplayBet}賭けてかなきゃいけないってワケ",true);
            yield return Step("まぁ誰かがロンして流局を止めれば次からはまた自由に賭けれるから",true);
            yield return Step("そんな合計が元々賭けた額の何倍にも積みあがったりはしないけど",true);
            // スマホを引っ込めて動画空間終了（シークバーは半分残したまま）
            for(float t=0;t<1;t+=Time.unscaledDeltaTime*2.5f) { transform.localScale=Vector3.one*Mathf.SmoothStep(1,.4f,t); yield return null; }
        }

        /// <summary>動画で先輩が賭けている額。上の BettingCompletedInfo と同じ値。</summary>
        private const int ReplayBet=2000;

        /// <summary>動画の画面の上に、指定した文字の周りを囲む金色の枠を出す。消すのは呼んだ側。</summary>
        private GameObject ShowFrame(RectTransform target)
        {
            if(target==null) return null;
            var layer=paused.transform.parent as RectTransform;
            var corners=new Vector3[4]; target.GetWorldCorners(corners);
            Vector2 min=layer.InverseTransformPoint(corners[0]), max=layer.InverseTransformPoint(corners[2]);
            // 文字の枠は文字より広いことがあるので、実際に描いた範囲で囲む
            var tmp=target.GetComponent<TextMeshProUGUI>();
            if(tmp!=null) { tmp.ForceMeshUpdate(); var b=tmp.textBounds;
                min=layer.InverseTransformPoint(target.TransformPoint(b.min)); max=layer.InverseTransformPoint(target.TransformPoint(b.max)); }
            const float pad=6f, w=3f; min-=Vector2.one*pad; max+=Vector2.one*pad;
            var root=new GameObject("ReplayBetFrame",typeof(RectTransform)); root.transform.SetParent(layer,false);
            Color gold=new Color32(255,215,0,255); Vector2 size=max-min, mid=(min+max)*.5f;
            Box("Top",root.transform,new Vector2(size.x,w),new Vector2(mid.x,max.y),gold);
            Box("Bottom",root.transform,new Vector2(size.x,w),new Vector2(mid.x,min.y),gold);
            Box("Left",root.transform,new Vector2(w,size.y),new Vector2(min.x,mid.y),gold);
            Box("Right",root.transform,new Vector2(w,size.y),new Vector2(max.x,mid.y),gold);
            return root;
        }
        private void ShowNewWall()
        {
            board.ShowNewWall();
            SetTurnCounter(0); // 配り直したので、手牌選択のあいだ打牌数は出さない（本編と同じ）
        }
        private IEnumerator BuildHand(Action completed)
        {
            yield return board.BuildHand(completed);
        }
        private void Update()
        {
            if (tauntBobbing)
            {
                float elapsed = Time.unscaledTime - tauntStarted;
                // 3秒かけて一度だけ上がり、元の位置へ戻る。セリフ待ちが長くても繰り返さない。
                float progress = Mathf.Clamp01(elapsed / 3f);
                float offset = 6f * Mathf.Sin(Mathf.PI * Mathf.SmoothStep(0f, 1f, progress));
                SetCharacterPosition(CharacterPosition + Vector2.up * Mathf.Round(offset));
                if (progress >= 1f) { tauntBobbing = false; SetCharacterPosition(CharacterPosition); }
            }
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
        private void SetCharacter(string body, string face)
        {
            girlBody.sprite = Resources.Load<Sprite>("女の子/" + body);
            girlFace.sprite = Resources.Load<Sprite>("女の子/" + face);
        }
        private void SetCharacterPosition(Vector2 position)
        {
            if (girlBody != null) girlBody.rectTransform.anchoredPosition = position;
            if (girlFace != null) girlFace.rectTransform.anchoredPosition = position;
        }
        private void OnDestroy() { if (ownedMaterial != null) Destroy(ownedMaterial); }
        private void OnDisable() { tauntBobbing = false; SetCharacterPosition(CharacterPosition); StopAllCoroutines(); if(phase!=null) phase.CancelTransitions(); if(subtitles!=null) subtitles.Hide(); }
    }
}
