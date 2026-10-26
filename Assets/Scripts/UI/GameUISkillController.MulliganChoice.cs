using System.Collections;
using System.Collections.Generic;
using KillingMahjong.Managers;
using KillingMahjong.Network;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    // 牌交換（マリガン）の新しい流れ（2026-10-11 のスキル変更）。
    //
    // 「1枚を山札と交換（即決）」から「3枚出し、出した3枚＋山札の3枚の6枚から、残す3枚を選ぶ」に変わった。
    // やりとりは2回:
    //
    //   1. 出す3枚を選ぶ             … TryPickForMulligan。3枚目で skill（target_hand_indexes）を送る
    //   2. 候補が届く（本人にだけ）   … HandleSkillAccepted。skill_accepted の mulliganOptions
    //   3. 残す3枚を選ぶ             … MulliganChoiceUI。mulligan_select（choices）を送る
    //   4. 結果が届く（本人にだけ）   … HandleMulliganSelected。山を入れ替えて、交換の演出を出す
    //
    // 2〜4 は1つの演出として順番待ち（Effects.EffectQueue）に入れる。カットイン（skill_casted）と重ならない。
    //
    // チュートリアルも同じ選択UIを使い、通信部分だけ台本側の応答へ渡す。
    public partial class GameUISkillController
    {
        internal System.Action<List<int>> TutorialMulliganPicked { get; set; }
        private class MulliganPick
        {
            public int TileId;
            public int WallIndex;
            public RectTransform Slot;
        }

        /// <summary>出す牌の枚数。サーバーは「3つちょうど・重複なし」しか受け付けない。</summary>
        private const int MulliganGiveCount = 3;

        /// <summary>出すと決めた牌に掛ける色。</summary>
        private static readonly Color MulliganPickTint = new Color(1f, 0.55f, 0.55f, 1f);

        /// <summary>結果（mulligan_selected）を待つ上限（秒）。届かなければ、演出なしで先へ進める。</summary>
        private const float MulliganSelectedTimeout = 8f;

        private readonly List<MulliganPick> _mulliganPicks = new List<MulliganPick>();

        /// <summary>送った3枚の、山の位置 → 牌の置き場。交換の演出で、どこから出て行くかに使う。</summary>
        private readonly Dictionary<int, RectTransform> _mulliganSentSlots = new Dictionary<int, RectTransform>();

        private MulliganSelectedData _mulliganSelected;
        private bool _awaitingMulliganSelected;
        private float _ownMulliganCastSeenAt = -100f;

        /// <summary>
        /// 出す牌を1枚選ぶ／外す。3枚そろったら送る。
        /// </summary>
        /// <returns>ここで3枚の選択を受け持ったら true。</returns>
        private bool TryPickForMulligan(int tileId, RectTransform slotRt)
        {
            if (uiManager == null) return false;
            if (uiManager.IsTutorialMode && TutorialMulliganPicked == null) return true;

            var wallTiles = BoardStateManager.Instance != null ? BoardStateManager.Instance.OriginalWallTiles : null;
            if (wallTiles == null) return true;

            // もう一度押したら外す。**同じ牌かどうかは、押した置き場で見る**
            // （同じ絵柄の牌が何枚もあるので、牌IDや位置で比べると、別の牌を押したのに外れてしまう）
            int existing = _mulliganPicks.FindIndex(p => p.Slot == slotRt);
            if (existing >= 0)
            {
                TintSlot(_mulliganPicks[existing].Slot, Color.white);
                _mulliganPicks.RemoveAt(existing);
                return true;
            }
            if (_mulliganPicks.Count >= MulliganGiveCount) return true;

            // 山の位置を決める。押した牌が覚えている位置が合っていればそれを使い、
            // 合わなければ、同じ牌IDの位置のうち、まだ選んでいない所を使う（同じIDの牌はどれを出しても同じ）
            int wallIndex = -1;
            var clicked = slotRt != null ? slotRt.GetComponent<TileInteraction>() : null;
            if (clicked != null) wallIndex = clicked.WallIndex;
            bool usable = wallIndex >= 0 && wallIndex < wallTiles.Count && wallTiles[wallIndex] == tileId
                && !_mulliganPicks.Exists(p => p.WallIndex == wallIndex);
            if (!usable)
            {
                wallIndex = -1;
                for (int i = 0; i < wallTiles.Count; i++)
                {
                    if (wallTiles[i] != tileId || _mulliganPicks.Exists(p => p.WallIndex == i)) continue;
                    wallIndex = i;
                    break;
                }
            }
            if (wallIndex < 0)
            {
                Debug.LogWarning("[Mulligan] 選んだ牌が山に見つかりません: " + tileId);
                return true;
            }

            _mulliganPicks.Add(new MulliganPick { TileId = tileId, WallIndex = wallIndex, Slot = slotRt });
            TintSlot(slotRt, MulliganPickTint);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayPickTileSE();

            if (_mulliganPicks.Count == MulliganGiveCount) SendMulliganPicks();
            return true;
        }

        private void SendMulliganPicks()
        {
            IsMulliganSelection = false;
            HideMulliganUI();

            var indexes = new List<int>();
            _mulliganSentSlots.Clear();
            foreach (var pick in _mulliganPicks)
            {
                indexes.Add(pick.WallIndex);
                _mulliganSentSlots[pick.WallIndex] = pick.Slot;
            }
            ClearMulliganPicks();

            if (uiManager.IsTutorialMode)
            {
                TutorialMulliganPicked?.Invoke(indexes);
                return;
            }
            CancelPendingSkillRequest();
            pendingMulligan = uiManager.BeginTransition("mulligan-request");
            uiManager.SendActionToServer("skill", new ActionPayload { skill_type = "mulligan", target_hand_indexes = indexes });
        }

        internal IEnumerator ApplyTutorialMulliganChoice(MulliganSelectedData result)
        {
            if (uiManager == null || !uiManager.IsTutorialMode) yield break;
            var ownedLock = uiManager.BeginTransition("skill:mulligan-choice");
            skillTransitions.Add(ownedLock);
            try { yield return ApplyMulliganSelected(result, ownedLock, animate: true); }
            finally
            {
                _mulliganSentSlots.Clear();
                skillTransitions.Remove(ownedLock);
                ownedLock.Dispose();
            }
            uiManager.RefreshBoardAfterTransition();
        }

        internal static List<int> ArrangeTutorialMulliganChoices(List<int> choices) => ArrangeMulliganChoices(choices);

        /// <summary>選びかけを捨てる。色も戻す。</summary>
        private void ClearMulliganPicks()
        {
            foreach (var pick in _mulliganPicks) TintSlot(pick.Slot, Color.white);
            _mulliganPicks.Clear();
        }

        private static void TintSlot(RectTransform slot, Color color)
        {
            if (slot == null) return;
            foreach (var image in slot.GetComponentsInChildren<Image>(true))
            {
                if (image != null) image.color = new Color(color.r, color.g, color.b, image.color.a);
            }
        }

        /// <summary>
        /// skill_accepted が届いた。牌交換の候補が入っていれば、残す3枚を選ぶ画面を順番待ちへ入れる。
        /// </summary>
        public void HandleSkillAccepted(SkillAcceptedData data)
        {
            if (data == null || data.mulliganOptions == null) return;
            if (uiManager == null || uiManager.IsTutorialMode) return;

            // 受け付けられた。返事待ちのロックはここで外す（カットインの skill_casted が本人に来ない場合に備える）
            pendingMulligan?.Dispose();
            pendingMulligan = null;

            StartCoroutine(EnqueueMulliganChoice(data.mulliganOptions));
        }

        private IEnumerator EnqueueMulliganChoice(MulliganOptionsData options)
        {
            // 自分の牌交換のカットイン（skill_casted）が先に列へ入るのを、少しだけ待つ。
            // 届く順は決まっていないので、もう届いていれば待たない
            float waited = 0f;
            while (waited < 0.35f && Time.unscaledTime - _ownMulliganCastSeenAt > 2f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            var queue = Effects.EffectQueue.Instance;
            if (queue == null) { StartCoroutine(MulliganChoiceRoutine(options)); yield break; }
            queue.Enqueue("mulligan-choice", () => RunQueuedMulliganChoice(options));
        }

        private IEnumerator RunQueuedMulliganChoice(MulliganOptionsData options)
        {
            if (this == null || !isActiveAndEnabled) yield break;

            int generation = _skillRunGeneration;
            bool done = false;
            StartCoroutine(RunThenFlag(MulliganChoiceRoutine(options), () => done = true));
            while (!done && this != null && isActiveAndEnabled && generation == _skillRunGeneration)
            {
                yield return null;
            }
            if (!done)
            {
                // 打ち切られた。画面を残さない
                var ui = FindFirstObjectByType<MulliganChoiceUI>(FindObjectsInactive.Include);
                if (ui != null) ui.Hide();
                _awaitingMulliganSelected = false;
            }
        }

        private IEnumerator MulliganChoiceRoutine(MulliganOptionsData options)
        {
            var ownedLock = uiManager.BeginTransition("skill:mulligan-choice");
            skillTransitions.Add(ownedLock);
            bool finished = false;
            try
            {
                MulliganChoiceUI ui = MulliganChoiceUI.FindOrCreate();
                List<int> decided = null;
                ui.Show(options.options, uiManager.TileResourceManager, picked => decided = picked);

                // **選ぶまで待つ。** 選び終えるまで、サーバーは手牌の確定を受け付けない
                while (decided == null && ownedLock.IsActive) yield return null;
                if (!ownedLock.IsActive) { ui.Hide(); yield break; }

                _mulliganSelected = null;
                _awaitingMulliganSelected = true;
                uiManager.SendActionToServer("mulligan_select", new ActionPayload { choices = ArrangeMulliganChoices(decided) });

                float waited = 0f;
                while (_mulliganSelected == null && waited < MulliganSelectedTimeout && ownedLock.IsActive)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                _awaitingMulliganSelected = false;
                if (!ownedLock.IsActive) yield break;

                if (_mulliganSelected == null)
                {
                    Debug.LogWarning("[Mulligan] mulligan_selected が " + MulliganSelectedTimeout + " 秒届きませんでした。演出なしで進めます");
                }
                else
                {
                    MulliganSelectedData result = _mulliganSelected;
                    _mulliganSelected = null;
                    yield return ApplyMulliganSelected(result, ownedLock, animate: true);
                }
                finished = ownedLock.IsActive;
            }
            finally
            {
                _awaitingMulliganSelected = false;
                _mulliganSentSlots.Clear();
                skillTransitions.Remove(ownedLock);
                ownedLock.Dispose();
            }
            if (finished) uiManager.RefreshBoardAfterTransition();
        }

        /// <summary>
        /// 選んだ候補を、送る並び（出した3か所へ入る順）に直す。
        /// サーバーは choices を先頭から順に、出した位置へ入れる。**出した牌を残すときは、元の位置に戻す。**
        /// そうしないと、残しただけの牌まで場所が入れ替わって見える。
        /// </summary>
        private static List<int> ArrangeMulliganChoices(List<int> decided)
        {
            var result = new List<int> { -1, -1, -1 };
            var rest = new List<int>();
            foreach (int choice in decided)
            {
                if (choice >= 0 && choice < MulliganGiveCount && result[choice] < 0) result[choice] = choice;
                else rest.Add(choice);
            }
            int next = 0;
            for (int i = 0; i < result.Count; i++)
            {
                if (result[i] >= 0) continue;
                result[i] = next < rest.Count ? rest[next++] : i;
            }
            return result;
        }

        /// <summary>mulligan_selected が届いた。選ぶ画面が待っていれば渡し、そうでなければその場で山だけ入れ替える。</summary>
        public void HandleMulliganSelected(MulliganSelectedData data)
        {
            if (data == null) return;
            if (_awaitingMulliganSelected)
            {
                _mulliganSelected = data;
                return;
            }
            // 待っていないのに届いた（待つのをあきらめたあと等）。盤面だけは合わせる
            StartCoroutine(ApplyMulliganSelected(data, null, animate: false));
            if (uiManager != null) uiManager.RefreshBoardAfterTransition();
        }

        /// <summary>山と手牌を入れ替える。animate なら、替わった牌ごとに交換の演出を出す。</summary>
        private IEnumerator ApplyMulliganSelected(MulliganSelectedData data, TransitionLockSet.Lease ownedLock, bool animate)
        {
            var state = BoardStateManager.Instance;
            if (state == null || data.changes == null) yield break;

            uiManager.ClearSelection();

            var swaps = new List<MulliganChangeData>();
            foreach (var change in data.changes)
            {
                if (change == null || change.oldTile == change.newTile) continue;   // 元のままの所

                // サーバーが入れ替えた「位置」を正として山を合わせる（牌IDは山に複数あるので、IDでは探さない）。
                // 手牌に入っている位置なら、手牌の側もそこで持ち替わる。
                // **ここで手牌を値で探して持ち替えてはいけない。** 山の牌を出したのに、手牌の同じ牌まで替わる
                if (!state.ReplaceWallTileAt(change.wallIndex, change.newTile))
                {
                    int wallIdx = state.CurrentWallTiles.IndexOf(change.oldTile);
                    if (wallIdx >= 0) state.CurrentWallTiles[wallIdx] = change.newTile;
                    else if (state.CurrentHandTiles.Remove(change.oldTile))
                    {
                        state.CurrentHandTiles.Add(change.newTile);
                        state.SortTileIds(state.CurrentHandTiles);
                    }
                }
                swaps.Add(change);
            }

            // 念のため、サーバーの山と見比べる。ずれていたら、次の交換で別の牌を指すことになる
            if (data.wall != null && state.OriginalWallTiles != null && data.wall.Count == state.OriginalWallTiles.Count)
            {
                for (int i = 0; i < data.wall.Count; i++)
                {
                    if (state.OriginalWallTiles[i] == data.wall[i]) continue;
                    Debug.LogWarning("[Mulligan] 山の位置 " + i + " がサーバーと違います（手元 " + state.OriginalWallTiles[i]
                        + " / サーバー " + data.wall[i] + "）。サーバーに合わせます");
                    state.ReplaceWallTileAt(i, data.wall[i]);
                }
            }

            if (!animate || _mulliganSwapAnimator == null) yield break;

            // 替わった牌は**一度に**入れ替える（2026-10-22 ユーザー指示「3枚指定した時、一気に3枚交換してほしい」）。
            // 以前は1枚ずつ演出を流していた
            var animated = new List<MulliganSwapAnimator.Swap>();
            foreach (var change in swaps)
            {
                // 置き場は**いまの盤面から山の位置で探す。** 送ったときに覚えた置き場は、
                // その後に盤面が作り直されていると別の牌に使い回されていて、そこを書き換えると
                // 位置と値の合わない牌（触れない牌）ができる
                RectTransform slot = FindSlotByWallIndex(change.wallIndex);
                if (slot == null)
                {
                    RectTransform sent;
                    if (_mulliganSentSlots.TryGetValue(change.wallIndex, out sent) && sent != null
                        && SlotWallIndex(sent) == change.wallIndex) slot = sent;
                }
                animated.Add(new MulliganSwapAnimator.Swap(change.oldTile, change.newTile, slot));
            }
            yield return _mulliganSwapAnimator.PlayRoutine(animated);
        }

        /// <summary>山の位置 <paramref name="wallIndex"/> の牌を、いま山か手牌に並んでいる置き場から探す。</summary>
        private RectTransform FindSlotByWallIndex(int wallIndex)
        {
            if (wallIndex < 0) return null;
            if (uiManager.WallUI != null)
            {
                foreach (var slot in uiManager.WallUI.GetWallSlots())
                {
                    if (slot != null && SlotWallIndex(slot) == wallIndex) return slot;
                }
            }
            if (uiManager.HandUI != null)
            {
                foreach (var slot in uiManager.HandUI.GetHandSlots())
                {
                    if (slot != null && SlotWallIndex(slot) == wallIndex) return slot;
                }
            }
            return null;
        }

        private static int SlotWallIndex(RectTransform slot)
        {
            var tile = slot != null ? slot.GetComponent<TileInteraction>() : null;
            return tile != null ? tile.WallIndex : -1;
        }
    }
}
