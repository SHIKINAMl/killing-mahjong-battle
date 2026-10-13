using System;
using System.Collections.Generic;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.EngineData;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>元の山indexと表示画像の対応。牌種IDが同じでも別の実物として扱う。</summary>
    public readonly struct RoleTileTarget
    {
        public readonly Image Image;
        public readonly int WallIndex, TileId;
        public RoleTileTarget(Image image, int wallIndex, int tileId)
        { Image = image; WallIndex = wallIndex; TileId = tileId; }
    }

    /// <summary>任意の表示補助情報の形式・選択スナップショットだけを検証。役の成立を判定しない。</summary>
    public static class HandRoleSupportData
    {
        public static bool TryResolve(WaitData wait, string role, IReadOnlyList<RoleTileTarget> targets,
            out HashSet<int> indexes, out bool includeWait)
        {
            indexes = new HashSet<int>(); includeWait = false;
            if (wait?.source_wall_indexes == null || wait.yaku_supports == null || string.IsNullOrEmpty(role)
                || targets == null || targets.Count != 13 || wait.source_wall_indexes.Length != 13) return false;
            var selected = new HashSet<int>();
            var images = new HashSet<Image>();
            foreach (var target in targets)
                if (target.Image == null || target.WallIndex < 0 || !selected.Add(target.WallIndex) || !images.Add(target.Image)) return false;
            var snapshot = new HashSet<int>();
            foreach (int index in wait.source_wall_indexes) if (index < 0 || !snapshot.Add(index)) return false;
            if (!snapshot.SetEquals(selected)) return false;
            string key = YakuNameUtil.ToVoiceKey(role);
            bool reported = false;
            if (wait.yaku != null) foreach (string name in wait.yaku)
                if (YakuNameUtil.ToVoiceKey(name) == key) reported = true;
            if (!reported) return false;
            YakuSupportData support = null;
            foreach (var item in wait.yaku_supports)
                if (item != null && YakuNameUtil.ToVoiceKey(item.name) == key)
                { if (support != null) return false; support = item; }
            if (support == null) return false;
            if (support.kind == "context") return support.groups == null || support.groups.Length == 0;
            if ((support.kind != "tiles" && support.kind != "whole_hand")
                || support.groups == null || support.groups.Length == 0) return false;
            var collected = new HashSet<int>(); bool collectedWait = false;
            foreach (var group in support.groups)
            {
                if (group == null || group.wall_indexes == null
                    || (group.wall_indexes.Length == 0 && !group.include_wait_tile)) return false;
                var unique = new HashSet<int>();
                foreach (int index in group.wall_indexes)
                {
                    if (!selected.Contains(index) || !unique.Add(index)) return false;
                    collected.Add(index);
                }
                collectedWait |= group.include_wait_tile;
            }
            indexes = collected; includeWait = collectedWait;
            return true;
        }
    }

    /// <summary>届いた対応情報を、実物の牌の発光へつなぐ。本編・試写で共用する。</summary>
    public sealed class HandRoleTileSupport
    {
        public const string EffectId = "hand.role_tile_support";
        private WaitData wait;
        private Image waitImage;
        private readonly Dictionary<int, RoleTileTarget> snapshot = new Dictionary<int, RoleTileTarget>();
        private readonly Dictionary<Image, TileRoleGlow> glows = new Dictionary<Image, TileRoleGlow>();
        public string HoveredRole { get; private set; }
        public int ActiveTileCount
        {
            get { int count = 0; foreach (var glow in glows.Values) if (glow != null && glow.IsHighlighted) count++; return count; }
        }
        public void Bind(WaitData data, IReadOnlyList<RoleTileTarget> targets, Image candidate)
        {
            Clear(); wait = data; waitImage = candidate;
            if (targets == null) return;
            foreach (var target in targets)
            {
                if (target.Image == null || snapshot.ContainsKey(target.WallIndex)) { snapshot.Clear(); return; }
                snapshot.Add(target.WallIndex, target);
            }
        }
        public void SetHoveredRole(string role, IReadOnlyList<RoleTileTarget> current)
        {
            if (!MatchesSnapshot(current) || !HandRoleSupportData.TryResolve(wait, role, current, out var indexes, out bool includeWait))
            { ClearHighlights(); return; }
            HoveredRole = YakuNameUtil.ToVoiceKey(role);
            var dead = new List<Image>();
            foreach (var entry in glows) if (entry.Key == null || entry.Value == null) dead.Add(entry.Key);
            foreach (var image in dead) glows.Remove(image);
            var active = new HashSet<Image>();
            foreach (var target in current) if (indexes.Contains(target.WallIndex)) active.Add(target.Image);
            if (includeWait && waitImage != null) active.Add(waitImage);
            foreach (var entry in glows) if (entry.Value != null && !active.Contains(entry.Key)) entry.Value.SetHighlighted(false);
            foreach (var target in current) if (indexes.Contains(target.WallIndex)) Highlight(target.Image);
            if (includeWait && waitImage != null) Highlight(waitImage);
        }
        private bool MatchesSnapshot(IReadOnlyList<RoleTileTarget> current)
        {
            if (current == null || current.Count != 13 || snapshot.Count != current.Count) return false;
            foreach (var target in current)
                if (!snapshot.TryGetValue(target.WallIndex, out var before)
                    || target.TileId != before.TileId) return false;
            return true;
        }
        private void Highlight(Image image)
        {
            if (!glows.TryGetValue(image, out var glow) || glow == null)
            { glow = TileRoleGlow.Attach(image); glows[image] = glow; }
            glow.SetHighlighted(true);
        }
        public void ClearHighlights()
        {
            HoveredRole = null;
            foreach (var glow in glows.Values) if (glow != null) glow.SetHighlighted(false);
        }
        public void Clear()
        {
            ClearHighlights();
            foreach (var glow in glows.Values) if (glow != null) glow.Release();
            glows.Clear(); snapshot.Clear(); wait = null; waitImage = null;
        }
    }
}
