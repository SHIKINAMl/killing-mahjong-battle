using System;
using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>フレームセットの固定ID正本。状態・通信・購入処理とは独立。</summary>
    public static class UIFrameCatalog
    {
        public const string DefaultId = "ui.frame.pixel_standard";
        private static Dictionary<string, UIFrameStyle> styles;
        public static int Revision { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { styles = null; Revision = 0; }

        private static void EnsureLoaded()
        {
            if (styles != null) return;
            styles = new Dictionary<string, UIFrameStyle>(StringComparer.Ordinal);
            foreach (var style in Resources.LoadAll<UIFrameStyle>("UIFrames")) Register(style);
            if (!styles.ContainsKey(DefaultId))
            {
                var fallback = ScriptableObject.CreateInstance<UIFrameStyle>();
                fallback.hideFlags = HideFlags.HideAndDontSave;
                styles.Add(DefaultId, fallback);
            }
        }

        /// <summary>今後の追加コンテンツも同じ入口。異なる定義によるIDの上書きは拒否。</summary>
        public static bool Register(UIFrameStyle style)
        {
            if (style == null || string.IsNullOrWhiteSpace(style.Id)) return false;
            EnsureLoaded();
            if (styles.TryGetValue(style.Id, out var previous)) return previous == style;
            styles.Add(style.Id, style); Revision++; return true;
        }

        public static bool Contains(string id)
        { EnsureLoaded(); return id != null && styles.TryGetValue(id, out var style) && style != null; }

        /// <summary>欠けた追加コンテンツ・旧保存値でも標準へ戻し、入力を止めない。</summary>
        public static UIFrameStyle Resolve(string id)
        {
            EnsureLoaded();
            return id != null && styles.TryGetValue(id, out var style) && style != null
                ? style : styles[DefaultId];
        }
    }
}
