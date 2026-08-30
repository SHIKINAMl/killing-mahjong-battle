using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using KillingMahjong.Managers;

namespace KillingMahjong.EditorTools
{
    /// <summary>
    /// チュートリアル（＝オープニングの会話）の台本を編集する専用ウィンドウ。
    ///
    /// **編集するのは `Assets/Resources/Tutorial/TutorialScenario.asset` だけ。**
    /// OpeningScene の `OpeningManager` にある `TutorialManager.scenario` にこの asset が
    /// 割り当たっていて、実際に画面へ出るセリフはここが正。
    ///
    /// **`Tools/チュートリアル/台本のセリフを TSV で編集` は現状どこにも効いていない。**
    /// あちらの書き込み先 `Resources/TutorialLines.asset` は存在せず、仮に作っても
    /// 適用されるのは `TutorialScenario.BuildDefault()` の中だけ。
    /// `scenario` が asset に設定されている限り `BuildDefault()` は呼ばれない。
    /// 混乱の元なので、このウィンドウから触る分にはあちらを気にしなくてよい。
    ///
    /// **なぜ Inspector で済ませないのか。**
    /// 1局あたり `List&lt;TutorialLine&gt;` が12本＋能力実演の入れ子まであり、
    /// フィールド名（`onHandFilledLines` など）を見ても、どの場面で喋るのかが分からない。
    /// ここでは**実際に流れる順**に並べ、日本語の見出しと「いつ出るか」を添える。
    /// </summary>
    public partial class TutorialScenarioEditorWindow : EditorWindow
    {
        public const string AssetPath = "Assets/Resources/Tutorial/TutorialScenario.asset";

        /// <summary>
        /// 局の中の1つのセリフ枠。
        ///
        /// **アクセサをラムダで持つのは、フィールド名の文字列で引かないため。**
        /// 文字列だとリネームに気づけず、黙って空の枠が並ぶ。ラムダならコンパイルが落ちる。
        /// `fieldName` は TSV 側の ID（`r0.introLines[2]`）と突き合わせる用に持っているだけ。
        /// </summary>
        private class Slot
        {
            public string fieldName;
            public string title;
            public string help;
            public Func<TutorialRoundData, List<TutorialLine>> get;
            public Action<TutorialRoundData, List<TutorialLine>> set;
        }

