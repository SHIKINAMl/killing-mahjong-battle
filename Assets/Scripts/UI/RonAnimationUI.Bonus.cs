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

        private static void SetBonusRowAlpha(Transform panel, float alpha)
        {
            foreach (string name in new[] { "TankiRow", "AssaultRow" })
            {
                var row = panel.Find("Body/" + name);
                if (row == null) continue;
                var group = row.GetComponent<CanvasGroup>();
                if (group == null) group = row.gameObject.AddComponent<CanvasGroup>();
                group.alpha = alpha;
                group.blocksRaycasts = false;
            }
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
            SetBonusRowAlpha(panel, 0f);
            var delta = panel.Find("Body/DeltaRow");
            if (delta != null) delta.gameObject.SetActive(false);
            yield return new WaitForSeconds(.7f);
            if (_tutorialSettlementContainer == null) yield break;
            group.alpha = .2f;
            yield return PlaySettlementBonus((RectTransform)_tutorialSettlementContainer.transform,
                settlement, ronTile, player, enemy);
            if (panel == null) yield break;
            group.alpha = 1f;
            SetBonusRowAlpha(panel, 1f);
            if (delta != null)
            {
                var labels = delta.GetComponentsInChildren<TextMeshProUGUI>(true);
                labels[1].text = FormatDelta(settlement.MyDelta);
                labels[2].text = FormatDelta(settlement.TheirDelta);
                delta.gameObject.SetActive(true);
            }
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
                var rays = new GameObject("TileRays", typeof(RectTransform), typeof(PixelShapeGraphic)).GetComponent<PixelShapeGraphic>();
                rays.transform.SetParent(rt, false); rays.raycastTarget = false;
                rays.rectTransform.sizeDelta = new Vector2(800, 600);
                var tile = BonusImage(rt, "WinningTile", tileResourceManager != null && ronTile >= 0
                    ? tileResourceManager.GetTileSprite(ronTile) : null, new Vector2(0, 75), new Vector2(88, 112));
                // 不明な牌IDでは別の牌を捏造しない。見出しはそのまま出せる。
                tile.enabled = tile.sprite != null;
                var title = BonusText(rt, "単騎", new Vector2(0, -8), 52, PanelInk);
                var note = BonusText(rt, "2倍ボーナス", new Vector2(0, -50), 25, AccentGold);
                group.alpha = 0;
                for (float t = 0; t < .7f; t += Time.deltaTime)
                {
                    group.alpha = Mathf.Clamp01(t / .2f);
                    DrawBonusRays(rays, t); yield return null;
                }
                group.alpha = 1;
                if (s.AssaultApplied)
                {
                    var jaws = new GameObject("BitingFangs", typeof(RectTransform), typeof(PixelShapeGraphic)).GetComponent<PixelShapeGraphic>();
                    jaws.transform.SetParent(rt, false); jaws.raycastTarget = false;
                    jaws.rectTransform.sizeDelta = new Vector2(800, 600);
                    // 奥から上下の歯列が迫り、単騎の牌を噛む。
                    for (float t = 0; t < .32f; t += Time.deltaTime)
                    { DrawBonusFangs(jaws, Mathf.Clamp01(t / .32f), 0); yield return null; }
                    DrawBonusFangs(jaws, 1, 0);
                    AudioManager.Instance?.PlayAssaultSE();
                    title.alpha = note.alpha = 0;
                    yield return new WaitForSeconds(.10f);
                    title.text = "単騎強襲"; title.color = AccentThem;
                    note.text = "2倍ダメージ";
                    for (float t = 0; t < .2f; t += Time.deltaTime)
                    {
                        float p = Mathf.Clamp01(t / .2f);
                        DrawBonusFangs(jaws, 1, p * 540);
                        title.alpha = note.alpha = p; yield return null;
                    }
                    jaws.gameObject.SetActive(false); title.alpha = note.alpha = 1;
                    yield return new WaitForSeconds(.65f);
                }
                else yield return new WaitForSeconds(.45f);
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

        // 既存ドットUIと同じ2pxの段。歯の輪郭を黒、面を生成りで描く。
        private static void DrawBonusFangs(PixelShapeGraphic g, float close, float rise)
        {
            g.Begin();
            float gap = Mathf.Lerp(300, 42, close);
            for (int side = -1; side <= 1; side += 2)
            for (int k = -4; k <= 4; k++)
            {
                float x = k * 58;
                float length = Mathf.Abs(k) == 3 ? 118 : 76;
                float baseY = 70 + side * (gap + 45 + Mathf.Abs(k) * 6) + rise;
                for (float step = 0; step < length; step += 4)
                {
                    float half = Mathf.Round(Mathf.Lerp(27, 3, step / length) / 2) * 2;
                    float y = baseY - side * step;
                    g.Box(x - half - 3, y - 3, x + half + 3, y + 5, Color.black);
                }
                for (float step = 4; step < length - 4; step += 4)
                {
                    float half = Mathf.Round(Mathf.Lerp(24, 1, step / length) / 2) * 2;
                    float y = baseY - side * step;
                    g.Box(x - half, y - 1, x + half, y + 4, PanelInk);
                }
            }
            g.End();
        }
    }
}
