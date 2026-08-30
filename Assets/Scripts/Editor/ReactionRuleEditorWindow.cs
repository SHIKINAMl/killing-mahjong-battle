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
    public partial class ReactionRuleEditorWindow : EditorWindow
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

    }
}
