using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        // 対局フェイズ①が終わったあとの締め（2026-09-24）。
        //
        // 出どころはフロー図（km-docs/tutorial/flow_20260927.drawio）の3枚目
        // 「流局＆能力について」。**2026-09-28 に能力の中身まで描かれた。**
        // ここが受け持つのは前半で、
        //
        //   対局フェイズ①終了 → 手牌選択フェイズ②へ → 初期非表示UI：能力ベル
        //   → 「とまぁこれがゲームの流れね」 → 「わかった？」 → 分岐
        //        はい　 → 「おっ後輩ちゃんできるねぇ」 → 「うーん…じゃあいらないと思うけど……ハイコレ」
        //        いいえ → 「そうだねぇ……気持ちわかるわー」 → 「ならとりあえず……ハイコレ！」
        //   → 左下に設定画面の画像を表示し、資料への入口を案内
        //   → 「ここに今までのチュートリアルがまとめてあるから困ったら見て」
        //
        // まで。**この続き（能力の紹介）は `TutorialManager.AbilityIntro.cs`。**

        private static readonly List<TutorialLine> WrapUpLines = new List<TutorialLine>
        {
            new TutorialLine("とまぁこれがゲームの流れね"),
        };

        /// <summary>分岐の問いかけ。選ばせている間もこの吹き出しを出したままにする。</summary>
        private const string WrapUpQuestion = "「わかった？」";

        private static readonly List<TutorialLine> WrapUpUnderstoodLines = new List<TutorialLine>
        {
            new TutorialLine("おっ後輩ちゃんできるねぇ"),
            new TutorialLine("うーん…じゃあいらないと思うけど……ハイコレ"),
        };

        private static readonly List<TutorialLine> WrapUpConfusedLines = new List<TutorialLine>
        {
            new TutorialLine("そうだねぇ……気持ちわかるわー"),
            new TutorialLine("ならとりあえず……ハイコレ！"),
        };

        private static readonly List<TutorialLine> ArchiveClosingLines = new List<TutorialLine>
        {
            new TutorialLine("ここに今までのチュートリアルがまとめてあるから困ったら見て"),
        };

        /// <summary>能力ベルを一度でも出したか。出したら以降は出したままにする。</summary>
        private bool _abilityBellRevealed;

        /// <summary>第2局か。締めのくだりはここの頭で一度だけ流す。</summary>
        private bool IsSecondTutorialRound(TutorialRoundData data)
        {
            return _scenario != null && _scenario.rounds != null
                   && _scenario.rounds.Count > 1 && _scenario.rounds[1] == data;
        }

        /// <summary>
        /// 能力ベルの出し入れ。フロー図の「初期非表示UI：能力ベル」。
        /// まだ説明していないものを置いておくと、押してよいのか分からない。
        /// </summary>
        private void SetAbilityBellVisible(bool visible)
        {
            if (gameUIManager == null || gameUIManager.AbilityUI == null) return;

            // **入れ物ごと起こす。** `AbilityUI` の GameObject 自体を伏せている場所が
            // 6箇所あり（賭け・マッチング・ロン・局送りなど）、子のベルだけ
            // SetActive(true) にしても `activeInHierarchy` は false のまま。
            // 2026-09-28 に実機で詰まった。ベルが出ないのに枠だけ出て、
            // 押しようがないまま止まる。
            if (visible) gameUIManager.AbilityUI.gameObject.SetActive(true);

            gameUIManager.AbilityUI.SetBellVisible(visible);
        }

        private IEnumerator RunAfterFirstRoundWrapUp()
        {
            yield return StartCoroutine(PlayLines(WrapUpLines));

            // 「わかった？」。**見た目は冒頭の「麻雀とか知ってたっけ？」と同じにする。**
            // 問いかけを吹き出しに出したまま選ばせないと、答えずに読み飛ばせてしまう。
            int answer = -1;                       // 0=はい / 1=いいえ
            TMP_FontAsset font = BorrowJapaneseFont();
            Transform parent = BuildIntroCanvas(dim: false);
            if (parent != null)
            {
                if (dialogueUI != null)
                {
                    dialogueUI.gameObject.SetActive(true);
                    dialogueUI.ShowText(WrapUpQuestion);
                }

                BuildQuestionPanel(parent, font, onYes: () => answer = 0, onNo: () => answer = 1);
                yield return new WaitUntil(() => answer >= 0);
                CloseIntro();
            }

            yield return StartCoroutine(PlayLines(
                answer == 1 ? WrapUpConfusedLines : WrapUpUnderstoodLines));

            yield return StartCoroutine(RunTutorialArchiveGuide());

            // フロー図では「ここに今までの…」から「それじゃあ第二局目だね」へ
            // そのまま矢印が伸びている。**続けて能力の紹介に入る。**
            yield return StartCoroutine(RunAbilityIntroduction());
        }

        /// <summary>資料への入口を画像で案内する。設定を開く操作は要求しない。</summary>
        private IEnumerator RunTutorialArchiveGuide()
        {
            ClearGuide();
            var hint = ShowArchiveAccessHint();
            try
            {
                yield return StartCoroutine(PlayLines(ArchiveClosingLines));
            }
            finally
            {
                if (hint != null) Destroy(hint);
            }
        }

        private GameObject ShowArchiveAccessHint()
        {
            var texture = Resources.Load<Texture2D>("Tutorial/archive_access_hint");
            if (texture == null) return null;

            var root = new GameObject("TutorialArchiveAccessHint", typeof(RectTransform),
                typeof(UnityEngine.Canvas), typeof(UnityEngine.UI.CanvasScaler));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Common.UISortingOrders.TutorialIntro;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.matchWidthOrHeight = 0.5f;

            var picture = new GameObject("OptionsExample", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
            picture.transform.SetParent(root.transform, false);
            var rect = (RectTransform)picture.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(16f, 16f);
            rect.sizeDelta = new Vector2(320f, 320f * texture.height / texture.width);
            var image = picture.GetComponent<UnityEngine.UI.RawImage>();
            image.texture = texture;
            image.raycastTarget = false;

            var label = new GameObject("AccessLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(root.transform, false);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = Vector2.zero;
            labelRect.anchoredPosition = new Vector2(16f, 24f + rect.sizeDelta.y);
            labelRect.sizeDelta = new Vector2(320f, 40f);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.font = BorrowJapaneseFont();
            text.fontSize = 16f;
            text.text = "設定 → チュートリアル資料\n困ったときに読み返せます";
            text.color = new Color32(255, 244, 210, 255);
            text.raycastTarget = false;
            return root;
        }
    }
}