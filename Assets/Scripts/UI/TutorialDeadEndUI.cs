using System.Collections;
using TMPro;
using UnityEngine;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    /// <summary>台本のDEADEND No1。表示だけを行い、HP・進行保存・戦績には触れない。</summary>
    public sealed class TutorialDeadEndUI : MonoBehaviour
    {
        public const string EffectId = "tutorial.dead_end_1";
        public EndingSequenceUI Sequence { get; private set; }
        public bool Returned { get; private set; }
        private RedDefeatPrototypeUI defeat;

        public static TutorialDeadEndUI Create(Transform parent)
        {
            var root = new GameObject("TutorialDeadEnd", typeof(RectTransform));
            if (parent != null) root.transform.SetParent(parent, false);
            return root.AddComponent<TutorialDeadEndUI>();
        }

        public IEnumerator Play(TMP_FontAsset font, bool automatic = false)
        {
            Returned = false;
            // 図で指定された既存の赤い敗北演出を再利用する。タイトル遷移はコールバックで置き換える。
            bool finished = false;
            defeat = RedDefeatPrototypeUI.Play(() => finished = true,
                AudioManager.Instance != null && AudioManager.Instance.AudioEnabled);
            if (defeat == null) throw new System.InvalidOperationException("チュートリアルの敗北演出素材がありません。");
            defeat.transform.SetParent(transform, false);
            yield return new WaitUntil(() => finished);
            // 赤い演出の黒幕は結果画面と同じ重なり順なので、残すと本文・結果・クレジットが隠れる
            Destroy(defeat.gameObject);
            defeat = null;
            Managers.Tutorial.TutorialAudioDirector.ResetVisuals();
            var ending = new GameObject("TutorialEndingSequence", typeof(RectTransform));
            ending.transform.SetParent(transform, false);
            Sequence = ending.AddComponent<EndingSequenceUI>();
            Sequence.Show(new VictoryConfig {
                victoryType = VictoryType.NormalDefeat,
                resultTitle = "DEAD END",
                endingName = "No1：\"安全な\"ゲーム",
                // No1専用のエンディング絵は未提供。未指定時は共通画面の黒背景を使う。
                dialoguePages = new[] {
                    "目の前が真っ赤に染まる",
                    "なにも見えない。体はぴくりともうごきやしない",
                    "そのうちこのアタマもなくなってしまうのだろう",
                    "取られてしまった僕の手足みたいに",
                }
            }, font, () => Returned = true, "直前の選択肢へ戻る▼", true);
            while (!Returned)
            {
                if (automatic)
                {
                    yield return new WaitForSecondsRealtime(1.5f);
                    Sequence.Advance();
                }
                else yield return null;
            }
        }

        public void Close()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
