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
    [RequireComponent(typeof(GameUIManager))]
    public class GameUISkillController : MonoBehaviour
    {
        private GameUIManager uiManager;
        private YakuSelectionUI yakuSelectionUI;

        [Header("Yaku Selection")]
        [SerializeField] private Font yakuSelectionFont;

        public bool IsMulliganSelection { get; private set; }

        private int _lastMulliganOutTileId = -1;
        private int _lastMulliganTargetIndex = -1;

        /// <summary>牌交換スキルの交換演出（分離クラス）</summary>
        private MulliganSwapAnimator _mulliganSwapAnimator;

        public void Setup(GameUIManager manager)
        {
            this.uiManager = manager;
            _mulliganSwapAnimator = new MulliganSwapAnimator(manager);

            if (mulliganCanvas != null)
            {
                mulliganCanvas.SetActive(false);
            }
        }

        public void CancelSkillSelection()
        {
            IsMulliganSelection = false;
            HideMulliganUI();
        }

        [Header("Mulligan UI Settings")]
        [SerializeField] private GameObject mulliganCanvas;
        private System.Collections.Generic.List<GameObject> hiddenUIs = new System.Collections.Generic.List<GameObject>();

        public void StartMulliganSelection()
        {
            IsMulliganSelection = true;
            ShowMulliganUI();
        }

        private RectTransform _lastMulliganOutSlotRt;

        /// <summary>診断用。牌IDの並びを「id(牌名)」形式で連結する。</summary>
        private static string Join(System.Collections.Generic.List<int> list)
        {
            if (list == null) return "(null)";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                int id = list[i];
                sb.Append(i).Append(':').Append(id);
                int b = Common.TileId.BaseId(id);
                if (b > 28) sb.Append("(範囲外!)");
            }
            return sb.ToString();
        }

        /// <summary>診断用。リスト内に同じ牌IDが何枚あるかを数える。</summary>
        private static int CountOf(System.Collections.Generic.List<int> list, int tileId)
        {
            if (list == null) return 0;
            int n = 0;
            foreach (var v in list) if (v == tileId) n++;
            return n;
        }

        public void OnMulliganTileSelected(int tileId, RectTransform slotRt)
        {
            IsMulliganSelection = false;
            HideMulliganUI();
            
            // アニメーション中の不意なRebuildを防ぐ
            uiManager.SetIsTransitioning(true);
            
            var wallTiles = BoardStateManager.Instance.OriginalWallTiles;
            if (wallTiles != null)
            {
                // クリックされた牌そのものの山index を使う。
                // ここで wallTiles.IndexOf(tileId) を使うと、同じ牌IDが山に複数あるとき
                // **常に最初の1枚の index が返り、別の牌が交換されてしまう**
                // （同じ絵柄の別の牌が入れ替わって見える不具合の原因だった）。
                int targetIndex = -1;
                var clicked = slotRt != null ? slotRt.GetComponent<TileInteraction>() : null;
                if (clicked != null) targetIndex = clicked.WallIndex;

                // 山に並べていない牌など WallIndex が無い場合だけ、従来どおり牌IDから引く
                if (targetIndex < 0 || targetIndex >= wallTiles.Count || wallTiles[targetIndex] != tileId)
                {
                    targetIndex = wallTiles.IndexOf(tileId);
                }

                if (targetIndex != -1)
                {
                    _lastMulliganOutTileId = tileId;
                    _lastMulliganTargetIndex = targetIndex;
                    _lastMulliganOutSlotRt = slotRt;
                    
                    // クリック直後には透明にしない。アニメーション開始時に透明にする。

                    uiManager.SendActionToServer("skill", new Network.ActionPayload { skill_type = "mulligan", target_hand_index = targetIndex });
                }
                else
                {
                    Debug.LogWarning("Mulligan failed: Selected tile not found in wall tiles.");
                    uiManager.SetIsTransitioning(false);
                }
            }
        }

        private void ShowMulliganUI()
        {
            if (mulliganCanvas != null) mulliganCanvas.SetActive(true);
            
            _sortingScope.BringToFront(uiManager.HandUI?.gameObject, UISortingOrders.MulliganFocusTiles);
            _sortingScope.BringToFront(uiManager.WallUI?.gameObject, UISortingOrders.MulliganFocusTiles);
            
            // Hide distracting/overlapping UI elements
            hiddenUIs.Clear();
            HideIfActive(uiManager.DialogueUI?.gameObject);
            HideIfActive(uiManager.PlayerInfoUI?.gameObject);
            HideIfActive(uiManager.EnemyInfoUI?.gameObject);
            HideIfActive(uiManager.YakuListUI?.gameObject);
        }

        private void HideIfActive(GameObject go)
        {
            if (go != null && go.activeSelf)
            {
                hiddenUIs.Add(go);
                go.SetActive(false);
            }
        }

        /// <summary>マリガン中の手牌/山UIの前面化と復元。
        /// プロジェクトルールに従い、対象のルートCanvasの overrideSorting のみを操作する。</summary>
        private readonly CanvasSortingScope _sortingScope = new CanvasSortingScope();

        private void HideMulliganUI()
        {
            if (mulliganCanvas != null) mulliganCanvas.SetActive(false);
            if (uiManager != null)
            {
                _sortingScope.Restore(uiManager.HandUI?.gameObject);
                _sortingScope.Restore(uiManager.WallUI?.gameObject);
            }
            
            foreach (var go in hiddenUIs)
            {
                if (go != null) go.SetActive(true);
            }
            hiddenUIs.Clear();
        }

        public void StartBoostHandSelection()
        {
            if (yakuSelectionUI == null)
            {
                yakuSelectionUI = gameObject.AddComponent<YakuSelectionUI>();
                if (yakuSelectionFont != null)
                {
                    yakuSelectionUI.customFont = yakuSelectionFont;
                }
            }

            yakuSelectionUI.Show(
                onSelected: (yakuName) => {
                    uiManager.SendActionToServer("skill", new Network.ActionPayload { skill_type = "boost_hand", yaku_name = yakuName });
                },
                onCanceled: () => {
                    Debug.Log("Boost hand cancelled");
                }
            );
        }

        public void HandleSkillCasted(SkillCastedData data)
        {
            StartCoroutine(HandleSkillCastedRoutine(data));
        }

        private System.Collections.IEnumerator HandleSkillCastedRoutine(SkillCastedData data)
        {
            uiManager.SetIsTransitioning(true); // ★ アニメーション中の非同期Rebuildを防ぐ

            // 発動の合図として一瞬だけ光らせる。カットインが出る前に置くこと
            // **白フラッシュは止めた（2026-08-20 の演出削減バッチ1）。**
            // ロンと同じ理由で、直後に出るカットインの黒幕(α0.5)に埋もれて効いていない。
            // Effects.ScreenFlash.Play();

            // 能力麻雀の核であるスキル発動が完全に無音だったため、種類別の音を鳴らす
            var audioMgr = Managers.AudioManager.Instance;
            if (audioMgr != null) audioMgr.PlaySkillSE(data.skillType);

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

            if (data.skillType == "boost_hand")
            {
                var oldLocalBonus = Managers.BoardStateManager.Instance.LocalBoostHandBonus != null ? 
                    new Dictionary<string, int>(Managers.BoardStateManager.Instance.LocalBoostHandBonus) : new Dictionary<string, int>();
                var oldEnemyBonus = Managers.BoardStateManager.Instance.EnemyBoostHandBonus != null ? 
                    new Dictionary<string, int>(Managers.BoardStateManager.Instance.EnemyBoostHandBonus) : new Dictionary<string, int>();

                bool statusReceived = false;
                System.Action<KillingMahjong.EngineData.StatusData> onStatus = (statusData) => { statusReceived = true; };
                NetworkMessageHandler.Instance.OnStatusReceived += onStatus;

                float timeout = 2.0f;
                while (!statusReceived && timeout > 0)
                {
                    timeout -= Time.deltaTime;
                    yield return null;
                }
                NetworkMessageHandler.Instance.OnStatusReceived -= onStatus;

                var newLocalBonus = Managers.BoardStateManager.Instance.LocalBoostHandBonus ?? new Dictionary<string, int>();
                var newEnemyBonus = Managers.BoardStateManager.Instance.EnemyBoostHandBonus ?? new Dictionary<string, int>();

                var targetOldBonus = isLocalPlayer ? oldLocalBonus : oldEnemyBonus;
                var targetNewBonus = isLocalPlayer ? newLocalBonus : newEnemyBonus;

                string boostedYakuName = "";
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
            }
            else if (uiManager.DialogueUI != null)
            {
                string castMessage = isLocalPlayer ? $"【あなた】がアビリティを発動！\n「{skillName}」" : $"【相手】がアビリティを発動！\n「{skillName}」";
                uiManager.DialogueUI.ShowText(castMessage);
                yield return new WaitForSeconds(2.0f);
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

            // --- 以降、実際のアビリティ効果（透視以外も含む）を実行 ---

            if (data.skillType == "perspective")
            {
                if (isLocalPlayer)
                {
                    if (newlyExposed.Count > 0 && uiManager.VisualController != null)
                    {
                        // 演出を見せるため、アニメーション完了を待つ
                        yield return StartCoroutine(uiManager.VisualController.PlayPerspectiveAnimation(newlyExposed));
                        
                        // アニメーション完了後にUIロックを解除する
                        uiManager.SetIsTransitioning(false);
                        uiManager.VisualController?.RebuildAllTilesFromState();
                    }
                    else
                    {
                        uiManager.SetIsTransitioning(false);
                        uiManager.VisualController?.RebuildAllTilesFromState();
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
                    uiManager.SetIsTransitioning(false);
                    uiManager.VisualController?.RebuildAllTilesFromState();
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
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Mulligan animation failed: mulliganResult is null.");
                    }

                    _lastMulliganOutTileId = -1;
                    _lastMulliganTargetIndex = -1;
                    
                    // MulliganSwapAnimator側で IsTransitioning=false と RebuildAllTilesFromState が呼ばれるため、ここでは不要
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
                    
                    uiManager.SetIsTransitioning(false);
                    uiManager.VisualController?.RebuildAllTilesFromState();
                }
            }

            // **どのスキルでも必ずロックを解除する。**
            //
            // 上の分岐は透視とマリガンしか見ておらず、役強化・特殊勝利・強襲は
            // 198行目で立てた IsTransitioning を誰も倒さないまま抜けていた。
            // 立ったままだと入力が固まるだけでなく、演出中に届いたサーバーイベントが
            // DeferUntilIdle に積まれたまま流れないため、手牌選択が永久に完了せず
            // **相手も巻き添えで止まる**（2026-08-07 に強襲で発覚。実際には元からある不具合で、
            // 役強化 10000・特殊勝利 30000 は滅多に撃たれないので表に出ていなかっただけ）。
            //
            // 透視・マリガンの分岐は演出を yield で待ち切ってから解除しているので、
            // ここに来た時点では既に false。二重に倒しても害はない。
            uiManager.SetIsTransitioning(false);
            uiManager.VisualController?.RebuildAllTilesFromState();
        }
    }
}
