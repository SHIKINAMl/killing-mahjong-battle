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
    public class TutorialScenarioEditorWindow : EditorWindow
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

        private void DrawRoundList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(210f));
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            EditorGUILayout.LabelField("局", EditorStyles.boldLabel);

            for (int i = 0; i < _asset.rounds.Count; i++)
            {
                var r = _asset.rounds[i];
                string label = r == null ? "(空)" : string.IsNullOrEmpty(r.label) ? "第" + (i + 1) + "局" : r.label;
                int count = r == null ? 0 : CountLines(r);

                bool on = _roundIndex == i;
                if (GUILayout.Toggle(on, label + "  (" + count + ")", "Button") != on)
                {
                    _roundIndex = i;
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.Space();

            bool endOn = _roundIndex == -1;
            int endCount = _asset.endingLines == null ? 0 : _asset.endingLines.Count;
            if (GUILayout.Toggle(endOn, "全局終了後  (" + endCount + ")", "Button") != endOn)
            {
                _roundIndex = -1;
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static int CountLines(TutorialRoundData r)
        {
            int n = 0;
            foreach (var s in Slots)
            {
                var list = s.get(r);
                if (list != null) n += list.Count;
            }
            if (r.abilityShowcases != null)
            {
                foreach (var sc in r.abilityShowcases)
                {
                    if (sc == null) continue;
                    if (sc.beforeLines != null) n += sc.beforeLines.Count;
                    if (sc.afterLines != null) n += sc.afterLines.Count;
                }
            }
            return n;
        }

        private void DrawRoundBody()
        {
            EditorGUILayout.BeginVertical();
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            if (_roundIndex == -1)
            {
                EditorGUILayout.LabelField("全局終了後", EditorStyles.boldLabel);
                if (_asset.endingLines == null) _asset.endingLines = new List<TutorialLine>();
                DrawLineList("ending", "エンディング", "すべての局が終わったあと。このあとタイトルへ戻る",
                    _asset.endingLines, v => _asset.endingLines = v);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            if (_roundIndex < 0 || _roundIndex >= _asset.rounds.Count)
            {
                EditorGUILayout.HelpBox("左から局を選んでください。", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            var round = _asset.rounds[_roundIndex];
            if (round == null)
            {
                EditorGUILayout.HelpBox("この局のデータが空です。", MessageType.Warning);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField(round.label, EditorStyles.boldLabel);
            DrawRoundConditions(round);
            EditorGUILayout.Space();

            for (int i = 0; i < Slots.Length; i++)
            {
                var slot = Slots[i];
                var list = slot.get(round);
                if (list == null) { list = new List<TutorialLine>(); slot.set(round, list); }

                // 「導入」だけは盤面を出す位置が行番号で決まっている。
                // 見えないままだと行を足しただけで演出が変わるので、ここで一緒に見せる
                DrawLineList("r" + _roundIndex + "." + slot.fieldName, slot.title, slot.help,
                    list, v => slot.set(round, v),
                    slot.fieldName == "introLines" ? round : null,
                    GetSkipReason(slot.fieldName, round, _asset, _roundIndex),
                    HasDefaultFallback(slot.fieldName));

                // 能力の実演は「前ふり」のすぐあとに挟まる。順番どおりの位置で出す
                if (slot.fieldName == "abilityIntroLines") DrawShowcases(round);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// この局でどの枠が実際に流れるかを決めているフラグ。
        /// 読み取り専用で見せる。ここを変えると台本の構造が変わるので、第2段階の担当。
        /// </summary>
        private void DrawRoundConditions(TutorialRoundData round)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("結末: " + round.outcome, GUILayout.Width(150f));
                EditorGUILayout.LabelField("能力を使う: " + (round.enemyUsesAbility ? "はい" : "いいえ"), GUILayout.Width(130f));
                EditorGUILayout.LabelField("手動で組ませる: " + (round.allowManualHandSelection ? "はい" : "いいえ"), GUILayout.Width(150f));
                EditorGUILayout.LabelField("役一覧へ誘導: " + (round.guideToYakuList ? "はい" : "いいえ"));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawShowcases(TutorialRoundData round)
        {
            if (round.abilityShowcases == null || round.abilityShowcases.Count == 0) return;

            for (int s = 0; s < round.abilityShowcases.Count; s++)
            {
                var sc = round.abilityShowcases[s];
                if (sc == null) continue;

                string name = string.IsNullOrEmpty(sc.skillType) ? "能力" + (s + 1) : sc.skillType;
                string key = "r" + _roundIndex + ".abilityShowcases[" + s + "]";

                if (sc.beforeLines == null) sc.beforeLines = new List<TutorialLine>();
                if (sc.afterLines == null) sc.afterLines = new List<TutorialLine>();

                // 実演は能力の局でしか回らない（RunRound の enemyUsesAbility の中）
                string skip = round.enemyUsesAbility
                    ? null
                    : "この局は能力を使わない局です（能力を使う: いいえ）。";

                var scLocal = sc;
                DrawLineList(key + ".beforeLines", "　└ 実演" + (s + 1) + "「" + name + "」の前",
                    "この能力を実際に見せる直前", sc.beforeLines, v => scLocal.beforeLines = v, null, skip);
                DrawLineList(key + ".afterLines", "　└ 実演" + (s + 1) + "「" + name + "」のあと",
                    "この能力を見せ終わった直後", sc.afterLines, v => scLocal.afterLines = v, null, skip);
            }
        }

        /// <summary>
        /// セリフ1枠ぶんの描画。追加・削除・並べ替えまでここで行う。
        ///
        /// <paramref name="revealOwner"/> を渡すと「導入」用の追加表示が出る。
        /// `revealBoardAfterLineIndex` が行番号を指しているため、
        /// **行を足し引きすると盤面が出るタイミングが黙って動く。** それを画面に出すため。
        /// </summary>
        private void DrawLineList(string key, string title, string help,
                                  List<TutorialLine> lines, Action<List<TutorialLine>> writeBack,
                                  TutorialRoundData revealOwner = null,
                                  string skipReason = null, bool hasDefaultFallback = false)
        {
            bool collapsed = _collapsed.Contains(key);
            bool skipped = !string.IsNullOrEmpty(skipReason);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            var headStyle = EditorStyles.foldoutHeader;
            if (skipped)
            {
                headStyle = new GUIStyle(EditorStyles.foldoutHeader);
                var red = SkipColor;
                headStyle.normal.textColor = red;
                headStyle.onNormal.textColor = red;
                headStyle.focused.textColor = red;
                headStyle.onFocused.textColor = red;
                headStyle.hover.textColor = red;
                headStyle.onHover.textColor = red;
                headStyle.active.textColor = red;
                headStyle.onActive.textColor = red;
            }

            string headText = skipped
                ? title + "  (" + lines.Count + "行)   ×  この局では流れません"
                : title + "  (" + lines.Count + "行)";

            bool open = EditorGUILayout.Foldout(!collapsed, headText, true, headStyle);
            if (open == collapsed)
            {
                if (open) _collapsed.Remove(key); else _collapsed.Add(key);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("＋行を足す", GUILayout.Width(90f)))
            {
                Record("セリフを追加");
                lines.Add(new TutorialLine("", TutorialSpeaker.Enemy));
                writeBack(lines);
                _collapsed.Remove(key);
            }
            EditorGUILayout.EndHorizontal();

            // 畳んでいても「流れません」だけは見せる。閉じた枠に書き足す事故を防ぐ
            if (skipped && !open) DrawSkipBanner(skipReason);

            if (!open) { EditorGUILayout.EndVertical(); return; }

            if (skipped) DrawSkipBanner(skipReason);

            if (!string.IsNullOrEmpty(help))
            {
                EditorGUILayout.LabelField(help, EditorStyles.miniLabel);
            }

            if (hasDefaultFallback && lines.Count == 0 && !skipped)
            {
                EditorGUILayout.HelpBox(
                    "この枠は空にすると、コードに書かれた既定のセリフが流れます（無言にはなりません）。",
                    MessageType.Info);
            }

            // 盤面を出す位置。実際に効く条件は RunRound と同じ `0 <= rev < 行数-1`
            int effectiveReveal = -1;
            if (revealOwner != null)
            {
                effectiveReveal = DrawRevealControl(revealOwner, lines.Count);
            }

            if (lines.Count == 0)
            {
                EditorGUILayout.LabelField("（この場面では何も喋りません）", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            int moveUp = -1, moveDown = -1, remove = -1;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line == null) { lines[i] = line = new TutorialLine("", TutorialSpeaker.Enemy); }

                EditorGUILayout.BeginHorizontal();

                GUILayout.Label((i + 1).ToString(), EditorStyles.miniLabel, GUILayout.Width(22f));

                var newSpeaker = (TutorialSpeaker)EditorGUILayout.EnumPopup(line.speaker, GUILayout.Width(80f));
                if (newSpeaker != line.speaker)
                {
                    Record("話者を変更");
                    line.speaker = newSpeaker;
                }

                var newText = EditorGUILayout.TextArea(line.text ?? "", GUILayout.MinHeight(18f));
                if (newText != line.text)
                {
                    Record("セリフを編集");
                    line.text = newText;
                }

                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button("↑", GUILayout.Width(24f))) moveUp = i;
                using (new EditorGUI.DisabledScope(i == lines.Count - 1))
                    if (GUILayout.Button("↓", GUILayout.Width(24f))) moveDown = i;
                if (GUILayout.Button("−", GUILayout.Width(24f))) remove = i;

                EditorGUILayout.EndHorizontal();

                if (revealOwner != null && i == effectiveReveal) DrawRevealMarker();
            }

            if (revealOwner != null && effectiveReveal < 0) DrawRevealMarker();

            // ループ中にリストをいじると添字が壊れるので、確定してから1つだけ実行する
            if (moveUp > 0)
            {
                Record("セリフを並べ替え");
                var t = lines[moveUp]; lines[moveUp] = lines[moveUp - 1]; lines[moveUp - 1] = t;
            }
            else if (moveDown >= 0 && moveDown < lines.Count - 1)
            {
                Record("セリフを並べ替え");
                var t = lines[moveDown]; lines[moveDown] = lines[moveDown + 1]; lines[moveDown + 1] = t;
            }
            else if (remove >= 0)
            {
                Record("セリフを削除");
                lines.RemoveAt(remove);
            }

            writeBack(lines);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 「盤面をどこで出すか」を選ばせる。戻り値は**実際に効く**行番号（効かないときは -1）。
        ///
        /// `RunRound` の条件は `rev >= 0 && rev < introLines.Count - 1`。
        /// **範囲外の値は黙って「全部流してから」に化ける。** しかも行を1つ足すと
        /// 範囲内に戻って演出が変わる。第3局・第4局が実際にその状態だったので、
        /// 保存されている生の値と、いま効いている結果を**両方**出す。
        /// </summary>
        private int DrawRevealControl(TutorialRoundData round, int lineCount)
        {
            int raw = round.revealBoardAfterLineIndex;
            bool active = raw >= 0 && raw < lineCount - 1;

            // 選択肢: 「全部流してから」＋「N行目のあと」（有効な範囲だけ）
            int usable = Mathf.Max(0, lineCount - 1);
            var labels = new string[usable + 1];
            labels[0] = "導入を全部流し終えてから";
            for (int i = 0; i < usable; i++) labels[i + 1] = (i + 1) + "行目のあと";

            int selected = active ? raw + 1 : 0;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("盤面を出すタイミング", GUILayout.Width(130f));
            int next = EditorGUILayout.Popup(selected, labels);
            EditorGUILayout.EndHorizontal();

            if (next != selected)
            {
                Record("盤面を出すタイミングを変更");
                round.revealBoardAfterLineIndex = next == 0 ? -1 : next - 1;
                raw = round.revealBoardAfterLineIndex;
                active = raw >= 0 && raw < lineCount - 1;
            }

            // 保存値が範囲外のまま残っている場合の注意。行を足すと挙動が変わる
            if (!active && raw >= 0)
            {
                EditorGUILayout.HelpBox(
                    "保存されている値は " + raw + " ですが、導入が " + lineCount + "行しかないので効いていません"
                    + "（いまは全部流してから盤面が出ます）。\n"
                    + "この枠に行を足すと " + (raw + 1) + "行目のあとで盤面が出るように変わります。"
                    + "そうしたくなければ、上で「導入を全部流し終えてから」を選び直してください。",
                    MessageType.Warning);
            }

            return active ? raw : -1;
        }

        /// <summary>Unity のスキンによって読める赤が違うので、明暗で切り替える。</summary>
        private static Color SkipColor
        {
            get
            {
                return EditorGUIUtility.isProSkin
                    ? new Color(1f, 0.45f, 0.40f)    // 暗いスキン: 明るめの赤
                    : new Color(0.72f, 0.11f, 0.08f); // 明るいスキン: 濃い赤
            }
        }

        // 折り返す小さいラベル。EditorStyles のものは wordWrap が false で理由が切れる
        private static GUIStyle _wrapMini, _wrapMiniBold;
        private static GUIStyle WrapMini
        {
            get
            {
                if (_wrapMini == null) _wrapMini = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
                return _wrapMini;
            }
        }
        private static GUIStyle WrapMiniBold
        {
            get
            {
                if (_wrapMiniBold == null) _wrapMiniBold = new GUIStyle(EditorStyles.miniBoldLabel) { wordWrap = true };
                return _wrapMiniBold;
            }
        }

        /// <summary>「この局では流れません」の赤い帯。理由まで書かないと直しようがない。</summary>
        private static void DrawSkipBanner(string reason)
        {
            var prev = GUI.color;
            GUI.color = SkipColor;
            EditorGUILayout.LabelField(
                "× この局では流れません。ここに書き足しても画面には出ません。", WrapMiniBold);
            GUI.color = prev;
            EditorGUILayout.LabelField("理由: " + reason, WrapMini);
        }

        private static void DrawRevealMarker()
        {
            var prev = GUI.color;
            GUI.color = new Color(0.55f, 0.85f, 1f);
            EditorGUILayout.LabelField("──── ここで盤面（山牌・手牌・ドラ・HP）が出る ────", EditorStyles.miniBoldLabel);
            GUI.color = prev;
        }

        /// <summary>
        /// Undo に積みつつ dirty を立てる。
        /// **ScriptableObject の中の List を書き換えるだけでは Unity は保存すべきだと気づかない。**
        /// 触る直前に必ずこれを通す。
        /// </summary>
        private void Record(string label)
        {
            if (_asset == null) return;
            Undo.RecordObject(_asset, label);
            EditorUtility.SetDirty(_asset);
        }
    }
}
