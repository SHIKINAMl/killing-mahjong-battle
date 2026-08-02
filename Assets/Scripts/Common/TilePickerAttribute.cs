using UnityEngine;

namespace KillingMahjong.Common
{
    /// <summary>
    /// int の牌種（0〜28）を、Inspector 上で日本語のドロップダウンとして表示する。
    ///
    /// データは int のまま。表示だけを変えるので、既存のロジックには一切影響しない。
    /// List&lt;int&gt; に付けると各要素に適用される。
    ///
    /// 使い方:
    ///   [TilePicker] public List&lt;int&gt; wallBaseIds;
    ///   [TilePicker(allowNone: true)] public int doraBaseId;   // -1（なし）を選べるようにする
    ///
    /// 名前について: 同じ名前空間に牌ID操作の <see cref="TileId"/> があるため、
    /// 属性名を TileId にすると `[TileId]` がそちらを指してしまう。衝突を避けて TilePicker としている。
    /// </summary>
    public class TilePickerAttribute : PropertyAttribute
    {
        /// <summary>-1（なし）を選択肢に含めるか。</summary>
        public readonly bool AllowNone;

        public TilePickerAttribute(bool allowNone = false)
        {
            AllowNone = allowNone;
        }
    }
}
