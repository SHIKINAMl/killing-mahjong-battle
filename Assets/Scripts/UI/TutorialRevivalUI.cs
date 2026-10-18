using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.Managers;
using KillingMahjong.Managers.Tutorial;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    /// <summary>復活導入の表示と入力だけを受け持つ。HP・勝敗・戦績・シーン遷移は変更しない。</summary>
    public sealed class TutorialRevivalUI : MonoBehaviour
    {
        public const string EffectId = "tutorial.revival";
        public const string ShrunkSpritePath = "Tutorial/beruru_shrunk";
        public enum SequenceStage { Red, Monologue, Bell, Summon, Skill, Revival, Beruru, White, Completed }
        public SequenceStage Stage { get; private set; }
        public int LineIndex { get; private set; }

        // 元の巻物（1010x1570、表示202x314）の人物部分と同じ、1ドット=2 UI単位。
        public static readonly Vector2 ShrunkSize = new Vector2(132f, 102f);
        private SkillEffectStage stage;
        private TextMeshProUGUI text;
        private Image cover, bell, scroll, beruru;
        private Button advance, bellButton, skillButton;
        private bool clicked, floating, closed;
        private float floatStartedAt;
        private readonly List<Material> materials = new List<Material>();

        public static TutorialRevivalUI Create(Transform parent)
        {
            var root = new GameObject("TutorialRevival", typeof(RectTransform));
            if (parent != null) root.transform.SetParent(parent, false);
            return root.AddComponent<TutorialRevivalUI>();
        }

        public IEnumerator Play(TutorialRevivalData data, TMP_FontAsset font, bool automatic = false)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            stage = new SkillEffectStage("RevivalStage", UISortingOrders.TutorialRevival, transform);
            stage.Root.AddComponent<GraphicRaycaster>();
            // 画面全体の透明な受け皿で、台詞・演出中に背後の盤面や設定が押されるのを防ぐ。
            var blocker = stage.AddDim("InputBlocker", Color.clear);
            blocker.raycastTarget = true;
            var red = stage.AddDarken("RedEdge", Color.red);
            red.MaxAlpha = 1f;
            red.Strength = 1f;
            cover = stage.AddDim("Space", Color.clear);

            Stage = SequenceStage.Red;
            yield return Animate(2f, t => {
                red.RadiusX = red.RadiusY = Mathf.Lerp(520f, 1f, t);
            });
            cover.color = Color.red;
            red.gameObject.SetActive(false);
            yield return Animate(1.5f, t => cover.color = Color.Lerp(Color.red, Color.black, t));

            text = stage.AddText("RevivalText", "", 20f, Color.white);
            if (font != null) text.font = font;
            text.rectTransform.sizeDelta = new Vector2(680f, 160f);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = true;
            // 専用材質にして、字幕や共有フォントに輪郭・影の変更を伝えない。
            var material = new Material(text.fontSharedMaterial);
            materials.Add(material);
            text.fontSharedMaterial = material;
            text.outlineWidth = 0f;
            material.DisableKeyword("UNDERLAY_ON");
            var catcher = stage.AddDim("RevivalAdvance", Color.clear);
            catcher.raycastTarget = true;
            advance = catcher.gameObject.AddComponent<Button>();
            advance.transition = Selectable.Transition.None;
            advance.onClick.AddListener(() => clicked = true);
            advance.gameObject.SetActive(false);

            Stage = SequenceStage.Monologue;
            yield return Lines(data.monologueLines, automatic);
            text.text = "";

            Stage = SequenceStage.Bell;
            var bellSprite = Resources.Load<Sprite>("麻雀牌/能力発動ボタン２");
            if (bellSprite == null) throw new InvalidOperationException("復活導入のベル画像がありません。");
            bell = stage.AddSprite("RevivalBell", bellSprite, new Vector2(0f, 450f), new Vector2(192f, 192f));
            bell.raycastTarget = true;
            bellButton = bell.gameObject.AddComponent<Button>();
            bellButton.transition = Selectable.Transition.None;
            bellButton.interactable = false;
            bellButton.onClick.AddListener(() => clicked = true);
            yield return Animate(.65f, t => bell.rectTransform.anchoredPosition =
                new Vector2(0f, Mathf.Lerp(450f, 0f, t * t)));
            clicked = false;
            bellButton.interactable = true;
            yield return WaitForInput(automatic);
            bellButton.interactable = false;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAbilityBellSE();
            yield return Animate(.4f, t => bell.color = new Color(1f, 1f, 1f, 1f - t));
            bell.gameObject.SetActive(false);

            Stage = SequenceStage.Summon;
            scroll = stage.AddSprite("RevivalScroll", null, new Vector2(0f, -35f), new Vector2(303f, 471f));
            for (int i = 1; i <= 10; i++)
            {
                scroll.sprite = Resources.Load<Sprite>("UI_Anim" + i);
                if (scroll.sprite == null) throw new InvalidOperationException("復活導入の登場コマがありません: " + i);
                yield return new WaitForSecondsRealtime(BelleFigure.FrameSeconds);
            }

            Stage = SequenceStage.Skill;
            var skillLabel = stage.AddText("RevivalSkillName", data.skillName, 22f, Color.black);
            if (font != null) skillLabel.font = font;
            skillLabel.outlineWidth = 0f;
            skillLabel.rectTransform.anchoredPosition = new Vector2(0f, -55f);
            skillLabel.rectTransform.sizeDelta = new Vector2(180f, 50f);
            var skill = stage.AddSprite("RevivalSkill", null, new Vector2(0f, -55f), new Vector2(180f, 50f));
            skill.color = Color.clear;
            skill.raycastTarget = true;
            skillButton = skill.gameObject.AddComponent<Button>();
            skillButton.transition = Selectable.Transition.None;
            skillButton.onClick.AddListener(() => clicked = true);
            clicked = false;
            yield return WaitForInput(automatic);
            skill.gameObject.SetActive(false);
            skillLabel.gameObject.SetActive(false);

            Stage = SequenceStage.Revival;
            // 同じ登場素材の閉じた姿へ戻し、能力名の発光で発動を伝える。対局スキルのコスト処理は呼ばない。
            for (int i = 10; i >= 7; i--)
            {
                scroll.sprite = Resources.Load<Sprite>("UI_Anim" + i);
                yield return new WaitForSecondsRealtime(BelleFigure.FrameSeconds);
            }
            var activation = stage.AddText("RevivalActivation", data.skillName, 32f, BelleFigure.Yellow);
            if (font != null) activation.font = font;
            activation.rectTransform.anchoredPosition = new Vector2(0f, -45f);
            yield return Animate(1.2f, t => {
                activation.rectTransform.localScale = Vector3.one * (1f + .2f * Mathf.Sin(t * Mathf.PI));
                activation.alpha = Mathf.Sin(t * Mathf.PI);
            });
            activation.gameObject.SetActive(false);
            yield return Animate(1.5f, t => cover.color = Color.Lerp(Color.black, new Color(.85f, .85f, .85f), t));

            // 巻物の人物の中心は(+10.5,+153)。同じ位置で、新しい絵を2倍の整数倍率まで縮める。
            var shrunk = Resources.Load<Sprite>(ShrunkSpritePath);
            if (shrunk == null) throw new InvalidOperationException("縮んだベルルの画像がありません。");
            beruru = stage.AddSprite("ShrunkBeruru", shrunk, new Vector2(10.5f, 118f), new Vector2(198f, 153f));
            scroll.gameObject.SetActive(false);
            yield return Animate(.7f, t => {
                beruru.rectTransform.sizeDelta = Vector2.Lerp(new Vector2(198f, 153f), ShrunkSize, t);
                beruru.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(10.5f, 118f), new Vector2(0f, 100f), t);
            });
            Stage = SequenceStage.Beruru;
            floating = true;
            floatStartedAt = Time.unscaledTime;
            text.color = Color.black;
            text.rectTransform.anchoredPosition = new Vector2(0f, -100f);
            text.transform.SetAsLastSibling();
            advance.transform.SetAsLastSibling();
            yield return Lines(data.beruruLines, automatic);
            text.text = "";
            // 白い光は次のタイトル画面で出す。ここではメニューへの遷移を先取りしない。
            Stage = SequenceStage.Completed;
        }

        /// <summary>タイトル表示後に光で包み、メニューを開く。同じ演出を試写からも呼ぶ。</summary>
        public IEnumerator PlayTitleReturn(Action showMenu)
        {
            floating = false;
            if (stage != null)
            {
                // 試写では同じ持ち主のまま続ける。本編ではタイトル側の新しい持ち主が呼ぶ。
                stage.Root.SetActive(false);
                Destroy(stage.Root);
            }
            stage = new SkillEffectStage("RevivalTitleTransition", UISortingOrders.TutorialRevival, transform);
            stage.Root.AddComponent<GraphicRaycaster>();
            var white = stage.AddDim("RevivalWhiteout", Color.clear);
            white.raycastTarget = true;
            Stage = SequenceStage.White;
            yield return new WaitForSecondsRealtime(.6f);
            yield return Animate(1.5f, t => white.color = new Color(1f, 1f, 1f, t));
            showMenu?.Invoke();
            yield return Animate(.5f, t => white.color = new Color(1f, 1f, 1f, 1f - t));
            Stage = SequenceStage.Completed;
        }

        private IEnumerator Lines(List<TutorialLine> lines, bool automatic)
        {
            if (lines == null) yield break;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] == null || string.IsNullOrEmpty(lines[i].text)) continue;
                LineIndex = i;
                text.text = TutorialEmphasis.Apply(lines[i].text);
                clicked = false;
                advance.gameObject.SetActive(true);
                yield return WaitForInput(automatic);
                advance.gameObject.SetActive(false);
            }
        }

        private IEnumerator WaitForInput(bool automatic)
        {
            if (automatic) yield return new WaitForSecondsRealtime(1.5f);
            else yield return new WaitUntil(() => clicked || closed);
        }

        private static IEnumerator Animate(float seconds, Action<float> apply)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                apply(Mathf.Clamp01(t / seconds));
                yield return null;
            }
            apply(1f);
        }

        private void Update()
        {
            if (floating && beruru != null)
                beruru.rectTransform.anchoredPosition = new Vector2(0f,
                    SkillEffectStage.Snap(100f + 6f * Mathf.Sin((Time.unscaledTime - floatStartedAt) * 2f)));
        }

        public void Close()
        {
            closed = true;
            floating = false;
            gameObject.SetActive(false);
            if (AudioManager.Instance != null) AudioManager.Instance.StopAbilityBellSE();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            foreach (var material in materials) if (material != null) Destroy(material);
            materials.Clear();
        }
    }
}
