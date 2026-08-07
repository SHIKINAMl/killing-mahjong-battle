using UnityEngine;
using UnityEditor;
using KillingMahjong.Common;

namespace KillingMahjong.EditorTools
{
    /// <summary>
    /// [TilePicker] を付けた int を、日本語の牌名ドロップダウンとして表示する。
    ///
    /// 保存されるのは今までどおり int（牌種 0〜28）。表示だけを差し替えている。
    /// 数字で牌を指定するのは間違いやすく、プランナーが台本を触るときの負担が大きいため。
    /// </summary>
    [CustomPropertyDrawer(typeof(TilePickerAttribute))]
    public class TilePickerDrawer : PropertyDrawer
    {
        // 牌種の並び（TileData / TutorialTiles と同じ採番）
        //   0-8 萬子1-9 / 9-17 筒子1-9 / 18-26 索子1-9 / 27 東 / 28 西
        private static readonly string[] Names = BuildNames();
        private static readonly string[] NamesWithNone = BuildNamesWithNone();

        private static string[] BuildNames()
        {
            string[] num = { "一", "二", "三", "四", "五", "六", "七", "八", "九" };
            var list = new string[29];
            for (int i = 0; i < 9; i++) list[i] = num[i] + "萬";
            for (int i = 0; i < 9; i++) list[9 + i] = num[i] + "筒";
            for (int i = 0; i < 9; i++) list[18 + i] = num[i] + "索";
            list[27] = "東";
            list[28] = "西";
            return list;
        }

        private static string[] BuildNamesWithNone()
        {
            var src = BuildNames();
            var list = new string[src.Length + 1];
            list[0] = "なし (-1)";
            for (int i = 0; i < src.Length; i++) list[i + 1] = src[i];
            return list;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.LabelField(position, label.text, "[TilePicker] は int 専用です");
                return;
            }

            var attr = (TilePickerAttribute)attribute;
            EditorGUI.BeginProperty(position, label, property);

            if (attr.AllowNone)
            {
                // -1 を先頭に置くので、表示上の index は +1 ずれる
                int shown = Mathf.Clamp(property.intValue + 1, 0, NamesWithNone.Length - 1);
                int picked = EditorGUI.Popup(position, label.text, shown, NamesWithNone);
                property.intValue = picked - 1;
            }
            else
            {
                int cur = property.intValue;
                if (cur < 0 || cur >= Names.Length)
                {
                    // 範囲外の値が入っていたら、気づけるように明示する
                    var warnRect = new Rect(position.x, position.y, position.width, position.height);
                    int fixedVal = EditorGUI.IntField(warnRect, label.text + " ※範囲外", cur);
                    property.intValue = fixedVal;
                }
                else
                {
                    property.intValue = EditorGUI.Popup(position, label.text, cur, Names);
                }
            }

            EditorGUI.EndProperty();
        }
    }
}
