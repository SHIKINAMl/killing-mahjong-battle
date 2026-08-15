using System;
using System.Collections.Generic;
using System.IO;
using KillingMahjong.Managers;
using KillingMahjong.Managers.Reactions;
using KillingMahjong.UI;
using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    /// <summary>
    /// **プランナーがコードを触らずに反応を足すためのウィンドウ。**
    ///
    /// 従来は新しい状況を1つ足すのに、`ReactionTrigger` への enum 追加と
    /// 発火させる C# の実装が要った。ここでは「いつ」を既定のイベントから選び、
    /// 「どんなとき」を変数の比較で組み立てる。**書けるのはデータだけ**なので、
    /// 何を足してもコンパイルは通るし、既存の反応も壊れない。
    ///
    /// 出す順番は3層で、**ルール → トリガー → CSV**。
    /// ここで作ったルールが当たれば、同じ場面の従来のセリフは出ない。
    /// ルールを消せば元の動きに戻る（既存のセリフは消していない）。
    ///
    /// **同じイベントのルールは上から順に見て、最初に条件を満たした1件だけが出る。**
    /// 点数付けで自動的に選ぶ方式は採っていない。理由は `ReactionRule` のコメント参照。
    /// </summary>
    public class ReactionRuleEditorWindow : EditorWindow
    {
        private const string AssetDir = "Assets/Resources/Reactions";
        private const string AssetPath = AssetDir + "/ReactionRules.asset";

        /// <summary>吹き出しは3行までなので、これを超えると見切れる</summary>
        private const int DialogueSoftLimit = 40;

        private ReactionRuleSet _asset;
        private int _selected = -1;
        private Vector2 _listScroll;
        private Vector2 _bodyScroll;
        private CharacterData _character;
        private string[] _faceIds = new string[0];

        [MenuItem("Tools/リアクション/反応ルールを編集（プランナー用）")]
        public static void Open()
        {
            var w = GetWindow<ReactionRuleEditorWindow>("反応ルール");
            w.minSize = new Vector2(900f, 520f);
            w.Load();
            w.Show();
        }

        private void OnEnable()
        {
            if (_asset == null) Load();
        }

        private void Load()
        {
            _asset = AssetDatabase.LoadAssetAtPath<ReactionRuleSet>(AssetPath);
            LoadCharacter();
        }

        private void LoadCharacter()
        {
            if (_character == null)
            {
                // 表情の候補を出すためだけに使う。reactions がいちばん多いものを既定にする
                var guids = AssetDatabase.FindAssets("t:" + nameof(CharacterData));
                int best = -1;
                foreach (var g in guids)
                {
                    var cd = AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(g));
                    if (cd == null) continue;
                    int n = cd.reactions == null ? 0 : cd.reactions.Count;
                    if (n > best) { best = n; _character = cd; }
                }
            }

            var faces = new List<string> { "" };
            if (_character != null && _character.faceSprites != null)
                foreach (var s in _character.faceSprites)
                    if (s != null && !string.IsNullOrEmpty(s.id) && s.id != "blink") faces.Add(s.id);
            _faceIds = faces.ToArray();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_asset == null)
            {
                EditorGUILayout.HelpBox(
                    "反応ルールのアセットがまだありません。\n" + AssetPath,
                    MessageType.Info);
                if (GUILayout.Button("作る", GUILayout.Height(30f))) CreateAsset();
                return;
            }

            if (_asset.rules == null) _asset.rules = new List<ReactionRule>();

            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawBody();
            EditorGUILayout.EndHorizontal();
        }

        private void CreateAsset()
        {
            Directory.CreateDirectory(AssetDir);
            var set = CreateInstance<ReactionRuleSet>();
            AssetDatabase.CreateAsset(set, AssetPath);
            AssetDatabase.SaveAssets();
            ReactionRuleSet.ClearCache();
            _asset = set;
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(AssetPath, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(_asset == null))
            {
                if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    EditorUtility.SetDirty(_asset);
                    AssetDatabase.SaveAssets();
                    ReactionRuleSet.ClearCache();
                    ShowNotification(new GUIContent("保存しました"));
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "ここで作ったルールが、同じ場面の従来のセリフより先に出ます（ルール → トリガー → CSV の順）。\n"
                + "同じ「きっかけ」のルールは上から順に見て、最初に条件が合った1件だけが出ます。"
                + "優先したいものを上に置いてください。",
                MessageType.Info);
        }

        // ------------------------------------------------------------------

        private void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(280f));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ルール一覧", EditorStyles.boldLabel);
            if (GUILayout.Button("＋", GUILayout.Width(28f)))
            {
                Record("ルールを追加");
                _asset.rules.Add(new ReactionRule());
                _selected = _asset.rules.Count - 1;
            }
            EditorGUILayout.EndHorizontal();

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            ReactionEvent? lastEvent = null;
            for (int i = 0; i < _asset.rules.Count; i++)
            {
                var r = _asset.rules[i];
                if (r == null) { _asset.rules[i] = r = new ReactionRule(); }

                // 同じきっかけごとに区切りを入れる。順番が優先順位なので、
                // どこからどこまでが同じ勝負なのかが見えないと並べ替えられない
                if (lastEvent == null || lastEvent.Value != r.trigger)
                {
                    EditorGUILayout.Space(2f);
                    EditorGUILayout.LabelField(ReactionVariableCatalog.EventLabel(r.trigger), EditorStyles.miniBoldLabel);
                    lastEvent = r.trigger;
                }

                EditorGUILayout.BeginHorizontal();

                string mark = !r.enabled ? "（停止中）"
                            : ReactionRuleEngine.IsUnreachable(_asset, i) ? "×  "
                            : "";
                string label = mark + (string.IsNullOrEmpty(r.label) ? "(名前なし)" : r.label)
                             + "  [" + CountLines(r) + "]";

                bool on = _selected == i;
                var style = new GUIStyle("Button") { alignment = TextAnchor.MiddleLeft };
                if (!r.enabled || ReactionRuleEngine.IsUnreachable(_asset, i))
                    style.normal.textColor = DeadColor;

                if (GUILayout.Toggle(on, label, style) != on) { _selected = i; GUI.FocusControl(null); }

                using (new EditorGUI.DisabledScope(i == 0))
                    if (GUILayout.Button("↑", GUILayout.Width(22f))) { Move(i, -1); break; }
                using (new EditorGUI.DisabledScope(i == _asset.rules.Count - 1))
                    if (GUILayout.Button("↓", GUILayout.Width(22f))) { Move(i, 1); break; }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static int CountLines(ReactionRule r)
        {
            return r.lines == null ? 0 : r.lines.Count;
        }

        private void Move(int index, int delta)
        {
            int to = index + delta;
            if (to < 0 || to >= _asset.rules.Count) return;
            Record("ルールを並べ替え");
            var t = _asset.rules[index];
            _asset.rules[index] = _asset.rules[to];
            _asset.rules[to] = t;
            if (_selected == index) _selected = to;
            else if (_selected == to) _selected = index;
        }

        // ------------------------------------------------------------------

        private void DrawBody()
        {
            EditorGUILayout.BeginVertical();
            _bodyScroll = EditorGUILayout.BeginScrollView(_bodyScroll);

            if (_selected < 0 || _selected >= _asset.rules.Count)
            {
                EditorGUILayout.HelpBox("左からルールを選ぶか、＋で新しく作ってください。", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            var rule = _asset.rules[_selected];

            // --- 見出し ---
            EditorGUILayout.BeginHorizontal();
            string newLabel = EditorGUILayout.TextField("名前", rule.label);
            if (newLabel != rule.label) { Record("名前を変更"); rule.label = newLabel; }

            bool newEnabled = EditorGUILayout.ToggleLeft("有効", rule.enabled, GUILayout.Width(60f));
            if (newEnabled != rule.enabled) { Record("有効を切替"); rule.enabled = newEnabled; }

            if (GUILayout.Button("複製", GUILayout.Width(50f))) Duplicate(rule);
            if (GUILayout.Button("削除", GUILayout.Width(50f))) { DeleteRule(); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical(); return; }
            EditorGUILayout.EndHorizontal();

            if (ReactionRuleEngine.IsUnreachable(_asset, _selected))
            {
                DrawDeadBanner(
                    "同じきっかけで、条件が1つも無いルールが上にあります。そちらが必ず先に当たるので、"
                    + "このルールは絶対に出ません。上のルールに条件を足すか、このルールを上へ move してください。");
            }

            EditorGUILayout.Space();

            // --- きっかけ ---
            EditorGUILayout.LabelField("いつ", EditorStyles.boldLabel);
            var events = (ReactionEvent[])Enum.GetValues(typeof(ReactionEvent));
            var eventLabels = new string[events.Length];
            for (int i = 0; i < events.Length; i++) eventLabels[i] = ReactionVariableCatalog.EventLabel(events[i]);

            int evIndex = Array.IndexOf(events, rule.trigger);
            int nextEv = EditorGUILayout.Popup("きっかけ", Mathf.Max(0, evIndex), eventLabels);
            if (nextEv != evIndex)
            {
                Record("きっかけを変更");
                rule.trigger = events[nextEv];
                // 変数はイベントごとに違う。残すと存在しない条件になるので消す
                if (rule.conditions != null) rule.conditions.Clear();
            }

            EditorGUILayout.Space();

            // --- 条件 ---
            DrawConditions(rule);

            EditorGUILayout.Space();

            // --- 出し方 ---
            EditorGUILayout.LabelField("出し方", EditorStyles.boldLabel);

            var nextPriority = (ReactionPriority)EditorGUILayout.EnumPopup("優先度", rule.priority);
            if (nextPriority != rule.priority) { Record("優先度を変更"); rule.priority = nextPriority; }
            EditorGUILayout.LabelField(PriorityHelp(rule.priority), WrapMini);

            float nextCd = EditorGUILayout.FloatField("再び出せるまでの秒数", rule.cooldownSeconds);
            if (!Mathf.Approximately(nextCd, rule.cooldownSeconds)) { Record("クールダウンを変更"); rule.cooldownSeconds = Mathf.Max(0f, nextCd); }

            var nextLimit = (FireLimit)EditorGUILayout.EnumPopup("出せる回数", rule.limit);
            if (nextLimit != rule.limit) { Record("回数制限を変更"); rule.limit = nextLimit; }

            EditorGUILayout.Space();

            // --- セリフ ---
            DrawLines(rule);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static string PriorityHelp(ReactionPriority p)
        {
            switch (p)
            {
                case ReactionPriority.Progress:
                    return "必ず出す。ロンや局の開始など、飛ばすと話が繋がらないもの向け。";
                case ReactionPriority.Situation:
                    return "出したいが、同じものが既に並んでいれば捨てる。打牌や賭けの反応向け。";
                default:
                    return "出なくてよい。ほかの演出が動いている間は無条件で捨てられ、"
                         + "全体で6秒のあいだを空ける。連打や放置への反応向け。";
            }
        }

        private void DrawConditions(ReactionRule rule)
        {
            if (rule.conditions == null) rule.conditions = new List<ReactionCondition>();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("どんなとき", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("＋条件を足す", GUILayout.Width(100f)))
            {
                Record("条件を追加");
                rule.conditions.Add(new ReactionCondition());
            }
            EditorGUILayout.EndHorizontal();

            if (rule.conditions.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "条件がありません。このきっかけが起きたら毎回このルールが当たります。\n"
                    + "同じきっかけで下にあるルールは出られなくなるので、いちばん下に置いてください。",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("すべて満たしたときだけ出ます。", WrapMini);

            var vars = ReactionVariableCatalog.For(rule.trigger);
            var varLabels = new string[vars.Length];
            for (int i = 0; i < vars.Length; i++) varLabels[i] = vars[i].label;

            int remove = -1;
            for (int i = 0; i < rule.conditions.Count; i++)
            {
                var c = rule.conditions[i];
                if (c == null) { rule.conditions[i] = c = new ReactionCondition(); }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                int varIndex = IndexOfVar(vars, c.key);
                int nextVar = EditorGUILayout.Popup(varIndex, varLabels, GUILayout.Width(200f));
                if (nextVar != varIndex && nextVar >= 0)
                {
                    Record("条件の項目を変更");
                    c.key = vars[nextVar].key;
                    c.op = CompareOp.Equal;
                    c.number = 0f;
                    c.text = "";
                    varIndex = nextVar;
                }

                var info = varIndex >= 0 ? vars[varIndex] : null;
                if (info == null)
                {
                    EditorGUILayout.LabelField("← 項目を選んでください", WrapMini);
                }
                else
                {
                    DrawConditionValue(c, info);
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("−", GUILayout.Width(24f))) remove = i;
                EditorGUILayout.EndHorizontal();

                if (info != null && !string.IsNullOrEmpty(info.help))
                    EditorGUILayout.LabelField(info.help, WrapMini);

                EditorGUILayout.EndVertical();
            }

            if (remove >= 0)
            {
                Record("条件を削除");
                rule.conditions.RemoveAt(remove);
            }
        }

        private static int IndexOfVar(ReactionVarInfo[] vars, string key)
        {
            for (int i = 0; i < vars.Length; i++) if (vars[i].key == key) return i;
            return -1;
        }

        private void DrawConditionValue(ReactionCondition c, ReactionVarInfo info)
        {
            switch (info.kind)
            {
                case VarKind.Bool:
                {
                    // 「＝ はい / ＝ いいえ」の2択に畳む。比較記号を選ばせても意味がない
                    bool current = c.op == CompareOp.NotEqual ? c.number < 0.5f : c.number >= 0.5f;
                    int idx = current ? 0 : 1;
                    int next = EditorGUILayout.Popup(idx, new[] { "はい", "いいえ" }, GUILayout.Width(90f));
                    if (next != idx)
                    {
                        Record("条件の値を変更");
                        c.op = CompareOp.Equal;
                        c.number = next == 0 ? 1f : 0f;
                    }
                    break;
                }

                case VarKind.Text:
                {
                    var nextOp = DrawOp(c, new[] { CompareOp.Equal, CompareOp.NotEqual });
                    if (nextOp != c.op) { Record("条件の比較を変更"); c.op = nextOp; }

                    if (info.choices != null && info.choices.Length > 0)
                    {
                        var labels = new string[info.choices.Length];
                        for (int i = 0; i < labels.Length; i++)
                            labels[i] = string.IsNullOrEmpty(info.choices[i]) ? "（なし）" : info.choices[i];

                        int idx = Array.IndexOf(info.choices, c.text ?? "");
                        int next = EditorGUILayout.Popup(Mathf.Max(0, idx), labels, GUILayout.Width(140f));
                        if (next != idx) { Record("条件の値を変更"); c.text = info.choices[next]; }
                    }
                    else
                    {
                        string next = EditorGUILayout.TextField(c.text ?? "", GUILayout.Width(140f));
                        if (next != c.text) { Record("条件の値を変更"); c.text = next; }
                    }
                    break;
                }

                default:
                {
                    var nextOp = DrawOp(c, new[] {
                        CompareOp.Equal, CompareOp.NotEqual,
                        CompareOp.GreaterOrEqual, CompareOp.LessOrEqual,
                        CompareOp.Greater, CompareOp.Less });
                    if (nextOp != c.op) { Record("条件の比較を変更"); c.op = nextOp; }

                    float next = EditorGUILayout.FloatField(c.number, GUILayout.Width(80f));
                    if (!Mathf.Approximately(next, c.number)) { Record("条件の値を変更"); c.number = next; }
                    break;
                }
            }
        }

        private static CompareOp DrawOp(ReactionCondition c, CompareOp[] allowed)
        {
            var labels = new string[allowed.Length];
            for (int i = 0; i < allowed.Length; i++) labels[i] = ReactionVariableCatalog.OpLabel(allowed[i]);

            int idx = Array.IndexOf(allowed, c.op);
            int next = EditorGUILayout.Popup(Mathf.Max(0, idx), labels, GUILayout.Width(90f));
            return allowed[next];
        }

        private void DrawLines(ReactionRule rule)
        {
            if (rule.lines == null) rule.lines = new List<ReactionRuleLine>();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("セリフ  (" + rule.lines.Count + "本)", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("＋セリフを足す", GUILayout.Width(110f)))
            {
                Record("セリフを追加");
                rule.lines.Add(new ReactionRuleLine());
            }
            EditorGUILayout.EndHorizontal();

            // 件数ではなく「中身のある行」で見る。枠だけ足して空のままだと、
            // 実行時もこのルールは無かったことになる
            if (!ReactionRuleEngine.HasUsableLine(rule))
            {
                EditorGUILayout.HelpBox(
                    rule.lines.Count == 0
                        ? "セリフが1本もありません。この状態ではルールは無視されます（従来のセリフが出ます）。"
                        : "セリフの欄はありますが、すべて空です。中身が無い行は数に入らないので、"
                          + "このルールは無視されます（従来のセリフが出ます）。",
                    MessageType.Error);
                return;
            }
            if (rule.lines.Count == 1)
            {
                EditorGUILayout.LabelField("2本以上書くとランダムに選ばれます。1本だと毎回同じセリフです。", WrapMini);
            }

            int remove = -1;
            for (int i = 0; i < rule.lines.Count; i++)
            {
                var line = rule.lines[i];
                if (line == null) { rule.lines[i] = line = new ReactionRuleLine(); }

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label((i + 1).ToString(), EditorStyles.miniLabel, GUILayout.Width(20f));

                int faceIdx = Array.IndexOf(_faceIds, line.faceId ?? "");
                var faceLabels = new string[_faceIds.Length];
                for (int f = 0; f < _faceIds.Length; f++)
                    faceLabels[f] = string.IsNullOrEmpty(_faceIds[f]) ? "（変えない）" : _faceIds[f];

                int nextFace = EditorGUILayout.Popup(Mathf.Max(0, faceIdx), faceLabels, GUILayout.Width(110f));
                if (nextFace != faceIdx && nextFace >= 0) { Record("表情を変更"); line.faceId = _faceIds[nextFace]; }

                var nextManfu = (ManfuType)EditorGUILayout.EnumPopup(line.manfu, GUILayout.Width(90f));
                if (nextManfu != line.manfu) { Record("漫符を変更"); line.manfu = nextManfu; }

                string nextText = EditorGUILayout.TextArea(line.text ?? "", GUILayout.MinHeight(18f));
                if (nextText != (line.text ?? "")) { Record("セリフを編集"); line.text = nextText; }

                if (GUILayout.Button("−", GUILayout.Width(24f))) remove = i;
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(line.text) && line.text.Length > DialogueSoftLimit)
                {
                    EditorGUILayout.HelpBox(
                        line.text.Length + " 字あります。" + DialogueSoftLimit + " 字を超えると吹き出しから見切れます。",
                        MessageType.Warning);
                }
            }

            if (remove >= 0)
            {
                Record("セリフを削除");
                rule.lines.RemoveAt(remove);
            }

            EditorGUILayout.LabelField(
                "漫符はまだゲーム側が読んでいません（設定しても今は出ません）。", WrapMini);
        }

        private void Duplicate(ReactionRule src)
        {
            Record("ルールを複製");
            var copy = new ReactionRule
            {
                label = src.label + " のコピー",
                enabled = src.enabled,
                trigger = src.trigger,
                priority = src.priority,
                cooldownSeconds = src.cooldownSeconds,
                limit = src.limit,
                conditions = new List<ReactionCondition>(),
                lines = new List<ReactionRuleLine>(),
            };
            foreach (var c in src.conditions)
                copy.conditions.Add(new ReactionCondition { key = c.key, op = c.op, number = c.number, text = c.text });
            foreach (var l in src.lines)
                copy.lines.Add(new ReactionRuleLine { text = l.text, faceId = l.faceId, manfu = l.manfu });

            _asset.rules.Insert(_selected + 1, copy);
            _selected++;
        }

        private void DeleteRule()
        {
            if (!EditorUtility.DisplayDialog("確認", "このルールを削除しますか？", "削除", "やめる")) return;
            Record("ルールを削除");
            _asset.rules.RemoveAt(_selected);
            _selected = Mathf.Min(_selected, _asset.rules.Count - 1);
        }

        /// <summary>
        /// Undo に積みつつ dirty を立てる。
        /// **ScriptableObject の中の List を書き換えるだけでは Unity は保存すべきだと気づかない。**
        /// </summary>
        private void Record(string label)
        {
            if (_asset == null) return;
            Undo.RecordObject(_asset, label);
            EditorUtility.SetDirty(_asset);
        }

        // ------------------------------------------------------------------

        private static Color DeadColor
        {
            get
            {
                return EditorGUIUtility.isProSkin
                    ? new Color(1f, 0.45f, 0.40f)
                    : new Color(0.72f, 0.11f, 0.08f);
            }
        }

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

        private static void DrawDeadBanner(string reason)
        {
            var prev = GUI.color;
            GUI.color = DeadColor;
            EditorGUILayout.LabelField("× このルールは出ません。", WrapMiniBold);
            GUI.color = prev;
            EditorGUILayout.LabelField("理由: " + reason, WrapMini);
        }
    }
}
