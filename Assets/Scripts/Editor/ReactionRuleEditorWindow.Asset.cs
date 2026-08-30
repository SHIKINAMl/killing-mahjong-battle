using System.Collections.Generic;
using System.IO;
using KillingMahjong.Managers.Reactions;
using KillingMahjong.UI;
using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    public partial class ReactionRuleEditorWindow
    {
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

        private void CreateAsset()
        {
            Directory.CreateDirectory(AssetDir);
            var set = CreateInstance<ReactionRuleSet>();
            AssetDatabase.CreateAsset(set, AssetPath);
            AssetDatabase.SaveAssets();
            ReactionRuleSet.ClearCache();
            _asset = set;
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
    }
}