        /// <summary>
        /// `TutorialManager.RunRound()` が実際に再生する順番。
        /// **並べ替えるときは RunRound を読んでから。** ここの順番は見た目の都合ではなく、
        /// 「どの順で画面に出るか」を表している。
        /// </summary>
        private static readonly Slot[] Slots =
        {
            new Slot {
                fieldName = "introLines", title = "導入",
                help = "局のはじめ。女の子とセリフだけの状態で流れ、途中で盤面が出る（revealBoardAfterLineIndex 行目のあと）",
                get = r => r.introLines, set = (r, v) => r.introLines = v },
            new Slot {
                fieldName = "abilityIntroLines", title = "能力の前ふり",
                help = "enemyUsesAbility が ON の局だけ。能力の実演に入る前",
                get = r => r.abilityIntroLines, set = (r, v) => r.abilityIntroLines = v },
            new Slot {
                fieldName = "abilityExplainLines", title = "能力の説明",
                help = "enemyUsesAbility が ON の局だけ。実演をすべて見せたあと",
                get = r => r.abilityExplainLines, set = (r, v) => r.abilityExplainLines = v },
            new Slot {
                fieldName = "enhanceExplainLines", title = "強化の説明",
                help = "能力の説明のすぐあと",
                get = r => r.enhanceExplainLines, set = (r, v) => r.enhanceExplainLines = v },
            new Slot {
                fieldName = "onYakuListOpenedLines", title = "役一覧を開いたとき",
                help = "guideToYakuList が ON の局だけ。プレイヤーが役一覧を開いた瞬間",
                get = r => r.onYakuListOpenedLines, set = (r, v) => r.onYakuListOpenedLines = v },
            new Slot {
                fieldName = "onHandFilledLines", title = "手牌が13枚そろったとき",
                help = "allowManualHandSelection が ON の局だけ。自力で満貫を組めた場合は下の枠が代わりに流れる",
                get = r => r.onHandFilledLines, set = (r, v) => r.onHandFilledLines = v },
            new Slot {
                fieldName = "onSelfManganLines", title = "自力で満貫を組めたとき",
                help = "13枚そろった時点で台本どおりの満貫手ができていた場合。空なら上の枠が流れる",
                get = r => r.onSelfManganLines, set = (r, v) => r.onSelfManganLines = v },
            new Slot {
                fieldName = "beforeBetLines", title = "賭け金の前",
                help = "賭け金フェイズに入る直前。前局が流局だった局では流れない（下の枠になる）",
                get = r => r.beforeBetLines, set = (r, v) => r.beforeBetLines = v },
            new Slot {
                fieldName = "inheritedBetLines", title = "賭け金の持ち越し（流局の次の局）",
                help = "前局が流局のときだけ。賭け金は自動で引き継がれるので、プレイヤーは操作しない",
                get = r => r.inheritedBetLines, set = (r, v) => r.inheritedBetLines = v },
            new Slot {
                fieldName = "onBattleStartLines", title = "対局開始",
                help = "打牌フェイズに切り替わった直後",
                get = r => r.onBattleStartLines, set = (r, v) => r.onBattleStartLines = v },
            new Slot {
                fieldName = "beforeManualDiscardLines", title = "自分で打つ前",
                help = "対局中。プレイヤーに手動で打牌させる直前",
                get = r => r.beforeManualDiscardLines, set = (r, v) => r.beforeManualDiscardLines = v },
            new Slot {
                fieldName = "outroLines", title = "決着後",
                help = "局の決着（ロン／流局）の演出が終わったあと。局の最後",
                get = r => r.outroLines, set = (r, v) => r.outroLines = v },
        };

        /// <summary>
        /// この局でその枠が**流れない**なら理由を返す。流れるなら null。
        ///
        /// **`TutorialManager` の分岐をそのまま写したもの。** 向こうを直したらここも直す。
        /// 写し元: `RunRound`(281-439) / `RunBattle`(814-) / `RunYakuListGuide`(1298-)。
        /// 嘘を書くと「赤いのに実は流れる」＝書いたセリフが消えたように見えるので、
        /// **迷ったら「流れる」側に倒す**（赤くしない）。
        /// </summary>
        private static string GetSkipReason(string field, TutorialRoundData r, TutorialScenario s, int roundIndex)
        {
            switch (field)
            {
                case "abilityIntroLines":
                case "abilityExplainLines":
                case "enhanceExplainLines":
                    return r.enemyUsesAbility ? null : "この局は能力を使わない局です（能力を使う: いいえ）。";

                case "onYakuListOpenedLines":
                    if (!r.enemyUsesAbility) return "この局は能力を使わない局です（能力を使う: いいえ）。";
                    return r.guideToYakuList ? null : "この局は役一覧へ誘導しません（役一覧へ誘導: いいえ）。";

                case "onHandFilledLines":
                case "onSelfManganLines":
                    return r.allowManualHandSelection
                        ? null
                        : "この局はプレイヤーに手牌を組ませません（手動で組ませる: いいえ）。";

                case "beforeBetLines":
                    return PrevRoundWasDraw(s, roundIndex)
                        ? "前の局が流局なので、この局は賭け金が持ち越されます。代わりに「賭け金の持ち越し」が流れます。"
                        : null;

                case "inheritedBetLines":
                    return PrevRoundWasDraw(s, roundIndex)
                        ? null
                        : "前の局は流局ではないので、この局は普通に賭け金を決めます。代わりに「賭け金の前」が流れます。";

                case "beforeManualDiscardLines":
                {
                    // RunBattle: autoTurns = Clamp(autoDiscardTurns, 0, turns) とし、
                    // autoTurns > 0 かつ turn == autoTurns + 1 が存在するときだけ流れる
                    int turns = r.enemyDiscardBaseIds == null ? 0 : r.enemyDiscardBaseIds.Count;
                    int autoTurns = Mathf.Clamp(r.autoDiscardTurns, 0, turns);
                    if (autoTurns <= 0)
                        return "この局は最初からプレイヤーが自分で打ちます（自動で打つ巡目が 0）。境目が無いので流れません。";
                    if (autoTurns >= turns)
                        return "この局は最後まで自動で打ちます（自動で打つ巡目 " + autoTurns + " ＝ 全 " + turns + " 巡）。手で打つ場面が来ません。";
                    return null;
                }

                default:
                    return null;   // 導入 / 対局開始 / 決着後 は必ず流れる
            }
        }

