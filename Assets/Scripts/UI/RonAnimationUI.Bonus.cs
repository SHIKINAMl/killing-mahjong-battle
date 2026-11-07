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

        // 単騎・強襲は表に行を足さない（2026-11-08 ユーザー指示「素点、倍率、ボルテージの3つの枠のみで。
        // 単騎、強襲などの演出があったら、再計算する感じの演出で」）。
        // 表は最初、単騎も強襲も掛かる前の数字で出し、カットインのあとで掛かった順に書き換える。
        //   単騎 … 負けた側の倍率が ×2 に変わり、その血の数字が数え上がる
        //   強襲 … 勝った側の獲得が 0 まで数え下がり、同じぶん負けた側の血が増える
        // 途中の数字はサーバーの確定値から逆にたどるだけで、掛け算をやり直しはしない
        // （単騎は敗者の支払いを2倍、強襲は勝者の獲得をそのまま敗者へ上乗せ。RonSettlementInfo の注記どおり）。

        private static void PrepareBonusSettlement(Transform panel, RonSettlementInfo settlement)
        {
            var result = panel.Find("Body/DeltaRow");
            var myValue = result.GetChild(1).GetComponent<TextMeshProUGUI>();
            var theirValue = result.GetChild(2).GetComponent<TextMeshProUGUI>();
            var start = BonusStageDeltas(settlement, false, false);
            ShowSettledDelta(settlement.LocalWon ? myValue : theirValue, start.winner);
            ShowSettledDelta(settlement.LocalWon ? theirValue : myValue, start.loser);
            result.gameObject.SetActive(true);

            var loserMult = LoserMultiplierCell(panel, settlement);
            if (loserMult != null)
            {
                loserMult.text = FormatMultiplier(settlement.Multiplier);
                loserMult.color = PanelInk;
            }
        }

        private static IEnumerator RevealBonusSettlement(Transform panel, RonSettlementInfo settlement)
        {
            var result = panel.Find("Body/DeltaRow");
            var myValue = result.GetChild(1).GetComponent<TextMeshProUGUI>();
            var theirValue = result.GetChild(2).GetComponent<TextMeshProUGUI>();
            var winnerValue = settlement.LocalWon ? myValue : theirValue;
            var loserValue = settlement.LocalWon ? theirValue : myValue;
            var loserMult = LoserMultiplierCell(panel, settlement);
            var before = BonusStageDeltas(settlement, false, false);

            if (settlement.IsTankiWait)
            {
                var after = BonusStageDeltas(settlement, true, false);
                if (loserMult != null)
                {
                    yield return RewriteSettlementCell(loserMult, FormatMultiplier(settlement.Multiplier * 2f));
                    if (panel == null) yield break;
                }
                yield return CountSettledDeltas(.40f, (loserValue, before.loser, after.loser));
                if (panel == null) yield break;
                before = after;
            }

            if (settlement.AssaultApplied)
            {
                var after = BonusStageDeltas(settlement, true, true);
                if (settlement.IsTankiWait) yield return new WaitForSeconds(.15f);
                yield return CountSettledDeltas(.45f,
                    (winnerValue, before.winner, after.winner), (loserValue, before.loser, after.loser));
                if (panel == null) yield break;
                if (!settlement.IsTankiWait)
                    AudioManager.Instance?.PlayStinger("se_assault_settlement_stamp", AudioManager.PitchJitter.None);
            }

            yield return SweepSettlementCells(PanelInk, .25f, myValue.rectTransform, theirValue.rectTransform);
        }

        /// <summary>
        /// 単騎・強襲をどこまで掛けたときの血の増減か（勝者は正、敗者は負）。
        /// 両方掛けたものがサーバーの確定値で、そこから逆にたどる。
        /// </summary>
        private static (int winner, int loser) BonusStageDeltas(RonSettlementInfo s, bool withTanki, bool withAssault)
        {
            int winner = s.LocalWon ? s.MyDelta : s.TheirDelta;
            int loser = s.LocalWon ? s.TheirDelta : s.MyDelta;
            if (s.AssaultApplied && !withAssault)
            {
                // 強襲は勝者の獲得を 0 にし、その額を敗者へ上乗せする
                winner = s.AssaultBonusDamage;
                loser += s.AssaultBonusDamage;
            }
            if (s.IsTankiWait && !withTanki) loser = Mathf.RoundToInt(loser / 2f);
            return (winner, loser);
        }

        /// <summary>負けた側の倍率の欄。精算前後の持ち点を出す表では倍率が無いので null。</summary>
        private static TextMeshProUGUI LoserMultiplierCell(Transform panel, RonSettlementInfo s)
        {
            if (s.ShowHpBalances) return null;
            var row = panel.Find("Body/MultRow");
            return row != null ? row.GetChild(s.LocalWon ? 2 : 1).GetComponent<TextMeshProUGUI>() : null;
        }

        private static string FormatMultiplier(float multiplier)
        {
            return "×" + multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>欄の字を書き換え、大きく置いてから段を踏んで元の大きさへ戻す。書き換えた欄は金色にする。
        /// 右端の欄は表の縁に接しているので、膨らませすぎると表の外へはみ出す（1.7 倍ではみ出した）。</summary>
        private static IEnumerator RewriteSettlementCell(TextMeshProUGUI cell, string value)
        {
            var rect = cell.rectTransform;
            cell.text = value;
            cell.color = AccentGold;
            foreach (float scale in new[] { 1.45f, 1.22f, 1.08f })
            {
                rect.localScale = Vector3.one * scale;
                yield return new WaitForSeconds(.05f);
                if (cell == null) yield break;
            }
            rect.localScale = Vector3.one;
            yield return SweepSettlementCells(AccentGold, .30f, rect);
        }

        /// <summary>血の数字を、前の値から新しい値まで段を踏んで数え直す。終わりに一度弾ませる。</summary>
        private static IEnumerator CountSettledDeltas(float duration, params (TextMeshProUGUI text, int from, int to)[] cells)
        {
            const float Steps = 8f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float progress = Mathf.Ceil(t / duration * Steps) / Steps;
                foreach (var cell in cells)
                {
                    if (cell.text == null) yield break;
                    // 途中は10の位でそろえ、端数がちらつかないようにする
                    int value = Mathf.RoundToInt(Mathf.Lerp(cell.from, cell.to, progress) / 10f) * 10;
                    ShowSettledDelta(cell.text, progress >= 1f ? cell.to : value);
                }
                yield return null;
            }

            var rects = new RectTransform[cells.Length];
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].text == null) yield break;
                ShowSettledDelta(cells[i].text, cells[i].to);
                rects[i] = cells[i].text.rectTransform;
            }
            foreach (float scale in new[] { 1.3f, 1.12f })
            {
                foreach (var rect in rects) rect.localScale = Vector3.one * scale;
                yield return new WaitForSeconds(.05f);
                if (rects[0] == null) yield break;
            }
            foreach (var rect in rects) rect.localScale = Vector3.one;
            yield return SweepSettlementCells(AccentGold, .30f, rects);
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
            // 単騎強襲は全体を強襲単独のカットイン（約1.1秒）と同じくらいに収める（2026-11-07 ユーザー指示
            // 「もっとはやめて。強襲のみの時と同じぐらいの演出時間」）。単騎の光も倍速で流す。
            yield return RevealTankiBonus(group, tile, glow, title, note, s.AssaultApplied ? .4f : 1f);
            if (s.AssaultApplied)
            {
                yield return PlayFangBite(rt, tile.rectTransform, glow, title, note, dim);
                yield return new WaitForSeconds(.05f);
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
            AudioManager.Instance?.PlayStinger("se_assault_settlement_cutin", AudioManager.PitchJitter.None);
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

        /// <param name="pace">1 で通常。0.5 なら半分の時間で同じ流れを見せる。</param>
        private static IEnumerator RevealTankiBonus(CanvasGroup group, Image tile, RawImage glow,
            TextMeshProUGUI title, TextMeshProUGUI note, float pace = 1f)
        {
            float TileFadeIn = .19f * pace;
            float BloomTime = .28f * pace;
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
            for (float real = 0f; real < BloomTime; real += Time.deltaTime)
            {
                float elapsed = real / pace; // 以下の刻みは通常の速さで書いてある
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

        // 2026-11-07 ユーザー指示。
        //   1回目「単騎強襲はもっと迫力よくできませんか」
        //   2回目「赤色とかではなく、画面の奥から現れてがぶっとしてる感じ。奥から透明状態のものがだんだんと実体化していって」
        //   3回目「もっとはやめて。強襲のみの時と同じぐらいの演出時間」
        //   4回目 参考動画（ワニが濁った水に半分見えないまま静止して待ち、3〜4コマで一気に飛びかかって鳥を噛む）「こんなイメージで」
        // 牙は**奥に潜んで止まったまま待ち、一瞬で手前まで飛びかかって噛む。** だんだん寄せてはこない。
        //   潜む … 奥で小さく、影の色で薄く浮かぶ。ぴたりと止まる（静止が長いほど飛びかかりが速く見える）
        //   飛びかかる … 数コマで手前まで来て、そのあいだに実体の色になり口を開き切る。見かけの大きさは遠近の 1/z
        //   当たり … 赤や白の閃光は使わない。画面全体の揺れと、最初の残像での一瞬の止め（ヒットストップ）だけ
        //   退場 … また奥へ沈んで消える
        //   文字 … 「単騎強襲」は大きい所から叩きつけて着地で小さく揺らす。「2倍ダメージ」は一拍遅れて弾む
        private const float BiteDimDepth = .45f;
        private const float FangFarScale = .22f;
        private static readonly Color FangShadowColor = new Color(.62f, .56f, .66f, 1f); // 濁った水越しに見えるような灰色。暗くすると紺の精算表に溶けて見えない

        /// <summary>奥行き 0（いちばん奥）〜1（構えの位置）に対する見かけの大きさ。遠近の 1/z で、手前ほど速く大きくなる。</summary>
        private static float FangDepthScale(float t)
        {
            return 1f / Mathf.Lerp(1f / FangFarScale, 1f, Mathf.Clamp01(t));
        }

        private static IEnumerator PlayFangBite(RectTransform parent, RectTransform tile, RawImage glow,
            TextMeshProUGUI title, TextMeshProUGUI note, Image dim)
        {
            float baseDim = dim != null ? dim.color.a : 0f;
            const float LurkFadeIn = .10f;
            const float LurkHold = .30f;
            const float Lunge = .08f;
            const float LurkDepth = .45f;
            const float LurkAlpha = .50f;
            const float MaterializeSteps = 6f;
            const float Retreat = .20f;
            const float BiteHold = .08f;
            const float TileRelease = .08f;
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
            // 牌の奥から迫り出し、中央を大きく挟む位置で構える。
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
                images[jaw].rectTransform.anchoredPosition = ready[jaw];
                images[jaw].color = FangShadowColor;
            }
            var impact = new GameObject("FangImpact", typeof(RectTransform), typeof(PixelShapeGraphic))
                .GetComponent<PixelShapeGraphic>();
            impact.transform.SetParent(jaws.transform, false);
            impact.raycastTarget = false;
            impact.rectTransform.anchoredPosition = tilePosition;

            // 潜む：奥で薄い影として浮かび上がり、止まったまま待つ。
            Vector3 lurkScale = approachScale * FangDepthScale(LurkDepth);
            teeth.localScale = lurkScale;
            opacity.alpha = 0f;
            for (float elapsed = 0; elapsed < LurkFadeIn; elapsed += Time.deltaTime)
            {
                opacity.alpha = Mathf.Ceil(elapsed / LurkFadeIn * 3f) / 3f * LurkAlpha;
                yield return null;
            }
            opacity.alpha = LurkAlpha;
            for (float elapsed = 0; elapsed < LurkHold; elapsed += Time.deltaTime)
            {
                SetBiteDim(dim, baseDim, .5f * elapsed / LurkHold);
                yield return null;
            }

            // 飛びかかる：数コマで手前まで来て実体になり、口を開き切る。速さは終わりほど上がる。
            for (float elapsed = 0; elapsed < Lunge; elapsed += Time.deltaTime)
            {
                float progress = elapsed / Lunge;
                float rush = progress * progress;
                teeth.localScale = approachScale * FangDepthScale(Mathf.Lerp(LurkDepth, 1f, rush));
                opacity.alpha = Mathf.Max(LurkAlpha, Mathf.Ceil(rush * MaterializeSteps) / MaterializeSteps);
                var tint = Color.Lerp(FangShadowColor, Color.white, rush);
                for (int jaw = 0; jaw < images.Length; jaw++)
                {
                    images[jaw].color = tint;
                    images[jaw].rectTransform.anchoredPosition = Vector2.Lerp(ready[jaw], open[jaw], progress);
                }
                glow.color = new Color(1f, 1f, 1f, Mathf.Lerp(.72f, .43f, progress));
                SetBiteDim(dim, baseDim, .5f + .5f * progress);
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
            SetBiteDim(dim, baseDim, 1f);
            WholeScreenShake.Play(.34f, 20f, new Vector2(.7f, 1f));

            // 長い残像と短い残像はそれぞれ一枚の絵として見せる。
            for (int frame = 0; frame < smearFrames.Length; frame++)
            {
                bool first = frame == 0;
                smear.texture = smearFrames[frame];
                smear.enabled = smear.texture != null;
                smear.color = new Color(1f, 1f, 1f, first ? 1f : .82f);
                DrawFangImpact(impact, first ? 1f : .42f);
                glow.color = new Color(1f, .83f, .63f, first ? .96f : .34f);
                // 1枚目を長めに置いて当たりの瞬間を止める
                yield return new WaitForSeconds(first ? .08f : .06f);
            }
            smear.enabled = false;
            impact.gameObject.SetActive(false);
            glow.color = new Color(1f, .83f, .63f, .18f);
            // 残像が消えてから、牙が牌を挟んだ姿をひと呼吸残す。
            yield return new WaitForSeconds(BiteHold);

            title.text = "単騎強襲"; title.color = AccentThem;
            note.text = "2倍ダメージ";
            const float TitleSlam = .08f;
            const float NoteDelay = .04f;
            const float NotePop = .08f;
            bool landed = false;
            float titleSize = title.fontSize;
            title.fontSize = titleSize * 1.25f;
            for (float elapsed = 0; elapsed < Retreat; elapsed += Time.deltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / Retreat);
                float movement = Mathf.SmoothStep(0, 1, progress);
                float release = Mathf.SmoothStep(0, 1, elapsed / TileRelease);
                tile.anchoredPosition = Vector2.Lerp(pressedPosition, tilePosition, release);
                tile.localScale = Vector3.Lerp(pressedScale, tileScale, release);
                // 噛んだあとは奥へ沈んで消える。
                teeth.localScale = Vector3.Lerp(biteScale, approachScale * FangDepthScale(.35f), movement);
                teeth.anchoredPosition = tilePosition + Vector2.Lerp(biteOffset, Vector2.zero, movement);
                for (int jaw = 0; jaw < images.Length; jaw++)
                {
                    images[jaw].rectTransform.anchoredPosition = Vector2.Lerp(bite[jaw], ready[jaw], movement);
                    images[jaw].color = Color.Lerp(Color.white, FangShadowColor, movement);
                }
                opacity.alpha = Mathf.Floor((1 - progress) * MaterializeSteps) / MaterializeSteps;
                glow.color = new Color(1f, .83f, .63f, Mathf.Lerp(.18f, .40f, release));
                SetBiteDim(dim, baseDim, 1f - .45f * movement);
                // 見出しは大きい所から速く落とす（加速して着地）。
                float slam = Mathf.Clamp01(elapsed / TitleSlam);
                title.alpha = 1f;
                title.rectTransform.localScale = Vector3.one * Mathf.Lerp(2.6f, 1f, slam * slam);
                if (!landed && slam >= 1f)
                {
                    landed = true;
                    WholeScreenShake.Play(.16f, 10f, new Vector2(0f, 1f));
                }
                float pop = Mathf.Clamp01((elapsed - TitleSlam - NoteDelay) / NotePop);
                note.alpha = pop > 0f ? 1f : 0f;
                note.rectTransform.localScale = Vector3.one * (pop < .6f ? Mathf.Lerp(1.7f, .92f, pop / .6f)
                                                                        : Mathf.Lerp(.92f, 1f, (pop - .6f) / .4f));
                yield return null;
            }
            title.rectTransform.localScale = note.rectTransform.localScale = Vector3.one;
            tile.anchoredPosition = tilePosition;
            tile.localScale = tileScale;
            glow.color = new Color(1f, .83f, .63f, .40f);
            jaws.SetActive(false);
            Destroy(jaws);
            title.alpha = note.alpha = 1;
        }

        /// <summary>沈め具合 0〜1。0 で元の暗さ、1 で深く暗くする（色は黒のまま。赤くはしない）。</summary>
        private static void SetBiteDim(Image dim, float baseAlpha, float depth)
        {
            if (dim == null) return;
            dim.color = new Color(0f, 0f, 0f, Mathf.Lerp(baseAlpha, BiteDimDepth, Mathf.Clamp01(depth)));
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
