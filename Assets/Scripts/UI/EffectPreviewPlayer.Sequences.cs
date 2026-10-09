using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.EngineData;
using KillingMahjong.UI.Effects;
using KillingMahjong.Visuals;

namespace KillingMahjong.UI
{
    public sealed partial class EffectPreviewPlayer
    {
        private IEnumerator PlayEffect()
        {
            switch (EffectId)
            {
                case "skill.perspective":
                    yield return rig.phase.PlaySkillCutinAnimationRoutine("透視", true, rig.player.CurrentCharacterData);
                    yield return ExposedTileEffectPlayer.PlayReveal(scope, rig.enemyTiles, new List<int> { 3, 12, 25 },
                        (index, tile) => tile.GetComponent<Image>().sprite = rig.tiles.GetTileSprite(Tile(index % 9)));
                    break;
                case "skill.mulligan":
                    yield return rig.phase.PlaySkillCutinAnimationRoutine("牌交換", true, rig.player.CurrentCharacterData);
                    var swap = new MulliganSwapAnimator(rig.tiles);
                    scope.AddCleanup(swap.CancelPresentation);
                    yield return swap.PlayRoutine(Tile(1), Tile(8), rig.handTiles[1]);
                    rig.handTiles[1].GetComponent<Image>().sprite = rig.tiles.GetTileSprite(Tile(8)); break;
                case "skill.boost_hand":
                    // 右上の一覧に「清一色+1」を入れて、届くまで伏せておく（本編と同じ段取り）
                    var boostBoard = rig.yakuList;
                    if (boostBoard != null)
                    {
                        yield return null;   // 一覧の Start が空のデータで上書きするので、1コマ待ってから入れる
                        boostBoard.UpdateBoostData(new Dictionary<string, int> { { "清一色", 1 } }, null);
                        boostBoard.HoldLocalBoostChip("清一色");
                        scope.AddCleanup(() =>
                        {
                            if (boostBoard == null) return;
                            boostBoard.ReleaseLocalBoostChip();
                            boostBoard.UpdateBoostData(null, null);
                        });
                    }
                    yield return rig.phase.PlaySkillCutinAnimationRoutine("役強化", true, rig.player.CurrentCharacterData, subText: "清一色");
                    var boost = BoostHandSkillEffect.Create();
                    scope.AddCleanup(() => { if (boost != null) boost.Dispose(); });
                    yield return boost.Play("清一色", boostBoard, rig.phase.PlayerCutinSprite);
                    yield return new WaitForSeconds(0.8f); break;
                case "skill.assault":
                    yield return rig.phase.PlaySkillCutinAnimationRoutine("強襲", true, rig.player.CurrentCharacterData);
                    var assault = AssaultSkillEffect.Create();
                    // 血の印は本編だと局の終わりまで残る。試写では少し見せてから、抜けるときに消す
                    scope.AddCleanup(() => { if (assault != null) assault.Dispose(); AssaultMarkUI.Clear(); });
                    yield return assault.Play(rig.enemy.HpGaugeAnchor, rig.player.HpGaugeAnchor, () => rig.enemy.PlayBounceAnimation(.4f));
                    yield return new WaitForSeconds(1.5f); break;
                case "skill.cutin.enemy":
                    yield return rig.phase.PlaySkillCutinAnimationRoutine("透視", false, rig.enemy.CurrentCharacterData); break;
                case "unused.special_victory":
                    yield return rig.phase.PlaySkillCutinAnimationRoutine("特殊勝利", true, rig.player.CurrentCharacterData); break;
                case "phase.dealing":
                    bool dark = false;
                    rig.phase.PlayRoundStartDarken("配牌", onReady: () => dark = true);
                    yield return Until(() => dark);
                    yield return new WaitForSeconds(1f);
                    bool light = false; rig.phase.PlayRoundStartFadeOut(() => light = true);
                    yield return Until(() => light); break;
                case "phase.betting":
                    rig.betting.gameObject.SetActive(true);
                    rig.betting.ShowFixedBettingPhase(20000, 20000, 2000, amount => { });
                    yield return rig.betting.WaitForSlideAnimation();
                    yield return new WaitForSeconds(2f);
                    rig.betting.HideBettingPhase(); yield return rig.betting.WaitForSlideAnimation(); break;
                case "phase.battle_start":
                    bool started = false;
                    rig.phase.PlayTransition("1 Round", rig.player, new BettingCompletedInfo {
                        LocalBet = 2000, EnemyBet = 2000, LocalHpBefore = 20000, EnemyHpBefore = 20000,
                        LocalHpAfter = 18000, EnemyHpAfter = 18000, HasServerHealth = true
                    }, () => { }, () => started = true, displayIsLocalTurn: true);
                    yield return Until(() => started); break;
                case "phase.local_turn": yield return rig.phase.PlayCenterTextAnimRoutine("先行"); break;
                case "phase.enemy_turn": yield return rig.phase.PlayCenterTextAnimRoutine("後攻"); break;
                case "phase.prompt": rig.phase.PlayPromptText("手牌を選んでください"); yield return new WaitForSeconds(3f); break;
                case "phase.draw":
                    bool drawn = false; rig.phase.PlayDrawTransition(() => { }, () => drawn = true);
                    yield return Until(() => drawn); break;
                case "ron.chance":
                    RonChanceEffect.Show(rig.handTiles[6]); yield return new WaitForSeconds(3f); break;
                case "ron.impact":
                    yield return RonImpactEffect.Play(this, new List<RectTransform> { rig.player.HpAnchor, rig.enemy.HpAnchor }); break;
                case "ron.local": case "ron.enemy":
                    yield return Ron(EffectId == "ron.local"); break;
                case "settlement.local": case "settlement.enemy":
                    bool paid = false; bool won = EffectId == "settlement.local";
                    rig.phase.PlayScoreSettlementAnimation(won, 2000, 2000, 20000, 20000,
                        won ? 22000 : 18000, won ? 18000 : 22000, "満貫", () => paid = true);
                    yield return Until(() => paid); break;
                case "ending.normal_win": yield return Ending(VictoryType.NormalVictory); break;
                case "ending.normal_lose": yield return Ending(VictoryType.NormalDefeat); break;
                case "ending.special_win": yield return Ending(VictoryType.SpecialVictory); break;
                case "ending.special_lose": yield return Ending(VictoryType.SpecialDefeat); break;
                case "unused.red_defeat":
                    bool ended = false; var red = RedDefeatPrototypeUI.Play(() => ended = true);
                    if (red == null) throw new InvalidOperationException("没案の画像がありません。");
                    scope.AddCleanup(() => { if (red != null) Destroy(red.gameObject); });
                    yield return Until(() => ended); break;
                default: yield return PlaySmallEffect(); break;
            }
            yield return new WaitForSeconds(.6f);
        }

