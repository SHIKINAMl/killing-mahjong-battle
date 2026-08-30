using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.UI;
using KillingMahjong.EngineData;
using KillingMahjong.Common;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        // TutorialManager: 敵の能力デモ（実演・スキル発動演出・透視マーク・役表誘導）

        /// <summary>
        /// 手順⑱: 敵が能力を順に使ってみせる。
        ///
        /// チュートリアルはサーバーに繋がないので実際のスキル処理は走らせない。
        /// 能力欄を開いて対象の行を指し示し、その能力のSEと相手の反応で「使った」ことを見せる。
        /// プレイヤーは能力を使えない制約（IsAbilityUsableByPlayer）があるため、ここは実演のみ。
        /// </summary>
        private IEnumerator RunEnemyAbilityShowcase(TutorialRoundData data)
        {
            var ability = gameUIManager != null ? gameUIManager.AbilityUI : null;

            if (ability != null)
            {
                ability.gameObject.SetActive(true);

                // 非アクティブから有効化した直後は AbilityUI の Start() がまだ走っていない。
                // 先に開くとウィンドウ位置の初期化と開く演出がぶつかるので1フレーム待つ。
                yield return null;

                ability.OpenWindow();

                // 実演中は押しても何も起きないようにする。
                // 押せてしまうと DialogueUI がチュートリアルのセリフを上書きし、
                // 送りボタン待ちのまま進めなくなる。
                ability.IsDisplayOnly = true;

                // 行が生成されてレイアウトが確定するまでさらに1フレーム待つ
                yield return null;
            }

            var showcases = data.abilityShowcases;
            if (showcases == null || showcases.Count == 0)
            {
                // 台本に能力が並んでいない場合は、従来どおり軽く見せるだけにする
                yield return new WaitForSeconds(1.0f);
            }
            else
            {
                foreach (var showcase in showcases)
                {
                    if (showcase == null || string.IsNullOrEmpty(showcase.skillType)) continue;

                    // どの能力の話かを矢印で指しながら説明する。
                    // マスクは使わないこと。穴の外側のクリックを全て食べるので、
                    // 出したままセリフ待ちに入ると送りボタンが押せなくなる。
                    RectTransform itemRt = ability != null
                        ? ability.GetAbilityItemRect(showcase.skillType)
                        : null;
                    if (itemRt != null) GuideTo(itemRt, useMask: false);

                    yield return StartCoroutine(PlayLines(showcase.beforeLines));

                    ClearGuide();
                    if (ability != null) ability.CloseWindow(false);

                    // ここから実際の発動。本編と同じ手順を踏む。
                    yield return StartCoroutine(RunEnemySkillActivation(showcase));

                    yield return StartCoroutine(PlayLines(showcase.afterLines));

                    // 次の能力の説明のために開き直す
                    if (ability != null && showcase != showcases[showcases.Count - 1])
                    {
                        ability.OpenWindow();
                        yield return null;
                    }
                }
            }

            ClearGuide();
            if (ability != null)
            {
                ability.IsDisplayOnly = false;
                ability.CloseWindow(false);
            }
        }

        /// <summary>
        /// 敵が能力を1つ実際に発動する。GameUISkillController が本編で行う手順に合わせている。
        /// カットイン → コスト（血）の支払い → 能力ごとの効果、の順。
        /// </summary>
        private IEnumerator RunEnemySkillActivation(TutorialAbilityShowcase showcase)
        {
            string skillName = SkillNames.GetDisplayName(showcase.skillType);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySkillSE(showcase.skillType);

            // 1. カットイン演出（立ち絵＋巨大テキスト）
            var phaseUI = gameUIManager != null ? gameUIManager.PhaseTransitionUI : null;
            if (phaseUI != null)
            {
                // SetupBoard がフェーズ演出を出さないよう PhaseTransitionUI を無効化している。
                // 無効なままだと内部の StartCoroutine が失敗し、
                // 「Coroutine couldn't be started because the game object is inactive」で進行が止まる。
                // カットインの間だけ有効化し、終わったら元に戻す。
                bool wasInactive = !phaseUI.gameObject.activeSelf;
                if (wasInactive)
                {
                    phaseUI.gameObject.SetActive(true);
                    // Start() で Canvas の sortingOrder を設定しているので、走らせてから使う
                    yield return null;
                }

                var cData = gameUIManager.EnemyInfoUI != null
                    ? gameUIManager.EnemyInfoUI.CurrentCharacterData
                    : null;

                yield return phaseUI.PlaySkillCutinAnimationRoutine(
                    skillName, isLocalPlayer: false, characterData: cData, duration: 2.0f);

                if (wasInactive) phaseUI.gameObject.SetActive(false);
            }
            else
            {
                yield return new WaitForSeconds(1.0f);
            }

            // 2. コストの支払い。能力は血を削って使うものなので、敵のHPも実際に減らす。
            int cost = GameRules.GetSkillCost(showcase.skillType, 0);
            if (cost > 0 && cost < 99999)
            {
                _enemyHp = Mathf.Max(0, _enemyHp - cost);
                ApplyHpToUI();

                if (gameUIManager != null && gameUIManager.EnemyInfoUI != null)
                    gameUIManager.EnemyInfoUI.PlayBounceAnimation(0.4f);

                // 体力が減る様子を見せるためのタメ
                yield return new WaitForSeconds(1.0f);
            }

            // 3. 能力ごとの効果
            if (showcase.skillType == SkillNames.Perspective)
            {
                ApplyPerspectiveMarks(showcase.perspectiveTileCount);
            }

            if (showcase.skillType == SkillNames.BoostHand && !string.IsNullOrEmpty(showcase.boostYakuName))
            {
                // 役強化は結果が役一覧に残る。直後の手順⑳でプレイヤーに確認させる。
                var board = BoardStateManager.Instance;
                if (board != null)
                {
                    if (board.EnemyBoostHandBonus == null)
                        board.EnemyBoostHandBonus = new Dictionary<string, int>();
                    board.EnemyBoostHandBonus[showcase.boostYakuName] = showcase.boostHan;

                    if (gameUIManager != null && gameUIManager.YakuListUI != null)
                        gameUIManager.YakuListUI.UpdateBoostData(
                            board.LocalBoostHandBonus, board.EnemyBoostHandBonus);
                }
            }

            yield return new WaitForSeconds(abilityShowcaseInterval);
        }

        /// <summary>今この局で透視マークを立てた牌。局が変わるときに消すために覚えておく。</summary>
        private readonly List<TileVisual> _perspectiveMarked = new List<TileVisual>();

        /// <summary>
        /// 敵の『透視』の効果。プレイヤーの牌のうち指定枚数に透視マークを出す。
        ///
        /// 能力の実演は手牌を組む前に入るため、その時点では手牌が空のことがある。
        /// その場合はプレイヤーが選ぶ対象である山牌に付ける。
        /// </summary>
        private void ApplyPerspectiveMarks(int count)
        {
            if (count <= 0 || gameUIManager == null) return;

            var candidates = new List<TileVisual>();

            if (gameUIManager.HandUI != null) CollectTileVisuals(gameUIManager.HandUI.GetHandSlots(), candidates);
            if (candidates.Count < count && gameUIManager.WallUI != null)
                CollectTileVisuals(gameUIManager.WallUI.GetWallSlots(), candidates);

            if (candidates.Count == 0)
            {
                Debug.LogWarning("[TutorialManager] 透視マークを付ける牌が見つかりませんでした。");
                return;
            }

            // 端に固まらないよう、候補全体に散らして選ぶ
            int picked = Mathf.Min(count, candidates.Count);
            int step = Mathf.Max(1, candidates.Count / picked);

            for (int i = 0; i < picked; i++)
            {
                var visual = candidates[Mathf.Min(i * step, candidates.Count - 1)];
                if (visual == null || _perspectiveMarked.Contains(visual)) continue;

                visual.SetExposed(true);
                _perspectiveMarked.Add(visual);
            }
        }

        private static void CollectTileVisuals<T>(IEnumerable<T> slots, List<TileVisual> into) where T : Component
        {
            if (slots == null) return;
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                var visual = slot.GetComponent<TileVisual>();
                if (visual != null && !into.Contains(visual)) into.Add(visual);
            }
        }

        /// <summary>局が切り替わるときに透視マークを消す。プールの牌に状態が残るのを防ぐ。</summary>
        private void ClearPerspectiveMarks()
        {
            foreach (var visual in _perspectiveMarked)
            {
                if (visual != null) visual.SetExposed(false);
            }
            _perspectiveMarked.Clear();
        }

        /// <summary>
        /// 手順⑳: 役一覧（役表）を実際に開かせる。
        /// 開くボタンが見つからない場合は誘導を諦めて先へ進む（進行が止まらないようにする）。
        /// </summary>
        private IEnumerator RunYakuListGuide(TutorialRoundData data)
        {
            var yakuList = gameUIManager != null ? gameUIManager.YakuListUI : null;
            if (yakuList == null) yield break;

            yakuList.gameObject.SetActive(true);

            // 画面右上の「役一覧」画像そのものを指す（開くボタン単体だと何を見ればよいか分からない）
            RectTransform guideRt = yakuList.GuideTargetRect;
            if (guideRt == null)
            {
                Debug.LogWarning("[TutorialManager] YakuListUI の開くボタンが未設定です。役一覧への誘導をスキップします。");
                yield break;
            }

            GuideTo(guideRt);

            // 開くまで待つ。すでに開いていればそのまま進む。
            yield return new WaitUntil(() => yakuList.IsOpen || _aborted);
            ClearGuide();

            if (_aborted) yield break;

            yield return StartCoroutine(PlayLines(data.onYakuListOpenedLines));

            yakuList.CloseYakuList();
        }

    }
}
