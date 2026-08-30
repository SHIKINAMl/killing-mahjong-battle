using System;
using System.Collections.Generic;
using KillingMahjong.UI;
using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    public partial class ReactionEditorWindow
    {
        // ---- トリガータブ ----

        private void DrawTriggerTab()
        {
            if (_character == null)
            {
                EditorGUILayout.HelpBox("CharacterData を選んでください。", MessageType.Info);
                return;
            }
            if (_character.reactions == null)
                _character.reactions = new List<CharacterReaction>();

            EditorGUILayout.LabelField(
                "漫符（manfu）・表示時間（duration）・ボイス（voiceClip）は"
                + "まだどこからも読まれていません。設定しても今は効きません。", WrapMiniBold);
            EditorGUILayout.LabelField(
                "EnemyInfoUI.PlayReaction は表情が設定されているときだけ絵を差し替えます。"
                + "体だけ設定しても無視されます。", WrapMini);

            foreach (var g in TriggerGroups)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(g.title, EditorStyles.boldLabel);
                foreach (var t in g.triggers) DrawTrigger(t);
            }

            DrawUnlistedTriggers();
        }

        /// <summary>
        /// `TriggerGroups` のどの分類にも入っていないトリガーを最後にまとめて出す。
        ///
        /// **これが無いと、enum に足したトリガーがエディタから見えない。**
        /// 「追加したのに出てこない」＝「追加できていない」と誤解して二重に足す事故になる。
        /// `ReactionTrigger` を正として引くので、分類への登録を忘れても必ずここに現れる。
        /// </summary>
        private void DrawUnlistedTriggers()
        {
            var known = new HashSet<ReactionTrigger>();
            foreach (var g in TriggerGroups)
                foreach (var t in g.triggers) known.Add(t);

            var rest = new List<ReactionTrigger>();
            foreach (ReactionTrigger t in Enum.GetValues(typeof(ReactionTrigger)))
                if (!known.Contains(t)) rest.Add(t);

            if (rest.Count == 0) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("分類に登録されていないトリガー", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "ReactionTrigger には在るけれど、このウィンドウの分類（TriggerGroups）に"
                + "書かれていないものです。セリフはここからそのまま書けます。\n"
                + "分類に足すと、上の適切な見出しの下へ移動します。", WrapMini);

            foreach (var t in rest) DrawTrigger(t);
        }

        private void DrawTrigger(ReactionTrigger trigger)
        {
            string when;
            bool live = LiveTriggers.TryGetValue(trigger, out when);

            var mine = new List<CharacterReaction>();
            foreach (var r in _character.reactions)
                if (r != null && r.trigger == trigger) mine.Add(r);

            string key = "trg:" + trigger;
            bool collapsed = _collapsed.Contains(key);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            string head = live
                ? $"{trigger}  ({mine.Count}件)"
                : $"{trigger}  ({mine.Count}件)   ×  呼ばれていません";
            bool open = EditorGUILayout.Foldout(!collapsed, head, true, live ? EditorStyles.foldoutHeader : RedFoldout);
            if (open == collapsed) { if (open) _collapsed.Remove(key); else _collapsed.Add(key); }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("＋行を足す", GUILayout.Width(90f)))
            {
                RecordCharacter("リアクションを追加");
                _character.reactions.Add(new CharacterReaction { trigger = trigger, dialogueText = "" });
                _collapsed.Remove(key);
                open = true;
            }
            EditorGUILayout.EndHorizontal();

            if (!live && !open)
            {
                DrawDeadBanner(UnwiredReason(trigger));
                EditorGUILayout.EndVertical();
                return;
            }
            if (!open) { EditorGUILayout.EndVertical(); return; }

            if (live)
            {
                EditorGUILayout.LabelField("いつ出るか: " + P(when), WrapMini);
            }
            else
            {
                DrawDeadBanner(UnwiredReason(trigger));
            }

            if (mine.Count == 0)
            {
                EditorGUILayout.LabelField("（このトリガーのセリフはありません）", WrapMini);
                EditorGUILayout.EndVertical();
                return;
            }
            if (mine.Count == 1)
            {
                EditorGUILayout.HelpBox("1件だけなので、毎回まったく同じセリフになります。", MessageType.Warning);
            }

            CharacterReaction remove = null;
            for (int i = 0; i < mine.Count; i++)
            {
                var r = mine[i];

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label((i + 1).ToString(), EditorStyles.miniLabel, GUILayout.Width(22f));

                string nextFace = DrawIdPopupForAsset("表情", r.faceExpressionId, _faceIds, 40f, 110f, r);
                if (nextFace != r.faceExpressionId) { RecordCharacter("表情を変更"); r.faceExpressionId = nextFace; }

                var nextManfu = (ManfuType)EditorGUILayout.EnumPopup(r.manfuType, GUILayout.Width(90f));
                if (nextManfu != r.manfuType) { RecordCharacter("漫符を変更"); r.manfuType = nextManfu; }

                string nextText = EditorGUILayout.TextArea(r.dialogueText ?? "", GUILayout.MinHeight(18f));
                if (nextText != (r.dialogueText ?? "")) { RecordCharacter("セリフを編集"); r.dialogueText = nextText; }

                if (GUILayout.Button("−", GUILayout.Width(24f))) remove = r;
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(r.dialogueText) && r.dialogueText.Length > DialogueSoftLimit)
                {
                    EditorGUILayout.HelpBox(
                        $"{r.dialogueText.Length} 字あります。{DialogueSoftLimit} 字を超えると吹き出しから見切れます。",
                        MessageType.Warning);
                }

                // `{0}` は EnemyDiscard にしか渡っていない。他で書くとそのまま画面に出る
                if (!string.IsNullOrEmpty(r.dialogueText) && r.dialogueText.Contains("{0}")
                    && r.trigger != ReactionTrigger.EnemyDiscard)
                {
                    EditorGUILayout.HelpBox(
                        "{0} に入れる値が渡されるのは EnemyDiscard だけです。"
                        + "ここでは {0} の文字がそのまま吹き出しに出ます。", MessageType.Error);
                }

                if (!string.IsNullOrEmpty(r.bodyExpressionId) && string.IsNullOrEmpty(r.faceExpressionId))
                {
                    EditorGUILayout.HelpBox(
                        "体だけ設定されています。PlayReaction は表情が空だと絵をまったく差し替えないので、"
                        + "この体は無視されます。", MessageType.Warning);
                }
            }

            if (remove != null)
            {
                RecordCharacter("リアクションを削除");
                _character.reactions.Remove(remove);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>配線されていない理由。個別の説明が無ければ一般的な文言に落とす</summary>
        private static string UnwiredReason(ReactionTrigger trigger)
        {
            string reason;
            if (UnwiredReasons.TryGetValue(trigger, out reason)) return reason;
            return "このトリガーを鳴らしているコードがありません。"
                 + "出すには発火させる側から ReactionController.Trigger() を呼ぶ実装が要ります。"
                 + "配線したら ReactionEditorWindow の LiveTriggers に条件を1行書き足すと、"
                 + "この赤帯が「いつ出るか」の説明に変わります。";
        }

        private string DrawIdPopupForAsset(string label, string value, string[] ids, float labelW, float popupW, CharacterReaction r)
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(labelW));

            int index = Array.IndexOf(ids, value ?? "");
            if (index < 0)
            {
                var grown = new string[ids.Length + 1];
                Array.Copy(ids, grown, ids.Length);
                grown[ids.Length] = value;
                ids = grown;
                index = ids.Length - 1;
            }

            var labels = new string[ids.Length];
            for (int i = 0; i < ids.Length; i++) labels[i] = string.IsNullOrEmpty(ids[i]) ? "（変えない）" : ids[i];

            int next = EditorGUILayout.Popup(index, labels, GUILayout.Width(popupW));
            return ids[next];
        }

        /// <summary>
        /// Undo に積みつつ dirty を立てる。
        /// **ScriptableObject の中の List を書き換えるだけでは Unity は保存すべきだと気づかない。**
        /// 触る直前に必ずこれを通す。
        /// </summary>
        private void RecordCharacter(string label)
        {
            if (_character == null) return;
            Undo.RecordObject(_character, label);
            EditorUtility.SetDirty(_character);
        }
    }
}
