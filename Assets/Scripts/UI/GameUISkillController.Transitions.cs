using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;
using UnityEngine.UI;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    // スキルの応答待ち・演出・ロック解放。入力と選択UIは元のpartialに残す。
    public partial class GameUISkillController
    {
        private TransitionLockSet.Lease pendingMulligan;

        // 再生中の役強化・強襲の演出。途中で打ち切るときに片付けるために持つ
        private Effects.BoostHandSkillEffect _boostEffect;
        private Effects.AssaultSkillEffect _assaultEffect;
        private readonly HashSet<TransitionLockSet.Lease> skillTransitions = new HashSet<TransitionLockSet.Lease>();
        private readonly HashSet<System.Action<StatusData>> skillStatusHandlers = new HashSet<System.Action<StatusData>>();

        public void CancelPendingSkillRequest()
        {
            if (pendingMulligan == null) return;
            pendingMulligan?.Dispose();
            pendingMulligan = null;
            _lastMulliganOutSlotRt = null;
            _lastMulliganOutTileId = _lastMulliganTargetIndex = -1;
        }

        internal void CancelActiveTransitions()
        {
            // 子の演出は別のMonoBehaviour上で進む場合もある。入力ロックより先に表示を片付ける。
            if (uiManager != null)
            {
                if (uiManager.PhaseTransitionUI != null) uiManager.PhaseTransitionUI.CancelSkillPresentations();
                if (uiManager.VisualController != null) uiManager.VisualController.CancelSkillPresentations();
            }
            _mulliganSwapAnimator?.CancelPresentation();
            // 役強化・強襲の演出は自前の入れ物で動いている。コルーチンを止めるだけだと画面に残る
            if (_boostEffect != null) _boostEffect.Dispose();
            if (_assaultEffect != null) _assaultEffect.Dispose();
            _boostEffect = null;
            _assaultEffect = null;
            StopAllCoroutines();
            foreach (var lease in skillTransitions) lease.Dispose();
            skillTransitions.Clear();
            if (NetworkMessageHandler.Instance != null)
                foreach (var handler in skillStatusHandlers) NetworkMessageHandler.Instance.OnStatusReceived -= handler;
            skillStatusHandlers.Clear();
            CancelPendingSkillRequest();
            CancelSkillSelection();
        }

        private void OnDisable() { CancelActiveTransitions(); }

        public void HandleSkillCasted(SkillCastedData data)
        {
            StartCoroutine(HandleSkillCastedRoutine(data));
        }

        private System.Collections.IEnumerator HandleSkillCastedRoutine(SkillCastedData data)
        {
            if (data == null) yield break;
            var ownedLock = uiManager.BeginTransition("skill:" + data.skillType);
            skillTransitions.Add(ownedLock);
            // 演出のロックを取得してから、同じマリガン要求の待ちを解除する。
            if (data.skillType == "mulligan" && data.player_id == NetworkMessageHandler.Instance.LocalPlayerId)
            {
                pendingMulligan?.Dispose();
                pendingMulligan = null;
            }
            bool finished = false;
            try { yield return HandleSkillEffectsRoutine(data, ownedLock); finished = ownedLock.IsActive; }
            finally { skillTransitions.Remove(ownedLock); ownedLock.Dispose(); }
            if (finished) uiManager.RefreshBoardAfterTransition();
        }

        private System.Collections.IEnumerator HandleSkillEffectsRoutine(SkillCastedData data, TransitionLockSet.Lease ownedLock)
        {

            // 発動の合図として一瞬だけ光らせる。カットインが出る前に置くこと
            // **白フラッシュは止めた（2026-08-20 の演出削減バッチ1）。**
            // ロンと同じ理由で、直後に出るカットインの黒幕(α0.5)に埋もれて効いていない。
            // Effects.ScreenFlash.Play();

            // 能力麻雀の核であるスキル発動が完全に無音だったため、種類別の音を鳴らす
            var audioMgr = Managers.AudioManager.Instance;
            if (audioMgr != null) audioMgr.PlaySkillSE(data.skillType);

            // **発動の手応えは揺れで出す（2026-09-13）。**
            // 上のフラッシュを止めたとき、合図が音だけになっていた。
            // 音は切ってあることがある（`AudioManager.AudioEnabled`）ので、
            // 無音でも「何かが起きた」と分かる合図が要る。
            // ロンより弱くする。能力は局に何度も飛ぶので、同じ強さだと疲れる。
            Effects.ScreenQuake.Play(12f, 0.22f);

            string localPlayerId = KillingMahjong.Network.NetworkMessageHandler.Instance.LocalPlayerId;
            bool isLocalPlayer = (data.player_id == localPlayerId);
            string skillName = SkillNames.GetDisplayName(data.skillType);

            // **強襲の「1局1回」を覚えておく（2026-08-26）。**
            // サーバーが受け付けた（skill_casted が返った）ときだけ立てる。
            // 送信時に立てると、別の理由で弾かれたときに撃っていないのに使用済みになる。
            // 倒すのは局頭の BoardStateManager.ClearAllBoardData()。
            if (isLocalPlayer && data.skillType == SkillNames.Assault)
            {
                Managers.BoardStateManager.Instance?.MarkLocalAssaultUsed();
            }

            string subText = null;

            // 役強化で強まった役の名前。発動後の演出でも出すので、外に出しておく
            string boostedYakuName = "";

            if (data.skillType == "boost_hand")
            {
                var oldLocalBonus = Managers.BoardStateManager.Instance.LocalBoostHandBonus != null ?
                    new Dictionary<string, int>(Managers.BoardStateManager.Instance.LocalBoostHandBonus) : new Dictionary<string, int>();
                var oldEnemyBonus = Managers.BoardStateManager.Instance.EnemyBoostHandBonus != null ?
                    new Dictionary<string, int>(Managers.BoardStateManager.Instance.EnemyBoostHandBonus) : new Dictionary<string, int>();

                bool statusReceived = false;
                System.Action<KillingMahjong.EngineData.StatusData> onStatus = (statusData) => { statusReceived = true; };
                NetworkMessageHandler.Instance.OnStatusReceived += onStatus;
                skillStatusHandlers.Add(onStatus);

                float timeout = 2.0f;
                try
                {
                    while (!statusReceived && timeout > 0)
                    {
                        timeout -= Time.deltaTime;
                        yield return null;
                        if (!ownedLock.IsActive) yield break;
                    }
                }
                finally
                {
                    NetworkMessageHandler.Instance.OnStatusReceived -= onStatus;
                    skillStatusHandlers.Remove(onStatus);
                }

                var newLocalBonus = Managers.BoardStateManager.Instance.LocalBoostHandBonus ?? new Dictionary<string, int>();
                var newEnemyBonus = Managers.BoardStateManager.Instance.EnemyBoostHandBonus ?? new Dictionary<string, int>();

                var targetOldBonus = isLocalPlayer ? oldLocalBonus : oldEnemyBonus;
                var targetNewBonus = isLocalPlayer ? newLocalBonus : newEnemyBonus;

                foreach (var kvp in targetNewBonus)
                {
                    if (!targetOldBonus.ContainsKey(kvp.Key) || targetOldBonus[kvp.Key] < kvp.Value)
                    {
                        boostedYakuName = kvp.Key;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(boostedYakuName))
                {
                    subText = $"<color=yellow>{boostedYakuName}</color>";
                }
            }

            // --- プレ解析：透視スキルの場合の newlyExposed の抽出 ---
            // サーバーからのstatus上書き前に最新の追加分を計算する
            List<int> newlyExposed = new List<int>();
            if (data.skillType == "perspective" && isLocalPlayer)
            {
                if (data.exposedHandIndexes != null && data.exposedHandIndexes.Count > 0)
                {
                    List<int> targetIndexes = data.exposedHandIndexes;
                    if (data.exposedHandIndexesByPlayer != null)
                    {
                        foreach (var kvp in data.exposedHandIndexesByPlayer)
                        {
                            if (kvp.Key != localPlayerId)
                            {
                                targetIndexes = kvp.Value;
                                break;
                            }
                        }
                    }

                    foreach (int val in targetIndexes)
                    {
                        int wallIdx = val;
                        if (wallIdx >= 0 && wallIdx < 34)
                        {
                            if (!Managers.BoardStateManager.Instance.ExposedEnemyHandWallIndexes.Contains(wallIdx))
                            {
                                newlyExposed.Add(wallIdx);
                                Managers.BoardStateManager.Instance.ExposedEnemyHandWallIndexes.Add(wallIdx);
                            }
                        }
                    }
                }
            }
            // 1. 以前の大迫力カットイン演出（血飛沫＋立ち絵＋巨大テキスト）を再生する
            if (uiManager.PhaseTransitionUI != null)
            {
                CharacterData cData = isLocalPlayer ? uiManager.PlayerInfoUI.CurrentCharacterData : uiManager.EnemyInfoUI.CurrentCharacterData;
                yield return uiManager.PhaseTransitionUI.PlaySkillCutinAnimationRoutine(skillName, isLocalPlayer, cData, 2.0f, null, subText);
                if (!ownedLock.IsActive) yield break;
            }
            else if (uiManager.DialogueUI != null)
            {
                string castMessage = isLocalPlayer ? $"【あなた】がアビリティを発動！\n「{skillName}」" : $"【相手】がアビリティを発動！\n「{skillName}」";
                uiManager.DialogueUI.ShowText(castMessage);
                yield return new WaitForSeconds(2.0f);
                if (!ownedLock.IsActive) yield break;
            }

            // 2. HP（コスト）の支払い演出
            //
            // **血はサーバーが正。** skill_casted の health（支払い後の値）をそのまま使う。
            // health は発動した側の値なので、相手が撃ったときは相手側に入れる。
            //
            // 0 のときは「サーバーが返していない」とみなし、従来どおり自前で引く。
            // 古いサーバーに繋いだときに血が 0 へ飛ぶのを防ぐため。
            {
                var board = Managers.BoardStateManager.Instance;
                int localHp = board.LocalPlayerHp;
                int enemyHp = board.EnemyPlayerHp;

                // 払った血の量は「支払い前」を控えないと出せない。
                // 反応（Skill_HighCostPaid / Skill_NearDeathByCost）の判定に使う
                int hpBeforeCast = isLocalPlayer ? localHp : enemyHp;

                // **引き算はしない。** サーバーが払ったあとの血を送ってくる。
                // 以前は health が無いときに `血 − cost` で自前計算していたが、
                // クライアントが辻褄を合わせるとサーバー側の誤りが画面に出なくなる。
                // 届かないときは動かさず、警告だけ出して次の status に任せる
                if (data.health > 0)
                {
                    if (isLocalPlayer) localHp = data.health;
                    else enemyHp = data.health;
                }
                else
                {
                    Debug.LogWarning($"[Skill] skill_casted に health が入っていません（{data.skillType}）。" +
                                     "血はここでは動かさず、status の同期に任せます");
                }

                board.UpdateHp(localHp, enemyHp);

                if (isLocalPlayer)
                {
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetHP(board.LocalPlayerHp);
                }
                else
                {
                    if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetHP(board.EnemyPlayerHp);
                }

                // スキルへの反応。カットインと血の演出が終わってから喋らせたいので、
                // ここ（血を反映したあと）で積む。実際に出るのは下の待機のあと
                var reaction = Managers.ReactionController.Instance;
                if (reaction != null)
                {
                    int hpAfterCast = isLocalPlayer ? board.LocalPlayerHp : board.EnemyPlayerHp;
                    int costPaid = Mathf.Max(0, hpBeforeCast - hpAfterCast);
                    reaction.HandleSkillCast(data.skillType, isLocalPlayer, costPaid, hpAfterCast);
                }
            }

            // 体力が減る様子をしっかり見せるためのタメ（待機）
            yield return new WaitForSeconds(1.0f);
            if (!ownedLock.IsActive) yield break;

            // --- 以降、実際のアビリティ効果（透視以外も含む）を実行 ---

            if (data.skillType == "perspective")
            {
                if (isLocalPlayer)
                {
                    if (newlyExposed.Count > 0 && uiManager.VisualController != null)
                    {
                        // 演出を見せるため、アニメーション完了を待つ
                        yield return StartCoroutine(uiManager.VisualController.PlayPerspectiveAnimation(newlyExposed));
                        if (!ownedLock.IsActive) yield break;

                    }
                    else
                    {

                    }
                }
                else
                {
                    // 敵プレイヤーの透視の場合は、ローカルプレイヤーの手牌が透視される
                    List<int> targetIndexes = data.exposedHandIndexes;
                    if (data.exposedHandIndexesByPlayer != null && data.exposedHandIndexesByPlayer.ContainsKey(localPlayerId))
                    {
                        targetIndexes = data.exposedHandIndexesByPlayer[localPlayerId];
                    }

                    if (targetIndexes != null)
                    {
                        foreach (int val in targetIndexes)
                        {
                            int wallIdx = val; // Python sends wall indices

                            if (wallIdx >= 0 && wallIdx < 34)
                            {
                                Managers.BoardStateManager.Instance.ExposedLocalHandWallIndexes.Add(wallIdx);
                            }
                        }
                    }

                }
            }
            else if (data.skillType == "boost_hand")
            {
                // **発動後の演出（2026-10-09）。** それまではカットインが出て血が減るだけだった。
                // 自分が使ったときだけ（相手のときはカットインのまま。ユーザーの判断）
                if (isLocalPlayer && uiManager.PlayerInfoUI != null)
                {
                    _boostEffect = Effects.BoostHandSkillEffect.Create();
                    if (_boostEffect != null)
                    {
                        yield return _boostEffect.Play(boostedYakuName);
                        _boostEffect = null;
                        if (!ownedLock.IsActive) yield break;
                    }
                }
            }
            else if (data.skillType == SkillNames.Assault)
            {
                // 同上。自分の血を抜いて相手の血の表示へ撃ち込み、局の終わりまで印を残す
                // （印を消すのは局の頭。BoardStateManager.ClearAllBoardData）
                if (isLocalPlayer && uiManager.PlayerInfoUI != null && uiManager.EnemyInfoUI != null)
                {
                    _assaultEffect = Effects.AssaultSkillEffect.Create();
                    if (_assaultEffect != null)
                    {
                        var enemyInfo = uiManager.EnemyInfoUI;
                        yield return _assaultEffect.Play(enemyInfo.HpGaugeAnchor, uiManager.PlayerInfoUI.HpGaugeAnchor,
                            () => { if (enemyInfo != null) enemyInfo.PlayBounceAnimation(0.4f); });
                        _assaultEffect = null;
                        if (!ownedLock.IsActive) yield break;
                    }
                }
            }
            else if (data.skillType == "mulligan")
            {
                if (isLocalPlayer)
                {
                    uiManager.ClearSelection();

                    if (data.mulliganResult != null)
                    {
                        int oldTileId = data.mulliganResult.oldTile;
                        int newTileId = data.mulliganResult.newTile;
                        int targetHandIndex = data.mulliganResult.targetHandIndex;

                        var stateMgr = Managers.BoardStateManager.Instance;

                        // 同じ牌IDが複数あるときに別の牌が巻き添えになる不具合を追うための診断。
                        // 状態更新は Remove/IndexOf に頼っており、いずれも「最初の1枚」しか見ない。
                        Debug.Log($"[Mulligan] old={oldTileId} new={newTileId} serverIndex={targetHandIndex}"
                            + $" clickedWallIndex={_lastMulliganTargetIndex}"
                            + $" / oldTileの枚数: hand={CountOf(stateMgr.CurrentHandTiles, oldTileId)}"
                            + $" wall={CountOf(stateMgr.CurrentWallTiles, oldTileId)}"
                            + $" originalWall={CountOf(stateMgr.OriginalWallTiles, oldTileId)}");
                        Debug.Log($"[Mulligan] BEFORE wall = {Join(stateMgr.CurrentWallTiles)}");
                        Debug.Log($"[Mulligan] BEFORE orig = {Join(stateMgr.OriginalWallTiles)}");

                        // サーバーが交換した「位置」を正として山を同期する。
                        // 同じ牌IDは山に複数あるので、牌IDで探すと別の牌を書き換えてしまう。
                        // ここを怠ると OriginalWallTiles がサーバーとズレ、
                        // 位置で牌を特定する処理（TileInteraction.WallIndex）が次の交換で誤動作する。
                        bool syncedByIndex = stateMgr.ReplaceWallTileAt(targetHandIndex, newTileId);
                        if (!syncedByIndex)
                        {
                            // index が使えない場合だけ、従来どおり牌IDで辻褄を合わせる
                            int wallIdx = stateMgr.CurrentWallTiles.IndexOf(oldTileId);
                            if (wallIdx >= 0) stateMgr.CurrentWallTiles[wallIdx] = newTileId;
                        }

                        // 交換した牌が手牌に入っていた場合は、手牌側も入れ替える。
                        // 手牌は「山から選んだ13枚」なので、山の同期とは別に持ち替えが要る。
                        if (stateMgr.CurrentHandTiles.Contains(oldTileId))
                        {
                            stateMgr.CurrentHandTiles.Remove(oldTileId);
                            stateMgr.CurrentHandTiles.Add(newTileId);
                            stateMgr.SortTileIds(stateMgr.CurrentHandTiles);
                        }

                        Debug.Log($"[Mulligan] AFTER  wall = {Join(stateMgr.CurrentWallTiles)}");
                        Debug.Log($"[Mulligan] AFTER  orig = {Join(stateMgr.OriginalWallTiles)}");

                        // アニメーション用のスロットを取得（直前の操作時の記録があればそれを使う、無ければデフォルト）
                        RectTransform targetSlot = _lastMulliganOutSlotRt;
                        if (targetSlot == null && uiManager.HandUI != null)
                        {
                            targetSlot = uiManager.HandUI.GetTileSlotRectTransform(oldTileId);
                        }

                        if (_mulliganSwapAnimator != null)
                        {
                            yield return _mulliganSwapAnimator.PlayRoutine(oldTileId, newTileId, targetSlot);
                            if (!ownedLock.IsActive) yield break;
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Mulligan animation failed: mulliganResult is null.");
                    }

                    _lastMulliganOutTileId = -1;
                    _lastMulliganTargetIndex = -1;

                    // ロック解除・最後の再構築は外側のスキル処理が一度だけ担当する。
                }
                else
                {
                    if (data.mulliganResult != null)
                    {
                        int oldTileId = data.mulliganResult.oldTile;
                        int newTileId = data.mulliganResult.newTile;

                        var stateMgr = Managers.BoardStateManager.Instance;
                        if (stateMgr.CurrentEnemyHandTiles.Contains(oldTileId))
                        {
                            stateMgr.CurrentEnemyHandTiles.Remove(oldTileId);
                            stateMgr.CurrentEnemyHandTiles.Add(newTileId);
                            stateMgr.SortTileIds(stateMgr.CurrentEnemyHandTiles);

                            int wallIdx = stateMgr.CurrentEnemyWallTiles.IndexOf(newTileId);
                            if (wallIdx >= 0)
                            {
                                stateMgr.CurrentEnemyWallTiles[wallIdx] = oldTileId;
                            }
                        }
                        else if (stateMgr.CurrentEnemyWallTiles.Contains(oldTileId))
                        {
                            int wallIdx = stateMgr.CurrentEnemyWallTiles.IndexOf(oldTileId);
                            if (wallIdx >= 0)
                            {
                                stateMgr.CurrentEnemyWallTiles.RemoveAt(wallIdx);
                                stateMgr.CurrentEnemyWallTiles.Add(newTileId);
                                stateMgr.SortTileIds(stateMgr.CurrentEnemyWallTiles);
                            }
                        }
                    }


                }
            }


        }
    }
}
