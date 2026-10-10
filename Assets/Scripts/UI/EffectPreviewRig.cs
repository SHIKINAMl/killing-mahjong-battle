using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>演出だけを持つ試写用の舞台。対局・通信・戦績のコンポーネントは含めない。</summary>
    public sealed class EffectPreviewRig : MonoBehaviour
    {
        public PhaseTransitionUI phase;
        public RonAnimationUI ron;
        public PlayerInfoUI player;
        public EnemyInfoUI enemy;
        public BettingUI betting;
        public BlinkEffectUI blink;
        public TileResourceManager tiles;
        public TMP_FontAsset font;
        public SpriteRenderer character;
        public SpriteRenderer face;
        public Camera viewCamera;
        public Sprite arrowArt;
        public DialogueUI dialogue;
        public KillingMahjong.UI.Effects.MatchMomentumUI momentum;
        /// <summary>右上の役一覧と強化の枠。役強化の演出が、最後にここへ飛んでいく。</summary>
        public YakuListUI yakuList;
        public VictoryConfig[] endings;
        public VictoryConfig[] victoryEndings;
        public RectTransform surface;
        public readonly List<RectTransform> enemyTiles = new List<RectTransform>();
        public readonly List<RectTransform> handTiles = new List<RectTransform>();

        public void Initialize()
        {
            phase.gameObject.SetActive(true);
            ron.gameObject.SetActive(true);
            player.gameObject.SetActive(true);
            enemy.gameObject.SetActive(true);
            player.SetMaxHP(20000); player.SetHP(20000); player.ShowReadyBox(false);
            enemy.SetMaxHP(20000); enemy.SetHP(20000); enemy.ShowReadyBox(false);
            player.SetVitalsVisible(true); enemy.SetPanelVisible(true);
            if (yakuList != null) yakuList.gameObject.SetActive(true);
            if (dialogue != null) { dialogue.HideText(); dialogue.gameObject.SetActive(false); }
            if (character != null) character.gameObject.SetActive(true);
            if (betting != null) betting.HideBettingPhase(true);
            var go = new GameObject("PreviewTiles", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            surface = go.GetComponent<RectTransform>();
            surface.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 16;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800, 600);
            scaler.matchWidthOrHeight = .5f;
            for (int i = 0; i < 34; i++)
                enemyTiles.Add(Tile("Enemy" + i, -1, new Vector2(-208 + (i % 17) * 26, -74 - (i / 17) * 33)));
            for (int i = 0; i < 13; i++)
                handTiles.Add(Tile("Hand" + i, KillingMahjong.Managers.TutorialTiles.Encode(i % 9, false),
                    new Vector2(-156 + i * 26, -235)));
        }

        private RectTransform Tile(string name, int id, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(surface, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(25, 33); rt.anchoredPosition = position;
            var img = go.GetComponent<Image>();
            img.sprite = tiles.GetTileSprite(id); img.raycastTarget = false;
            return rt;
        }

        public VictoryConfig Ending(VictoryType type)
        {
            bool win = type == VictoryType.NormalVictory || type == VictoryType.SpecialVictory;
            var source = win ? victoryEndings : endings;
            if (source != null) foreach (var item in source)
                if (item != null && item.victoryType == type) return item;
            var fallback = win ? (type == VictoryType.NormalVictory ? VictoryType.NormalDefeat : VictoryType.SpecialDefeat) : type;
            if (endings != null) foreach (var item in endings)
                if (item != null && item.victoryType == fallback) return item;
            throw new InvalidOperationException("EDの設定がありません: " + type);
        }
    }
}
