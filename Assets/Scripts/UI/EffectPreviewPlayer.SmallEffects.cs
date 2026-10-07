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
                case "tile.sort":
                    var from = new System.Collections.Generic.Dictionary<RectTransform, Vector2>();
                    var to = new System.Collections.Generic.Dictionary<RectTransform, Vector2>();
                    for (int i = 0; i < rig.handTiles.Count; i++) {
                        from[rig.handTiles[i]] = rig.handTiles[i].anchoredPosition;
                        to[rig.handTiles[i]] = rig.handTiles[rig.handTiles.Count - 1 - i].anchoredPosition;
                    }
                    yield return TileMoveAnimator.AnimatePositions(from, to); break;
                case "ron.cutin": case "ron.cutin_max":
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
                case "ui.button_hover": case "ui.menu_hover":
                    var button = new GameObject("PreviewButton", typeof(RectTransform), typeof(Image), typeof(Button));
                    button.transform.SetParent(rig.surface, false);
                    var rect = button.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(230, 45);
                    button.GetComponent<Image>().color = new Color32(60, 25, 40, 255);
                    Text(button.transform, "ボタンの演出", Vector2.zero, new Vector2(230, 45), 18);
                    var data = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
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
                case "tile.clatter": TileClatterEffect.Attach(rig.surface, 150f); break;
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
                    rig.handTiles[6].gameObject.AddComponent<TileVisual>().SetTile(id, rig.tiles.GetTileSprite(id), rig.tiles); break;
                case "gauge.absorb":
                    var gauge = rig.gameObject.AddComponent<ScoreGaugeUI>();
                    gauge.SetVisible(true); gauge.SetStakes(2000, 2000);
                    yield return new WaitForSeconds(1f); gauge.AbsorbStakesIntoGauge(true, 2000); break;
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
                case "character.blink":
                    var character = rig.enemy.CurrentCharacterData;
                    var blink = character.faceSprites.Find(item => item.id == character.blinkFaceId);
                    if (blink == null || rig.face == null) throw new InvalidOperationException("まばたき素材がありません。");
                    var before = rig.face.sprite;
                    for (int i = 0; i < 3; i++) { rig.enemy.SetFaceExpression(character.blinkFaceId); yield return new WaitForSeconds(.12f); rig.face.sprite = before; yield return new WaitForSeconds(.8f); }
                    break;
                case "character.talk":
                    rig.character.gameObject.AddComponent<TalkBobAnimator>();
                    goto case "dialogue.typewriter";
                case "dialogue.typewriter":
                    rig.dialogue.gameObject.SetActive(true);
                    rig.dialogue.ShowText("これは演出の再生見本です。文字送りとキャラクターの動きを確認できます。"); break;
                case "camera.zoom":
                    var zoom = rig.viewCamera.gameObject.AddComponent<CameraZoomController>();
                    yield return zoom.ZoomToTargetRoutine(rig.character.transform); yield return new WaitForSeconds(1f);
                    yield return zoom.ResetZoomRoutine(); break;
                case "scene.momentum":
                    rig.momentum.ShowMomentum(new System.Collections.Generic.List<int> { 20000, 18000, 24000 },
                        new System.Collections.Generic.List<int> { 20000, 22000, 16000 }); break;
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
    }
}
