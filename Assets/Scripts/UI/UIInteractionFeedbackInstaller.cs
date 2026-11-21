using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>動的な選択肢にも共通の入力反応を付ける。情報画像・全画面の送り面には付けない。</summary>
    public sealed class UIInteractionFeedbackInstaller : MonoBehaviour
    {
        private Selectable[] buffer = new Selectable[128];
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartInstaller()
        {
            var go = new GameObject("UIInteractionFeedbackInstaller");
            DontDestroyOnLoad(go); go.AddComponent<UIInteractionFeedbackInstaller>();
        }

        public static bool ShouldAttach(Selectable target)
        {
            if (target == null || !(target.transform is RectTransform)) return false;
            string name = target.name;
            if (name == "DialogueAdvanceCatcher" || name == "Advance" || name == "PaperUI"
                || name == "TitleClickToTutorial" || name == "TitleClickTarget" || name == "Scrim"
                || name == "Backdrop" || name == "CloseCatcher") return false;
            // 全画面クリックで文字送りする透明面に「枠付きボタン」を描かない。
            var rect = (RectTransform)target.transform;
            if (rect.rect.width > 550 && rect.rect.height > 350 && target is Button) return false;
            return true;
        }

        private void Update()
        {
            if (Selectable.allSelectableCount > buffer.Length)
                buffer = new Selectable[Mathf.NextPowerOfTwo(Selectable.allSelectableCount)];
            int count = Selectable.AllSelectablesNoAlloc(buffer);
            for (int i = 0; i < count; i++)
            {
                var target = buffer[i];
                if (ShouldAttach(target) && target.GetComponent<UIInteractionFeedback>() == null)
                {
                    // ここで付ける枠は、実行時にできたボタン（離席の知らせの「続ける」など）の物で、シーンに置く物ではない。
                    // 「シーンに無かったので作りました」の知らせに混ぜない（2026-10-23。離席の知らせのあと、
                    // 次のシーンのタイトルの知らせに枠の名前が出ていた）。シーンにあるボタンの枠は UIInteractionFeedbackBaker で置く
                    int mark = SceneFirst.CreatedCount;
                    UIInteractionFeedback.Attach(target.gameObject);
                    SceneFirst.ForgetSince(mark);
                }
            }
        }
    }
}
