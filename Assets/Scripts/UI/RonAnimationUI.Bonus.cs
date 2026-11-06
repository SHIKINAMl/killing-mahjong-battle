using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.Managers;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    public partial class RonAnimationUI
    {
        public const string TankiBonusEffectId = "ron.bonus_tanki";
        public const string AssaultBonusEffectId = "ron.bonus_assault";
        public const string TankiAssaultEffectId = "ron.bonus_tanki_assault";

        private static void SetSettlementRowAlpha(Transform row, float alpha)
        {
            var group = row.GetComponent<CanvasGroup>();
            if (group == null) group = row.gameObject.AddComponent<CanvasGroup>();
            group.alpha = alpha;
            group.blocksRaycasts = false;
        }

        private static void PrepareBonusSettlement(Transform panel, RonSettlementInfo settlement)
        {
            foreach (string rowName in new[] { "TankiRow", "AssaultRow" })
            {
                var row = panel.Find("Body/" + rowName);
                if (row == null) continue;
                SetSettlementRowAlpha(row, 0f);
                row.gameObject.SetActive(true);
            }

            var result = panel.Find("Body/DeltaRow");
            var myValue = result.GetChild(1).GetComponent<TextMeshProUGUI>();
            var theirValue = result.GetChild(2).GetComponent<TextMeshProUGUI>();
            if (settlement.AssaultApplied && !settlement.IsTankiWait)
            {
                // 通常精算は、勝者から敗者へ移した強襲分を表示上だけ戻して見せる。
                var winnerValue = settlement.LocalWon ? myValue : theirValue;
                var loserValue = settlement.LocalWon ? theirValue : myValue;
                int loserDelta = settlement.LocalWon ? settlement.TheirDelta : settlement.MyDelta;
                ShowSettledDelta(winnerValue, settlement.AssaultBonusDamage);
                ShowSettledDelta(loserValue, loserDelta + settlement.AssaultBonusDamage);
            }
            else
            {
                ShowSettledDelta(myValue, settlement.MyDelta);
                ShowSettledDelta(theirValue, settlement.TheirDelta);
            }
            result.gameObject.SetActive(true);
        }

        private static IEnumerator RevealBonusSettlement(Transform panel, RonSettlementInfo settlement)
        {
            var result = panel.Find("Body/DeltaRow");
            var myValue = result.GetChild(1).GetComponent<TextMeshProUGUI>();
            var theirValue = result.GetChild(2).GetComponent<TextMeshProUGUI>();
            int winnerColumn = settlement.LocalWon ? 1 : 2;
            int loserColumn = settlement.LocalWon ? 2 : 1;

            if (settlement.IsTankiWait)
            {
                var tanki = panel.Find("Body/TankiRow");
                tanki.GetChild(winnerColumn).GetComponent<TextMeshProUGUI>().text = "-";
                tanki.GetChild(loserColumn).GetComponent<TextMeshProUGUI>().text = "×2";
                SetSettlementRowAlpha(tanki, 1f);
                yield return SweepSettlementCells(AccentGold, .36f, (RectTransform)tanki.GetChild(loserColumn));
                if (panel == null) yield break;
            }

            if (settlement.AssaultApplied)
            {
                var assault = panel.Find("Body/AssaultRow");
                SetSettlementRowAlpha(assault, 1f);
                yield return SweepSettlementCells(AccentGold, .46f,
                    (RectTransform)assault.GetChild(winnerColumn), (RectTransform)assault.GetChild(loserColumn));
                if (panel == null) yield break;
                if (!settlement.IsTankiWait)
                {
                    ShowSettledDelta(myValue, settlement.MyDelta);
                    ShowSettledDelta(theirValue, settlement.TheirDelta);
                }
            }

            yield return SweepSettlementCells(PanelInk, .25f, myValue.rectTransform, theirValue.rectTransform);
        }

        private static void ShowSettledDelta(TextMeshProUGUI text, int delta)
        {
            HpNumberStyle.Apply(text, delta);
            if (delta == 0) text.color = PanelFaint;
            text.text = FormatDelta(delta);
        }

        private static IEnumerator SweepSettlementCells(Color tint, float duration, params RectTransform[] cells)
        {
            var highlights = new Image[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                // テキストより背面で、列の幅に沿って下線が走る。
                var highlight = new GameObject("SettlementUnderline", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                highlight.transform.SetParent(cells[i].parent, false);
                highlight.transform.SetSiblingIndex(0);
                highlight.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                highlight.raycastTarget = false;
                var rect = highlight.rectTransform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                highlights[i] = highlight;
            }

            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float progress = t / duration;
                tint.a = 1f - Mathf.SmoothStep(0f, 1f, (progress - .42f) / .58f);
                for (int i = 0; i < cells.Length; i++)
                {
                    if (cells[i] == null) yield break;
                    var cell = cells[i];
                    var rect = highlights[i].rectTransform;
                    rect.localPosition = cell.localPosition - (Vector3)Vector2.Scale(cell.pivot, cell.rect.size);
                    rect.sizeDelta = new Vector2(cell.rect.width * Mathf.SmoothStep(0f, 1f, progress / .42f), 2f);
                    highlights[i].color = tint;
                }
                yield return null;
            }

            foreach (var highlight in highlights)
                if (highlight != null) Destroy(highlight.gameObject);
        }

        // 台本の強襲説明が終わってから呼ぶ。表は残し、HP転送は従来の台本側が行う。
        public IEnumerator ShowTutorialSettlementWithBonus(RonSettlementInfo settlement, int ronTile,
            PlayerInfoUI player, EnemyInfoUI enemy)
        {
            ShowTutorialSettlement(settlement);
            if (settlement == null || _tutorialSettlementPanel == null) yield break;
            if (!settlement.IsTankiWait && !settlement.AssaultApplied) yield break;
            var panel = _tutorialSettlementPanel;
            PrepareBonusSettlement(panel, settlement);
            yield return new WaitForSeconds(.7f);
            if (panel == null || _tutorialSettlementContainer == null) yield break;
            yield return PlaySettlementBonus((RectTransform)_tutorialSettlementContainer.transform,
                settlement, ronTile, player, enemy);
            if (panel == null) yield break;
            yield return RevealBonusSettlement(panel, settlement);
        }

        // コレクションも本編と同じ表・同じ演出を使う。HP・通信・戦績には触れない。
        public IEnumerator PreviewSettlementBonus(RonSettlementInfo sample, int ronTile,
            PlayerInfoUI player, EnemyInfoUI enemy)
        {
            using (var scope = presentations.Begin())
            {
                var stage = scope.Own(new GameObject("SettlementBonusPreview", typeof(RectTransform), typeof(Canvas)));
                stage.transform.SetParent(transform, false);
                var rt = (RectTransform)stage.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
                var canvas = stage.GetComponent<Canvas>();
                canvas.overrideSorting = true; canvas.sortingOrder = UISortingOrders.RonAnimation;
                yield return scope.Run(SettlementRoutine(rt, stage, sample, player, enemy,
                    sample.MyHpBefore, sample.MyHpAfter, sample.TheirHpBefore, sample.TheirHpAfter, ronTile, true));
            }
        }

        private IEnumerator PlaySettlementBonus(RectTransform parent, RonSettlementInfo s, int ronTile,
            PlayerInfoUI player, EnemyInfoUI enemy)
        {
            using (var scope = presentations.Begin())
                yield return scope.Run(SettlementBonusVisuals(scope, parent, s, ronTile, player, enemy));
        }

        private IEnumerator SettlementBonusVisuals(PresentationScope scope, RectTransform parent,
            RonSettlementInfo s, int ronTile, PlayerInfoUI player, EnemyInfoUI enemy)
        {
            var root = scope.Own(new GameObject("SettlementBonus", typeof(RectTransform), typeof(CanvasGroup)));
            root.transform.SetParent(parent, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            var group = root.GetComponent<CanvasGroup>(); group.blocksRaycasts = false;
            var dim = new GameObject("BonusDim", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            dim.transform.SetParent(rt, false); dim.color = new Color(0, 0, 0, .20f); dim.raycastTarget = false;
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.sizeDelta = Vector2.zero;

            if (!s.IsTankiWait)
            {
                var caster = s.LocalWon ? player?.CurrentCharacterData : enemy?.CurrentCharacterData;
                yield return PlayAssaultCutin(rt, group, caster, s.LocalWon);
                yield break;
            }

            float raise = s.AssaultApplied ? 24f : 0f;
            var tile = BonusImage(rt, "WinningTile", tileResourceManager != null && ronTile >= 0
                ? tileResourceManager.GetTileSprite(ronTile) : null, new Vector2(0, 75 + raise), new Vector2(88, 112));
            // 不明な牌IDでは別の牌を捏造しない。見出しはそのまま出せる。
            tile.enabled = tile.sprite != null;
            var title = BonusText(rt, "単騎", new Vector2(0, -8 + raise), 52, PanelInk);
            var note = BonusText(rt, "2倍ボーナス", new Vector2(0, -50 + raise), 25, AccentGold);
            var glow = CreateTankiGlow(tile);
            yield return RevealTankiBonus(group, tile, glow, title, note);
            if (s.AssaultApplied)
            {
                yield return PlayFangBite(rt, tile.rectTransform, glow, title, note);
                yield return new WaitForSeconds(.62f);
            }
            else
            {
                yield return new WaitForSeconds(.80f);
            }
            for (float t = 0; t < .2f; t += Time.deltaTime)
            { group.alpha = 1 - Mathf.Clamp01(t / .2f); yield return null; }
            group.alpha = 0;
        }

        private static IEnumerator PlayAssaultCutin(RectTransform parent, CanvasGroup group,
            CharacterData caster, bool localWon)
        {
            var maskTexture = Resources.Load<Texture2D>("UI/Cutin/assault_slash_mask");
            var frameTexture = Resources.Load<Texture2D>("UI/Cutin/assault_slash_frame");
            var band = new GameObject("AssaultCutin", typeof(RectTransform)).GetComponent<RectTransform>();
            band.SetParent(parent, false);
            var bandPosition = new Vector2(0f, 74f);
            Vector2 bandSize = new Vector2(800f, 400f);

            if (maskTexture != null && frameTexture != null)
            {
                var slash = AssaultSlashLayer(band, "AssaultSlashMask", maskTexture, bandSize);
                slash.color = localWon ? new Color32(23, 49, 93, 255) : new Color32(106, 16, 36, 255);
                slash.gameObject.AddComponent<Mask>();

                var strokes = new GameObject("AssaultSpeedLines", typeof(RectTransform), typeof(PixelShapeGraphic))
                    .GetComponent<PixelShapeGraphic>();
                strokes.transform.SetParent(slash.transform, false);
                strokes.raycastTarget = false;
                Color accent = localWon ? new Color32(58, 109, 177, 255) : new Color32(189, 30, 53, 255);
                strokes.Begin();
                strokes.Line(new Vector2(-408, -120), new Vector2(400, 114), 64f, accent);
                strokes.Line(new Vector2(-403, -136), new Vector2(-190, -74), 5f, PanelInk);
                strokes.Line(new Vector2(238, 67), new Vector2(405, 115), 4f, PanelInk);
                strokes.End();

                var body = caster?.bodySprites?.Find(x => x.id == caster.defaultBodyId)?.sprite ?? caster?.normalSprite;
                var face = caster?.faceSprites?.Find(x => x.id == "キメ顔")?.sprite
                    ?? caster?.faceSprites?.Find(x => x.id == caster.defaultFaceId)?.sprite;
                if (body != null && !caster.showAsPlaceholder)
                {
                    // 各立ち絵の元画像での目の位置。顔差分も同じ座標と倍率で重ねる。
                    bool senior = caster.characterName == "TutorialGirl";
                    var eye = senior ? new Vector2(.59f, .63f) : new Vector2(.49f, .766f);
                    float height = senior ? 1060f : 2100f;
                    var size = new Vector2(height * body.rect.width / body.rect.height, height);
                    var position = new Vector2(6f, 16f) - Vector2.Scale(eye - Vector2.one * .5f, size);
                    var portrait = BonusImage(slash.rectTransform, "AssaultCaster", body, position, size);
                    if (face != null)
                        BonusImage(portrait.rectTransform, "Face", face, Vector2.zero, size);
                }

                AssaultSlashLayer(band, "AssaultSlashFrame", frameTexture, bandSize);
            }

            var shadow = BonusText(band, "強襲", new Vector2(2f, -73f), 66f, Color.black);
            var title = BonusText(band, "強襲", new Vector2(0f, -69f), 66f, PanelInk);
            title.characterSpacing = shadow.characterSpacing = 14f;

            const float Entry = .16f;
            Vector2 start = bandPosition - new Vector2(420f, 118f);
            group.alpha = 0f;
            AudioManager.Instance?.PlayAssaultSE();
            for (float t = 0f; t < Entry; t += Time.deltaTime)
            {
                float progress = t / Entry;
                float brake = 1f - Mathf.Pow(1f - progress, 3f);
                band.anchoredPosition = Vector2.Lerp(start, bandPosition, brake);
                band.localScale = new Vector3(1f, Mathf.Lerp(.65f, 1f, brake), 1f);
                group.alpha = Mathf.Clamp01(progress * 3f);
                yield return null;
            }
            band.anchoredPosition = bandPosition;
            band.localScale = Vector3.one;
            group.alpha = 1f;
            yield return new WaitForSeconds(.80f);

            const float Exit = .15f;
            for (float t = 0f; t < Exit; t += Time.deltaTime)
            {
                float progress = t / Exit;
                band.anchoredPosition = bandPosition + new Vector2(140f, 40f) * (progress * progress);
                band.localScale = new Vector3(1f, Mathf.Lerp(1f, .75f, progress), 1f);
                group.alpha = 1f - progress;
                yield return null;
            }
            group.alpha = 0f;
        }

        private static RawImage AssaultSlashLayer(RectTransform parent, string name, Texture2D texture, Vector2 size)
        {
            var layer = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            layer.transform.SetParent(parent, false);
            layer.rectTransform.sizeDelta = size;
            layer.texture = texture;
            layer.raycastTarget = false;
            return layer;
        }

        private static Image BonusImage(RectTransform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false); image.sprite = sprite; image.preserveAspect = true;
            image.raycastTarget = false; image.rectTransform.sizeDelta = size; image.rectTransform.anchoredPosition = pos;
            if (sprite == null) image.enabled = false;
            return image;
        }

        private static TextMeshProUGUI BonusText(RectTransform parent, string content, Vector2 pos, float size, Color color)
        {
            var text = new GameObject("BonusTitle", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false); text.rectTransform.sizeDelta = new Vector2(540, 80);
            text.rectTransform.anchoredPosition = pos; text.font = GameUiTypography.Display;
            text.fontSize = size; text.text = content; text.color = color;
            text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false; text.outlineColor = Color.black; text.outlineWidth = .22f;
            return text;
        }

        private static RawImage CreateTankiGlow(Image tile)
        {
            var glow = new GameObject("TankiGlow", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            glow.transform.SetParent(tile.transform.parent, false);
            glow.transform.SetSiblingIndex(tile.transform.GetSiblingIndex());
            glow.raycastTarget = false;
            glow.texture = Resources.Load<Texture2D>("UI/TankiBonusGlow");
            glow.enabled = tile.enabled && glow.texture != null;
            glow.rectTransform.sizeDelta = new Vector2(450f, 450f);
            glow.rectTransform.anchoredPosition = tile.rectTransform.anchoredPosition;
            return glow;
        }

        private static IEnumerator RevealTankiBonus(CanvasGroup group, Image tile, RawImage glow,
            TextMeshProUGUI title, TextMeshProUGUI note)
        {
            const float TileFadeIn = .19f;
            const float BloomTime = .28f;
            glow.color = Color.clear;
            title.alpha = note.alpha = 0f;
            note.fontSize = 30f;
            note.rectTransform.anchoredPosition += Vector2.down * 9f;
            group.alpha = 0f;
            tile.color = Color.clear;
            for (float elapsed = 0f; elapsed < TileFadeIn; elapsed += Time.deltaTime)
            {
                float fade = Mathf.SmoothStep(0f, 1f, elapsed / TileFadeIn);
                group.alpha = fade;
                tile.color = new Color(1f, 1f, 1f, fade);
                yield return null;
            }

            group.alpha = 1f;
            tile.color = Color.white;
            AudioManager.Instance?.PlayStinger("se_tanki_bonus", AudioManager.PitchJitter.None);
            for (float elapsed = 0f; elapsed < BloomTime; elapsed += Time.deltaTime)
            {
                // 牌の後ろで小さな光が一度弾け、外側の輪まで広がる。
                float scale = elapsed < .035f ? .62f : elapsed < .070f ? .84f : 1f;
                float alpha = elapsed < .070f ? .88f : Mathf.Lerp(1f, .72f, (elapsed - .070f) / .15f);
                glow.rectTransform.localScale = Vector3.one * scale;
                glow.color = new Color(1f, 1f, 1f, alpha);
                title.alpha = Mathf.SmoothStep(0f, 1f, (elapsed - .06f) / .09f);
                note.alpha = Mathf.SmoothStep(0f, 1f, (elapsed - .13f) / .10f);
                yield return null;
            }

            glow.rectTransform.localScale = Vector3.one;
            glow.color = new Color(1f, 1f, 1f, .72f);
            title.alpha = note.alpha = 1f;
        }

        private static IEnumerator PlayFangBite(RectTransform parent, RectTransform tile, RawImage glow,
            TextMeshProUGUI title, TextMeshProUGUI note)
        {
            const float Entry = .29f;
            const float Retreat = .30f;
            const float ReadyHold = .14f;
            const float WindUp = .25f;
            const float BiteHold = .20f;
            const float TileRelease = .12f;
            Vector2 tilePosition = tile.anchoredPosition;
            Vector3 tileScale = tile.localScale;
            Vector2 pressedPosition = tilePosition + new Vector2(4f, -8f);
            Vector3 pressedScale = Vector3.Scale(tileScale, new Vector3(1.04f, .92f, 1f));
            var jaws = new GameObject("BitingFangs", typeof(RectTransform), typeof(CanvasGroup));
            jaws.transform.SetParent(parent, false);
            var opacity = jaws.GetComponent<CanvasGroup>();
            opacity.blocksRaycasts = false;

            var smearFrames = new[]
            {
                Resources.Load<Texture2D>("UI/AssaultFangsSmearLong"),
                Resources.Load<Texture2D>("UI/AssaultFangsSmearShort")
            };
            var smear = new GameObject("FangSmearFrame", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            smear.transform.SetParent(jaws.transform, false);
            smear.rectTransform.sizeDelta = new Vector2(parent.rect.width * 1.08f, parent.rect.height * .81f);
            smear.rectTransform.anchoredPosition = tilePosition;
            smear.raycastTarget = false;
            smear.enabled = false;

            // 噛みつく瞬間は精算表より広げ、犬歯の先だけを牌の対角に残す。
            const float PaintedBiteWidth = 322f;
            const float PaintedBiteCenterX = -22f;
            const float BiteWidthBoost = 1.34f;
            const float BiteHeightBoost = 1.20f;
            const float ApproachScale = 1.28f;
            float biteWidthScale = PanelWidth / PaintedBiteWidth * BiteWidthBoost;
            var biteScale = new Vector3(biteWidthScale, BiteHeightBoost, 1f);
            var approachScale = new Vector3(ApproachScale, ApproachScale, 1f);
            var teeth = new GameObject("FangTeeth", typeof(RectTransform)).GetComponent<RectTransform>();
            teeth.SetParent(jaws.transform, false);
            teeth.anchoredPosition = tilePosition;
            teeth.localScale = approachScale;
            var images = new RawImage[2];
            var biteTextures = new[]
            {
                Resources.Load<Texture2D>("UI/AssaultFangsBiteUpper"),
                Resources.Load<Texture2D>("UI/AssaultFangsBiteLower")
            };
            // 旧サイズで牌を挟んでいた、上と下の犬歯の先の位置（牌の原点からの差）。
            var upperContact = new Vector2(26f, 1f);
            var lowerContact = new Vector2(-21f, -42f);
            var contactCorrection = new Vector2((1f - BiteWidthBoost) / biteScale.x,
                (1f - BiteHeightBoost) / biteScale.y);
            var bite = new[]
            {
                new Vector2(-14f, 44f) + Vector2.Scale(upperContact, contactCorrection),
                new Vector2(14f, -44f) + Vector2.Scale(lowerContact, contactCorrection),
            };
            Vector2 halfScreen = parent.rect.size * .5f;
            // 左上と右下の画面外から入り、中央を大きく挟む位置で構える。
            var outside = new[]
            {
                (new Vector2(-halfScreen.x - 250f, halfScreen.y + 125f) - tilePosition) / ApproachScale,
                (new Vector2(halfScreen.x + 190f, -halfScreen.y - 125f) - tilePosition) / ApproachScale,
            };
            var ready = new[]
            {
                (new Vector2(-halfScreen.x * .56f, halfScreen.y * .64f) - tilePosition) / ApproachScale,
                (new Vector2(halfScreen.x * .67f, -halfScreen.y * .46f) - tilePosition) / ApproachScale,
            };
            var open = new[] { ready[0] + new Vector2(-28f, 33f), ready[1] + new Vector2(28f, -33f) };
            for (int jaw = 0; jaw < images.Length; jaw++)
            {
                var texture = Resources.Load<Texture2D>(jaw == 0 ? "UI/AssaultFangsUpper" : "UI/AssaultFangsLower");
                images[jaw] = CreateFangImage(teeth, "Fang", texture);
                images[jaw].rectTransform.anchoredPosition = outside[jaw];
            }
            var impact = new GameObject("FangImpact", typeof(RectTransform), typeof(PixelShapeGraphic))
                .GetComponent<PixelShapeGraphic>();
            impact.transform.SetParent(jaws.transform, false);
            impact.raycastTarget = false;
            impact.rectTransform.anchoredPosition = tilePosition;

            for (float elapsed = 0; elapsed < Entry; elapsed += Time.deltaTime)
            {
                float progress = elapsed / Entry;
                float brake = 1f - Mathf.Pow(1f - progress, 3f);
                opacity.alpha = Mathf.Clamp01(progress * 4f);
                for (int jaw = 0; jaw < images.Length; jaw++)
                    images[jaw].rectTransform.anchoredPosition = Vector2.Lerp(outside[jaw], ready[jaw], brake);
                yield return null;
            }
            opacity.alpha = 1f;
            for (int jaw = 0; jaw < images.Length; jaw++)
                images[jaw].rectTransform.anchoredPosition = ready[jaw];
            yield return new WaitForSeconds(ReadyHold);
            for (float elapsed = 0; elapsed < WindUp; elapsed += Time.deltaTime)
            {
                // 開き切るまでを三つの姿勢で見せ、次のフレームで牌へ飛ぶ。
                float progress = elapsed < .05f ? .28f : elapsed < .12f ? .68f : 1f;
                glow.color = new Color(1f, 1f, 1f, Mathf.Lerp(.72f, .43f, progress));
                for (int jaw = 0; jaw < images.Length; jaw++)
                    images[jaw].rectTransform.anchoredPosition = Vector2.Lerp(ready[jaw], open[jaw], progress);
                yield return null;
            }
            teeth.localScale = biteScale;
            var biteOffset = new Vector2(-PaintedBiteCenterX * biteWidthScale, 0f);
            teeth.anchoredPosition = tilePosition + biteOffset;
            for (int jaw = 0; jaw < images.Length; jaw++)
            {
                if (biteTextures[jaw] != null) images[jaw].texture = biteTextures[jaw];
                images[jaw].rectTransform.anchoredPosition = bite[jaw];
            }
            tile.anchoredPosition = pressedPosition;
            tile.localScale = pressedScale;
            title.alpha = note.alpha = 0;
            glow.color = new Color(1f, .83f, .63f, .96f);
            AudioManager.Instance?.PlayStinger("se_fang_bite", AudioManager.PitchJitter.None);

            // 長い残像と短い残像はそれぞれ一枚の絵として見せる。
            for (int frame = 0; frame < smearFrames.Length; frame++)
            {
                bool first = frame == 0;
                smear.texture = smearFrames[frame];
                smear.enabled = smear.texture != null;
                smear.color = new Color(1f, 1f, 1f, first ? 1f : .82f);
                DrawFangImpact(impact, first ? 1f : .42f);
                glow.color = new Color(1f, .83f, .63f, first ? .96f : .34f);
                yield return new WaitForSeconds(first ? .075f : .095f);
            }
            smear.enabled = false;
            impact.gameObject.SetActive(false);
            glow.color = new Color(1f, .83f, .63f, .18f);
            // 残像が消えてから、牙が牌を挟んだ姿をひと呼吸残す。
            yield return new WaitForSeconds(BiteHold);

            title.text = "単騎強襲"; title.color = AccentThem;
            note.text = "2倍ダメージ";
            for (float elapsed = 0; elapsed < Retreat; elapsed += Time.deltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / Retreat);
                float movement = Mathf.SmoothStep(0, 1, progress);
                float release = Mathf.SmoothStep(0, 1, elapsed / TileRelease);
                tile.anchoredPosition = Vector2.Lerp(pressedPosition, tilePosition, release);
                tile.localScale = Vector3.Lerp(pressedScale, tileScale, release);
                teeth.localScale = Vector3.Lerp(biteScale, approachScale, movement);
                teeth.anchoredPosition = tilePosition + Vector2.Lerp(biteOffset, Vector2.zero, movement);
                for (int jaw = 0; jaw < images.Length; jaw++)
                    images[jaw].rectTransform.anchoredPosition = Vector2.Lerp(bite[jaw], outside[jaw], movement);
                opacity.alpha = 1 - progress;
                glow.color = new Color(1f, .83f, .63f, Mathf.Lerp(.18f, .40f, release));
                title.alpha = note.alpha = progress;
                yield return null;
            }
            tile.anchoredPosition = tilePosition;
            tile.localScale = tileScale;
            glow.color = new Color(1f, .83f, .63f, .40f);
            jaws.SetActive(false);
            Destroy(jaws);
            title.alpha = note.alpha = 1;
        }

        private static void DrawFangImpact(PixelShapeGraphic impact, float strength)
        {
            var color = new Color(1f, .78f, .58f, strength);
            impact.Begin();
            impact.Line(new Vector2(-51, 6), new Vector2(-93, -1), 4 * strength, color);
            impact.Line(new Vector2(-48, -17), new Vector2(-75, -38), 3 * strength, color);
            impact.Line(new Vector2(49, 7), new Vector2(95, 15), 4 * strength, color);
            impact.Line(new Vector2(46, 26), new Vector2(70, 49), 3 * strength, color);
            impact.End();
        }

        private static RawImage CreateFangImage(Transform parent, string name, Texture2D texture)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(parent, false);
            image.texture = texture;
            image.enabled = texture != null;
            image.raycastTarget = false;
            // 牙素材を1/5のドットに描き直しても、牌に食い込む位置と表示寸法は元のまま。
            if (texture != null) image.rectTransform.sizeDelta = new Vector2(texture.width, texture.height) * 2.15f;
            image.rectTransform.localRotation = Quaternion.Euler(0, 0, 20);
            return image;
        }
    }
}