        /// <summary>
        /// 前の局が流局だったか。`TutorialManager._prevRoundWasDraw` と同じ判定。
        /// 第1局は前局が無いので false（普通に賭け金を決める）。
        /// </summary>
        private static bool PrevRoundWasDraw(TutorialScenario s, int roundIndex)
        {
            if (s == null || s.rounds == null) return false;
            int prev = roundIndex - 1;
            if (prev < 0 || prev >= s.rounds.Count) return false;
            var p = s.rounds[prev];
            return p != null && p.outcome == TutorialOutcome.Draw;
        }

        /// <summary>
        /// 空にすると**コードに書かれた既定のセリフ**が出る枠。空＝無言ではないので明示する。
        /// 写し元: `ResolveHandFilledLines` / `ResolveSelfManganLines` / `ResolveInheritedBetLines`。
        /// </summary>
        private static bool HasDefaultFallback(string field)
        {
            return field == "onHandFilledLines"
                || field == "onSelfManganLines"
                || field == "inheritedBetLines";
        }

        private TutorialScenario _asset;
        private int _roundIndex;                 // -1 = 全局終了後（endingLines）
        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private readonly HashSet<string> _collapsed = new HashSet<string>();

        [MenuItem("Tools/チュートリアル/会話とイベントの編集")]
        public static void Open()
        {
            var w = GetWindow<TutorialScenarioEditorWindow>("チュートリアル台本");
            w.minSize = new Vector2(760f, 420f);
            w.Load();
            w.Show();
        }

        private void OnEnable()
        {
            if (_asset == null) Load();
        }

        private void Load()
        {
            _asset = AssetDatabase.LoadAssetAtPath<TutorialScenario>(AssetPath);
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_asset == null)
            {
                EditorGUILayout.HelpBox(
                    "台本アセットが見つかりません:\n" + AssetPath,
                    MessageType.Error);
                if (GUILayout.Button("読み込み直す")) Load();
                return;
            }

            if (_asset.rounds == null) _asset.rounds = new List<TutorialRoundData>();

            EditorGUILayout.BeginHorizontal();
            DrawRoundList();
            DrawRoundBody();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label(AssetPath, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("アセットを選択", EditorStyles.toolbarButton, GUILayout.Width(100f)))
            {
                Selection.activeObject = _asset;
                EditorGUIUtility.PingObject(_asset);
            }

            using (new EditorGUI.DisabledScope(_asset == null))
            {
                if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    EditorUtility.SetDirty(_asset);
                    AssetDatabase.SaveAssets();
                    ShowNotification(new GUIContent("保存しました"));
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "ここを直すと、そのまま OpeningScene の会話に反映されます（Play で確認できます）。\n" +
                "「Tools/チュートリアル/台本のセリフを TSV で編集」は現在この台本には効きません。混同しないでください。",
                MessageType.Info);
        }

    }
}
