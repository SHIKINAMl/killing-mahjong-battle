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

        private static void PrepareBonusSettlement(Transform panel)
        {
            foreach (string rowName in new[] { "TankiRow", "AssaultRow", "DeltaRow" })
            {
                var row = panel.Find("Body/" + rowName);
                if (row == null) continue;
                SetSettlementRowAlpha(row, 0f);
                row.gameObject.SetActive(true);
            }
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
            }

            ShowSettledDelta(myValue, settlement.MyDelta);
            ShowSettledDelta(theirValue, settlement.TheirDelta);
            SetSettlementRowAlpha(result, 1f);
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
            var group = panel.GetComponent<CanvasGroup>();
            PrepareBonusSettlement(panel);
            yield return new WaitForSeconds(.7f);
            if (panel == null || _tutorialSettlementContainer == null) yield break;
            group.alpha = .2f;
            yield return PlaySettlementBonus((RectTransform)_tutorialSettlementContainer.transform,
                settlement, ronTile, player, enemy);
            if (panel == null) yield break;
            group.alpha = 1f;
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
            dim.transform.SetParent(rt, false); dim.color = new Color(0, 0, 0, .45f); dim.raycastTarget = false;
            dim.rectTransform.anchorMin = Vector2.zero; dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.sizeDelta = Vector2.zero;

            if (!s.IsTankiWait)
            {
                // 発動者本人の既存立ち絵と顔を帯の中へ重ねる。
                var data = s.LocalWon ? player != null ? player.CurrentCharacterData : null
                    : enemy != null ? enemy.CurrentCharacterData : null;
                var band = new GameObject("AssaultBand", typeof(RectTransform));
                band.transform.SetParent(rt, false);
                var bandRt = (RectTransform)band.transform;
                bandRt.anchoredPosition = new Vector2(0, 40);
                var maskRt = CutinPixelBackgroundUI.Attach(bandRt,
                    s.LocalWon ? "skill_band_blue" : "skill_band_red");
                if (maskRt == null) maskRt = bandRt;
                if (data != null)
                {
                    var body = data.bodySprites?.Find(x => x.id == data.defaultBodyId);
                    var portrait = BonusImage(maskRt, "Caster", body != null ? body.sprite : data.normalSprite,
                        new Vector2(-145, -185), new Vector2(720, 720));
                    var face = data.faceSprites?.Find(x => x.id == data.defaultFaceId);
                    if (face != null) BonusImage(portrait.rectTransform, "Face", face.sprite, Vector2.zero, new Vector2(720, 720));
                }
                BonusText(rt, "強襲", new Vector2(160, 60), 64, PanelInk);
                group.alpha = 0f;
                for (float t = 0; t < .16f; t += Time.deltaTime)
                {
                    float p = Mathf.Clamp01(t / .16f);
                    rt.anchoredPosition = new Vector2(Mathf.Round(Mathf.Lerp(-120, 0, p) / 2) * 2, 0);
                    group.alpha = p; yield return null;
                }
                rt.anchoredPosition = Vector2.zero; group.alpha = 1;
                AudioManager.Instance?.PlayAssaultSE();
                yield return new WaitForSeconds(.65f);
            }
            else
            {
                var tile = BonusImage(rt, "WinningTile", tileResourceManager != null && ronTile >= 0
                    ? tileResourceManager.GetTileSprite(ronTile) : null, new Vector2(0, 75), new Vector2(88, 112));
                // 不明な牌IDでは別の牌を捏造しない。見出しはそのまま出せる。
                tile.enabled = tile.sprite != null;
                var title = BonusText(rt, "単騎", new Vector2(0, -8), 52, PanelInk);
                var note = BonusText(rt, "2倍ボーナス", new Vector2(0, -50), 25, AccentGold);
                if (s.AssaultApplied)
                {
                    var rays = new GameObject("TileRays", typeof(RectTransform), typeof(PixelShapeGraphic)).GetComponent<PixelShapeGraphic>();
                    rays.transform.SetParent(rt, false); rays.raycastTarget = false;
                    rays.rectTransform.sizeDelta = new Vector2(800, 600);
                    rays.transform.SetSiblingIndex(tile.transform.GetSiblingIndex());
                    group.alpha = 0;
                    for (float t = 0; t < .7f; t += Time.deltaTime)
                    {
                        group.alpha = Mathf.Clamp01(t / .2f);
                        DrawBonusRays(rays, t); yield return null;
                    }
                    group.alpha = 1;
                    yield return PlayFangBite(rt, tile.rectTransform, title, note);
                    yield return new WaitForSeconds(.65f);
                }
                else yield return PlayTankiBonus(group, tile, title, note);
            }
            for (float t = 0; t < .2f; t += Time.deltaTime)
            { group.alpha = 1 - Mathf.Clamp01(t / .2f); yield return null; }
            group.alpha = 0;
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

        private static void DrawBonusRays(PixelShapeGraphic g, float time)
        {
            g.Begin();
            var center = new Vector2(0, 75);
            for (int k = 0; k < 12; k++)
            {
                float a = k * Mathf.PI / 6;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float radius = 110 + (k % 2) * 30 + Mathf.Min(time * 35, 20);
                g.Line(center + dir * 55, center + dir * radius, k % 2 == 0 ? 6 : 3, new Color(1, .8f, .2f, .65f));
            }
            g.End();
        }

        private static IEnumerator PlayTankiBonus(CanvasGroup group, Image tile,
            TextMeshProUGUI title, TextMeshProUGUI note)
        {
            const float TileFadeIn = .24f;
            const float BloomTime = .30f;
            var glow = new GameObject("TankiGlow", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            glow.transform.SetParent(tile.transform.parent, false);
            glow.transform.SetSiblingIndex(tile.transform.GetSiblingIndex());
            glow.raycastTarget = false;
            glow.texture = Resources.Load<Texture2D>("UI/TankiBonusGlow");
            glow.enabled = tile.enabled && glow.texture != null;
            glow.rectTransform.sizeDelta = new Vector2(450f, 450f);
            glow.rectTransform.anchoredPosition = tile.rectTransform.anchoredPosition;
            glow.color = Color.clear;

            title.alpha = note.alpha = 0f;
            note.fontSize = 30f;
            note.rectTransform.anchoredPosition = new Vector2(0f, -59f);
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
                float bloom = Mathf.SmoothStep(0f, 1f, elapsed / .16f);
                float settle = Mathf.SmoothStep(0f, 1f, (elapsed - .16f) / .14f);
                glow.color = new Color(1f, 1f, 1f, bloom * Mathf.Lerp(1f, .78f, settle));
                glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(.56f, 1f, bloom);
                title.alpha = Mathf.SmoothStep(0f, 1f, (elapsed - .04f) / .12f);
                note.alpha = Mathf.SmoothStep(0f, 1f, (elapsed - .12f) / .16f);
                yield return null;
            }

            glow.color = new Color(1f, 1f, 1f, .78f);
            title.alpha = note.alpha = 1f;
            yield return new WaitForSeconds(.58f);
        }

        private static IEnumerator PlayFangBite(RectTransform parent, RectTransform tile,
            TextMeshProUGUI title, TextMeshProUGUI note)
        {
            const float FadeIn = .36f;
            const float Retreat = .30f;
            const float ReadyHold = .05f;
            const float WindUp = .15f;
            const float SmearHold = .10f;
            const float BiteHold = .10f;
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
            smear.rectTransform.sizeDelta = new Vector2(PanelWidth, PanelWidth * .75f);
            smear.rectTransform.anchoredPosition = tilePosition;
            smear.raycastTarget = false;
            smear.enabled = false;

            // 噛みついた絵の透過余白を除いた横幅と中心。精算表と同じ幅に合わせる。
            const float PaintedBiteWidth = 322f;
            const float PaintedBiteCenterX = -22f;
            float biteWidthScale = PanelWidth / PaintedBiteWidth;
            var teeth = new GameObject("FangTeeth", typeof(RectTransform)).GetComponent<RectTransform>();
            teeth.SetParent(jaws.transform, false);
            teeth.anchoredPosition = tilePosition;
            var images = new RawImage[2];
            var biteTextures = new[]
            {
                Resources.Load<Texture2D>("UI/AssaultFangsBiteUpper"),
                Resources.Load<Texture2D>("UI/AssaultFangsBiteLower")
            };
            var bite = new[] { new Vector2(-14, 44), new Vector2(14, -44) };
            var ready = new[] { bite[0] + new Vector2(-133, 82), bite[1] + new Vector2(133, -90) };
            var open = new[] { ready[0] + new Vector2(-12, 36), ready[1] + new Vector2(12, -36) };
            for (int jaw = 0; jaw < images.Length; jaw++)
            {
                var texture = Resources.Load<Texture2D>(jaw == 0 ? "UI/AssaultFangsUpper" : "UI/AssaultFangsLower");
                images[jaw] = CreateFangImage(teeth, "Fang", texture);
                images[jaw].rectTransform.anchoredPosition = ready[jaw];
            }
            var impact = new GameObject("FangImpact", typeof(RectTransform), typeof(PixelShapeGraphic))
                .GetComponent<PixelShapeGraphic>();
            impact.transform.SetParent(jaws.transform, false);
            impact.raycastTarget = false;
            impact.rectTransform.anchoredPosition = tilePosition;

            for (float elapsed = 0; elapsed < FadeIn; elapsed += Time.deltaTime)
            {
                opacity.alpha = Mathf.SmoothStep(0, 1, elapsed / FadeIn);
                yield return null;
            }
            opacity.alpha = 1;
            yield return new WaitForSeconds(ReadyHold);
            for (float elapsed = 0; elapsed < WindUp; elapsed += Time.deltaTime)
            {
                float progress = Mathf.SmoothStep(0, 1, (elapsed + Time.deltaTime) / WindUp);
                for (int jaw = 0; jaw < images.Length; jaw++)
                    images[jaw].rectTransform.anchoredPosition = Vector2.Lerp(ready[jaw], open[jaw], progress);
                yield return null;
            }
            teeth.localScale = new Vector3(biteWidthScale, 1f, 1f);
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
            AudioManager.Instance?.PlayStinger("se_fang_bite", AudioManager.PitchJitter.None);

            // 着弾位置は固定し、各残像を通常再生でも見分けられる長さだけ残す。
            for (int frame = 0; frame < smearFrames.Length; frame++)
            {
                smear.texture = smearFrames[frame];
                smear.enabled = smear.texture != null;
                DrawFangImpact(impact, frame == 0 ? 1f : .5f);
                bool fading = frame == smearFrames.Length - 1;
                for (float elapsed = 0; elapsed < SmearHold; elapsed += Time.deltaTime)
                {
                    smear.color = new Color(1f, 1f, 1f, fading ? 1f - elapsed / SmearHold : 1f);
                    yield return null;
                }
            }
            smear.enabled = false;
            impact.gameObject.SetActive(false);
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
                teeth.localScale = new Vector3(Mathf.Lerp(biteWidthScale, 1f, movement), 1f, 1f);
                teeth.anchoredPosition = tilePosition + Vector2.Lerp(biteOffset, Vector2.zero, movement);
                for (int jaw = 0; jaw < images.Length; jaw++)
                    images[jaw].rectTransform.anchoredPosition = Vector2.Lerp(bite[jaw], ready[jaw], movement);
                opacity.alpha = 1 - progress;
                title.alpha = note.alpha = progress;
                yield return null;
            }
            tile.anchoredPosition = tilePosition;
            tile.localScale = tileScale;
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
            if (texture != null) image.rectTransform.sizeDelta = new Vector2(texture.width, texture.height) * .43f;
            image.rectTransform.localRotation = Quaternion.Euler(0, 0, 20);
            return image;
        }
    }
}
