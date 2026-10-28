using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.UI.Effects;
using KillingMahjong.Visuals;

namespace KillingMahjong.UI
{
    public sealed partial class EffectPreviewPlayer
    {
        private IEnumerator PlaySmallEffect()
        {
            switch (EffectId)
            {
                case "ui.ability_bell":
                    var bellAudio = KillingMahjong.Managers.AudioManager.Instance;
                    if (bellAudio == null) throw new InvalidOperationException("AudioManagerがありません。");
                    scope.AddCleanup(() => { if (bellAudio != null) bellAudio.StopAbilityBellSE(); });
                    bellAudio.PlayAbilityBellSE();
                    yield return new WaitForSecondsRealtime(2.25f);
                    yield break;
                case "tile.sort":
                    var from = new System.Collections.Generic.Dictionary<RectTransform, Vector2>();
                    var to = new System.Collections.Generic.Dictionary<RectTransform, Vector2>();
                    for (int i = 0; i < rig.handTiles.Count; i++) {
                        from[rig.handTiles[i]] = rig.handTiles[i].anchoredPosition;
                        to[rig.handTiles[i]] = rig.handTiles[rig.handTiles.Count - 1 - i].anchoredPosition;
                    }
                    yield return TileMoveAnimator.AnimatePositions(from, to); break;
                case "ron.cutin": case "ron.cutin_max":
                case RonAuraUI.EnemyEffectId:
                    bool cutinDone = false;
                    var cutin = rig.surface.gameObject.AddComponent<CutinAnimationUI>();
                    cutin.PlayCutin(rig.character.sprite, rig.face.sprite, rig.font, "ロン", () => cutinDone = true,
                        EffectId == "ron.cutin_max" ? 1f : 0f);
                    yield return Until(() => cutinDone); break;
                case "scene.aurora":
                    var light = new GameObject("AuroraSample", typeof(LineRenderer));
                    var line = light.GetComponent<LineRenderer>();
                    line.sharedMaterial = rig.character.sharedMaterial; line.positionCount = 3;
                    line.SetPositions(new[] { new Vector3(-3, 1, 0), new Vector3(0, 2, 0), new Vector3(3, 1, 0) });
                    line.startWidth = line.endWidth = .15f; line.sortingOrder = rig.character.sortingOrder + 1;
                    line.startColor = line.endColor = Color.cyan;
                    light.AddComponent<AuroraLightAnimator>();
                    scope.AddCleanup(() => { if (line != null) Destroy(line.material); }); break;
                case "ui.button_hover": case "ui.menu_hover": case UIFrameCatalog.DefaultId:
                    var button = new GameObject("PreviewButton", typeof(RectTransform), typeof(Image), typeof(Button));
                    button.transform.SetParent(rig.surface, false);
                    var rect = button.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(230, 45);
                    button.GetComponent<Image>().color = new Color32(60, 25, 40, 255);
                    Text(button.transform, "ボタンの演出", Vector2.zero, new Vector2(230, 45), 18);
                    var data = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                    Canvas.ForceUpdateCanvases();
                    data.position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                    if (EffectId == UIFrameCatalog.DefaultId)
                    {
                        var feedback = UIInteractionFeedback.Attach(button);
                        feedback.SetFrameId(UIFrameCatalog.DefaultId);
                        feedback.OnPointerEnter(data); yield return new WaitForSecondsRealtime(1);
                        feedback.OnPointerDown(data); yield return new WaitForSecondsRealtime(.5f);
                        feedback.OnPointerUp(data); feedback.SetSelected(true);
                        yield return new WaitForSecondsRealtime(1);
                        feedback.OnPointerExit(data); yield return new WaitForSecondsRealtime(1);
                        feedback.Clear(true);
                        yield break;
                    }
                    if (EffectId == "ui.button_hover") {
                        var hover = button.AddComponent<UIButtonHoverEffect>(); hover.OnPointerEnter(data);
                        yield return new WaitForSeconds(1f); hover.OnPointerDown(data);
                        yield return new WaitForSeconds(.5f); hover.OnPointerUp(data); hover.OnPointerExit(data);
                    } else {
                        var hover = button.AddComponent<MenuButtonHover>(); hover.OnPointerEnter(data);
                        yield return new WaitForSeconds(1f); hover.OnPointerDown(data);
                        yield return new WaitForSeconds(.5f); hover.OnPointerUp(data); hover.OnPointerExit(data);
                    }
                    break;
                case "screen.flash": ScreenFlash.Play(); break;
                case "screen.pixel_tone":
                    var pixelImage = new GameObject("PixelToneSample", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                    pixelImage.transform.SetParent(rig.surface, false);
                    pixelImage.rectTransform.sizeDelta = new Vector2(640, 480);
                    pixelImage.texture = Resources.Load<Texture2D>("UnusedEndings/RedDefeat/closed");
                    pixelImage.raycastTarget = false;
                    PixelToneUI.Attach(pixelImage); break;
                case "screen.scene_break": ScreenFlash.PlaySceneBreak(); break;
                case "screen.quake": ScreenQuake.Play(12f, 1f); break;
                case "screen.tint": ScreenTint.Set(Color.red, .45f); yield return new WaitForSeconds(2f); ScreenTint.Clear(); break;
                case "screen.flicker": ScreenTint.Flicker(Color.red, 2f); break;
                case "screen.wake":
                    if (rig.blink == null) throw new InvalidOperationException("目覚めの演出が舞台にありません。");
                    rig.blink.gameObject.SetActive(true); rig.blink.PlayWakeUpEffect(); break;
                case "tile.sparkle": TileSparkleEffect.Attach(rig.handTiles[6]).SetContinuous(true); break;
                case "tile.rays": TileRisingRayEffect.Attach(rig.handTiles[6]).SetContinuous(true); break;
                case TileClatterEffect.EffectId:
                    bool clatterReady = false;
                    rig.phase.PlayRoundStartDarken("第１局", onReady: () => clatterReady = true);
                    yield return Until(() => clatterReady);
                    yield return null;
                    var clatterRoot = rig.phase.transform as RectTransform;
                    var clatter = TileClatterEffect.Attach(clatterRoot, 112f);
                    scope.AddCleanup(() => { if (clatter != null) TileClatterEffect.Hide(clatterRoot); });
                    yield return new WaitForSecondsRealtime(8f);
                    yield break;
                case "blood.burst": PixelBloodEffect.Play(rig.surface, new Vector2(0, -160)); break;
                case "blood.transfer": BloodTransferEffect.Play(rig.surface, new Vector2(-220, 40), new Vector2(330, -70), 2f); break;
                case "blood.glow":
                    var blood = new GameObject("BloodSample", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    blood.transform.SetParent(rig.surface, false); blood.color = new Color32(230, 25, 75, 255);
                    blood.rectTransform.sizeDelta = new Vector2(40, 180); blood.rectTransform.anchoredPosition = new Vector2(0, 20);
                    BloodGlowUI.Attach(blood); break;
                case "hp.damage": rig.player.SetHP(12000); rig.enemy.SetHP(15000); break;
                case "hp.heal": rig.player.SetHP(26000); break;
                case "hp.heartbeat": rig.player.SetHP(1000); break;
                case "hp.glitch":
                    var glitch = HpDamageGlitch.PlayPreview();
                    scope.AddCleanup(() => { if (glitch != null) Destroy(glitch.gameObject); }); break;
                case "hp.zoom_self": yield return rig.player.ZoomInRoutine(); yield return new WaitForSeconds(1f); yield return rig.player.ResetZoomRoutine(); break;
                case "hp.zoom_enemy": yield return rig.enemy.ZoomInRoutine(); yield return new WaitForSeconds(1f); yield return rig.enemy.ResetZoomRoutine(); break;
                case "tile.dora":
                    var id = KillingMahjong.Managers.TutorialTiles.Encode(4, true);
                    rig.handTiles[6].GetComponent<TileVisual>().SetTile(id, rig.tiles.GetTileSprite(id), rig.tiles); break;
                case "gauge.absorb":
                    // **舞台（rig）に直接付けない。** ScoreGaugeUI.SetVisible は自分の子を全部
                    // 表示に切り替えるので、舞台に付けると控えの演出（まばたきの黒幕など）まで出て、
                    // 画面が真っ暗になっていた（2026-10-17 のユーザー報告）。専用の入れ物に付ける
                    var gaugeHolder = new GameObject("PreviewScoreGauge", typeof(RectTransform));
                    gaugeHolder.transform.SetParent(rig.transform, false);
                    var gauge = gaugeHolder.AddComponent<ScoreGaugeUI>();
                    gauge.SetVisible(true); gauge.SetStakes(2000, 2000);
                    // 本編では画面の上端にあるが、試写では上端に演出名の見出し（高さ 38）が被さる。
                    // 試写のときだけ見出しの下まで下げる
                    var gaugeBar = gauge.GuideTargetRect;
                    if (gaugeBar != null) gaugeBar.anchoredPosition += new Vector2(0f, -40f);
                    yield return new WaitForSeconds(1.2f);
                    gauge.AbsorbStakesIntoGauge(true, 3000);
                    yield return new WaitForSeconds(1.8f); break;   // 吸い込み 0.45 秒 + ゲージが伸びる 0.6 秒
                case VoltageUI.RevealEffectId:
                    var voltage = VoltageUI.CreatePresentation(rig.surface);
                    scope.AddCleanup(() => { if (voltage != null) voltage.CancelRevealPresentation(); });
                    voltage.PresentFeedback(2, false, 5, 5, 9, 1.3f);
                    yield return voltage.RevealPresentation();
                    yield return new WaitForSecondsRealtime(.8f);
                    break;
                case "voltage.flight":
                    var target = new GameObject("VoltageUI_Self", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    target.transform.SetParent(rig.surface, false); target.color = new Color32(110, 240, 220, 255);
                    target.rectTransform.sizeDelta = new Vector2(22, 160); target.rectTransform.anchoredPosition = new Vector2(-350, 0);
                    if (!VoltageTileFlightEffect.TryPlay(rig.handTiles[6], false, () => { }))
                        throw new InvalidOperationException("ボルテージの吸収先が見つかりません。");
                    break;
                case "character.death": yield return rig.enemy.PlayDeathRoutine(); break;
                case "character.bounce": rig.enemy.PlayBounceAnimation(.6f); break;
                case "character.turn": rig.player.SetTurnGlow(true); rig.enemy.SetTurnGlow(true); break;
                case "character.breathe": rig.character.gameObject.AddComponent<BreathingAnimator>(); break;
                case "character.float": rig.character.gameObject.AddComponent<FloatingAnimator>(); break;
                case "character.beat_dance":
                    // 本編では、手牌を選ぶあいだが弾むだけ、打牌のあいだが笑顔と交互だった
                    rig.enemy.SetBeatDance(BeatDanceAnimator.Mode.Bob); yield return new WaitForSeconds(5f);
                    rig.enemy.SetBeatDance(BeatDanceAnimator.Mode.Swap); yield return new WaitForSeconds(7f);
                    rig.enemy.SetBeatDance(BeatDanceAnimator.Mode.None); break;
                case "character.blink":
                    var character = rig.enemy.CurrentCharacterData;
                    var blink = character.faceSprites.Find(item => item.id == character.blinkFaceId);
                    if (blink == null || rig.face == null) throw new InvalidOperationException("まばたき素材がありません。");
                    var before = rig.face.sprite;
                    for (int i = 0; i < 3; i++) { rig.enemy.SetFaceExpression(character.blinkFaceId); yield return new WaitForSeconds(.12f); rig.face.sprite = before; yield return new WaitForSeconds(.8f); }
                    break;
                case "character.senior_bounce":
                    rig.enemy.SetCharacterSprite(Resources.Load<Sprite>("女の子/tutorialexe"));
                    if (rig.face != null) rig.face.enabled = false;
                    if (rig.character.GetComponent<TalkBobAnimator>() == null)
                        rig.character.gameObject.AddComponent<TalkBobAnimator>();
                    rig.enemy.SetIdleSlimeBounce(true);
                    scope.AddCleanup(() => { if (rig != null && rig.enemy != null) rig.enemy.SetIdleSlimeBounce(false); });
                    yield return new WaitForSeconds(4f);
                    rig.enemy.SetIdleSlimeBounce(false);
                    break;
                case "character.talk":
                    rig.character.gameObject.AddComponent<TalkBobAnimator>();
                    goto case "dialogue.typewriter";
                case "dialogue.typewriter":
                    rig.dialogue.gameObject.SetActive(true);
                    rig.dialogue.ShowText("これは演出の再生見本です。文字送りとキャラクターの動きを確認できます。"); break;
                case "dialogue.log":
                    // ログに数行ためてから開く（2026-10-25 に、部屋に浮かぶ過去の吹き出しにした）
                    rig.dialogue.gameObject.SetActive(true);
                    foreach (var logLine in new[] { "「おっ後輩ちゃんじゃーん」", "「先輩を助けに来たんだな？えらいぞー」", "「まっ長くなっちゃったから早速ルール説明するわ」" })
                    {
                        rig.dialogue.ShowText(logLine);
                        yield return new WaitForSeconds(1.2f);
                    }
                    rig.dialogue.OpenLog();
                    yield return new WaitForSeconds(3f);
                    rig.dialogue.CloseLog(); break;
                case "camera.zoom":
                    var zoom = rig.viewCamera.gameObject.AddComponent<CameraZoomController>();
                    yield return zoom.ZoomToTargetRoutine(rig.character.transform); yield return new WaitForSeconds(1f);
                    yield return zoom.ResetZoomRoutine(); break;
                case "scene.momentum":
                    // 相手の満貫ロン → 流局 → 自分の跳満ロン → 賭け金 → 自分の役満ロン。吹き出しはロンの点だけに出る
                    rig.momentum.ShowMomentum(
                        new System.Collections.Generic.List<int> { 20000, 19000, 13000, 13500, 22000, 21000, 36000 },
                        new System.Collections.Generic.List<int> { 20000, 19000, 25000, 24500, 16000, 15000, 0 },
                        new[] {
                            new Effects.MatchMomentumUI.RonMark { Index = 2, IsLocalWin = false, Rank = "満貫" },
                            new Effects.MatchMomentumUI.RonMark { Index = 4, IsLocalWin = true, Rank = "跳満" },
                            new Effects.MatchMomentumUI.RonMark { Index = 6, IsLocalWin = true, Rank = "役満" } });
                    yield return new WaitUntil(() => rig.momentum == null || !rig.momentum.IsShowing);
                    break;
                case "dora.float":
                    var dora = new GameObject("DoraFloatSample", typeof(SpriteRenderer));
                    dora.transform.position = rig.character.transform.position + Vector3.left * 2;
                    dora.GetComponent<SpriteRenderer>().sprite = rig.tiles.GetTileSprite(Tile(4));
                    dora.AddComponent<DoraFloatAnimator>(); break;
                case "character.hair": HairNeonGlow.Attach(rig.character); break;
                case "scene.atmosphere": SceneAtmosphere.Attach(rig.surface, .5f, .12f, .5f); break;
                case "scene.battle": BattleAtmosphere.EnsureCreated(); BattleAtmosphere.SetVisible(true); break;
                case "scene.room":
                    var girl = new GameObject("RoomGirlSample", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                    girl.transform.SetParent(rig.surface, false); girl.sprite = rig.character.sprite; girl.preserveAspect = true;
                    girl.rectTransform.sizeDelta = new Vector2(240, 350); girl.rectTransform.anchoredPosition = new Vector2(0, 40);
                    rig.character.gameObject.SetActive(false); if (rig.face != null) rig.face.gameObject.SetActive(false);
                    RoomAmbience.Attach(rig.surface, girl.rectTransform, null, null, null, null, null, rig.font); break;
                case "tutorial.highlight": TutorialHighlightUI.Show(rig.handTiles[6], TutorialHighlightUI.Style.Frame); break;
                case TutorialReadingPresentation.EffectId:
                    yield return PreviewReadingBoard();
                    yield break;
                case "tutorial.arrow":
                    var arrow = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
                    arrow.transform.SetParent(rig.surface, false);
                    var image = arrow.GetComponent<Image>(); image.sprite = rig.arrowArt;
                    var pointer = arrow.AddComponent<TutorialArrowUI>(); pointer.ShowAt(rig.handTiles[6]); break;
                case "tutorial.mask":
                    var mask = new GameObject("Mask", typeof(RectTransform), typeof(Canvas)).AddComponent<TutorialMaskUI>();
                    mask.Show(rig.handTiles[6]); break;
                case "tutorial.button_intro":
                    var intro = TutorialButtonIntroUI.Show(rig.handTiles[6]);
                    if (intro == null) throw new InvalidOperationException("導入の対象がありません。");
                    yield return intro.PopIn(); break;
                default: throw new ArgumentException("再生方法が未登録です: " + EffectId);
            }
            yield return new WaitForSeconds(4f);
        }

        private IEnumerator PreviewReadingBoard()
        {
            var root = new GameObject("ReadingPreview", typeof(RectTransform));
            root.transform.SetParent(rig.surface, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
            scrim.transform.SetParent(root.transform, false);
            var scrimRect = scrim.GetComponent<RectTransform>();
            scrimRect.anchorMin = Vector2.zero; scrimRect.anchorMax = Vector2.one;
            scrimRect.offsetMin = scrimRect.offsetMax = Vector2.zero;
            var board = new GameObject("RuleBoard", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            board.transform.SetParent(root.transform, false);
            board.sprite = Resources.Load<Sprite>("Tutorial/案内板_満貫");
            board.preserveAspect = true;
            board.raycastTarget = false;
            board.rectTransform.sizeDelta = new Vector2(722, 470);
            board.rectTransform.anchoredPosition = new Vector2(0, 20);
            var reading = TutorialReadingPresentation.Prepare(root.transform);
            scope.AddCleanup(() => { if (reading != null) reading.Cancel(); if (root != null) Destroy(root); });
            yield return reading.Enter();
            yield return new WaitForSecondsRealtime(2.5f);
            yield return reading.Exit();
        }
    }
}