        private IEnumerator Until(Func<bool> predicate)
        {
            float deadline = Time.unscaledTime + 45f;
            while (!predicate())
            {
                if (Time.unscaledTime > deadline) throw new TimeoutException("演出が完了しませんでした: " + EffectId);
                yield return null;
            }
        }

        private IEnumerator Ending(VictoryType type)
        {
            var ending = new GameObject("PreviewEnding", typeof(RectTransform)).AddComponent<EndingSequenceUI>();
            ending.Show(rig.Ending(type), rig.font, () => { });
            // 本文 → 結果文字のフェード → クレジット。本編のボタンを自動送りするだけ。
            while (ending.Stage == EndingSequenceUI.EndingStage.Dialogue)
            { yield return new WaitForSeconds(2f); ending.Advance(); }
            yield return new WaitForSeconds(2.5f); ending.Advance();
            yield return new WaitForSeconds(3f);
        }

        private IEnumerator Ron(bool won)
        {
            bool done = false;
            // 判定ではなく表示用の固定局面。戦績は VictoryUI を通さず変更しない。
            var hand = new List<int>();
            foreach (int id in new[] {0,0,0,0,1,2,3,4,5,6,8,27,27}) hand.Add(Tile(id));
            var settlement = new RonSettlementInfo {
                TotalHan = 5, Multiplier = 1, RankName = "満貫", MyBet = 2000, TheirBet = 2000,
                LocalWon = won, MyDelta = won ? 2000 : -2000, TheirDelta = won ? -2000 : 2000,
                MyHpBefore = 20000, TheirHpBefore = 20000,
                MyHpAfter = won ? 22000 : 18000, TheirHpAfter = won ? 18000 : 22000,
                ShowPerRowHan = true, Rows = new List<RonSettlementInfo.YakuRow> {
                    new RonSettlementInfo.YakuRow { Name = "混一色", Han = 3 },
                    new RonSettlementInfo.YakuRow { Name = "一気通貫", Han = 2 } }
            };
            rig.ron.PlayRonSequence(hand, Tile(7), new List<string> { "混一色", "一気通貫" },
                "2000 × 1", "満貫", 2000, won, rig.player, rig.enemy, 20000,
                settlement.MyHpAfter, 20000, settlement.TheirHpAfter, () => done = true,
                "2000 × 1", settlement);
            yield return Until(() => done);
        }
        private static int Tile(int baseId) => KillingMahjong.Managers.TutorialTiles.Encode(baseId, false);
    }
}
