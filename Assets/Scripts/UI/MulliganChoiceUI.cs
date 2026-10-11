using System;
using System.Collections.Generic;
using KillingMahjong.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 牌交換（マリガン）の、残す3枚を選ぶ画面（2026-10-11 のスキル変更）。
    ///
    /// 牌交換は「1枚を山札と交換」から「3枚出して、出した3枚と山札の3枚の6枚から、残す3枚を選ぶ」に変わった。
    /// サーバーから6枚の候補（skill_accepted の mulliganOptions）が届いたら、この画面を出す。
    ///
    ///   ・左の3枚が出した牌、右の3枚が山札から引いた牌
    ///   ・牌を押すと選ぶ／外す。3枚選ぶと「決定」が押せる
    ///   ・**閉じるボタンは無い。** 選び終えるまでサーバーが手牌の確定を受け付けないので、
    ///     閉じられると進めなくなる。出した3枚をそのまま選べば、元のままにできる
    ///
    /// 画面の部品はシーンに置いてある（Prefab: Assets/Prefabs/UI/MulliganChoice.prefab）。
    /// 置く／足すのは <c>Tools/UI/実行時UIをシーンへ置く（対局）</c>（SceneFirstBaker.BakeMatch）。
    /// 牌の絵と選んだ印だけは、開くたびに当て直す。
    /// </summary>
    public class MulliganChoiceUI : MonoBehaviour
    {
        public const string RootName = "MulliganChoice";

        public const int OptionCount = 6;
        public const int KeepCount = 3;

        private static readonly Color PanelBg = new Color32(0x1C, 0x14, 0x18, 0xF2);
        private static readonly Color PanelEdge = new Color32(0x6A, 0x50, 0x58, 0xFF);
        private static readonly Color TextMain = new Color32(0xF4, 0xEC, 0xE0, 0xFF);
        private static readonly Color TextSub = new Color32(0xB8, 0xA8, 0xA0, 0xFF);
        private static readonly Color Marker = new Color32(0xC8, 0x30, 0x40, 0xFF);
        private static readonly Color PickFrame = new Color32(0xFF, 0xE0, 0x40, 0xFF);
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        private static readonly Vector2 PanelSize = new Vector2(660f, 360f);
        private const float TileSize = 76f;
        private const float TileY = 6f;
        /// <summary>選んだ牌を持ち上げる量。</summary>
        private const float PickLift = 16f;
        /// <summary>牌の中心の横位置。左の3枚と右の3枚のあいだを空ける。</summary>
        private static readonly float[] TileX = { -245f, -160f, -75f, 75f, 160f, 245f };

        private bool _built;
        private TMP_FontAsset _font;
        private GameObject _body;
        private TextMeshProUGUI _countText;
        private Button _confirm;
        private readonly List<Button> _tileButtons = new List<Button>();
        private readonly List<Image> _tileFaces = new List<Image>();
        private readonly List<Image> _tileFrames = new List<Image>();

        private readonly List<int> _picked = new List<int>();
        private Action<List<int>> _onDecided;

        /// <summary>いま開いているか。</summary>
        public bool IsOpen { get { return _body != null && _body.activeSelf; } }

        /// <summary>シーンに置いてある画面を探す。無ければ作る（そのときは再生時に警告が出る）。</summary>
        public static MulliganChoiceUI FindOrCreate()
        {
            var found = FindFirstObjectByType<MulliganChoiceUI>(FindObjectsInactive.Include);
            if (found != null) return found;

            GameObject go = SceneFirst.Root(RootName, out bool _, typeof(RectTransform));
            var ui = go.GetComponent<MulliganChoiceUI>();
            return ui != null ? ui : go.AddComponent<MulliganChoiceUI>();
        }

        /// <param name="optionTileIds">候補の牌6枚。先頭3つが出した牌、後ろ3つが山札から引いた牌</param>
        /// <param name="tiles">牌の絵の表</param>
        /// <param name="onDecided">「決定」を押したとき。選んだ候補の番号（0〜5）が、選んだ順に3つ渡る</param>
        public void Show(IList<int> optionTileIds, TileResourceManager tiles, Action<List<int>> onDecided)
        {
            Build();
            _onDecided = onDecided;
            _picked.Clear();

            for (int i = 0; i < _tileFaces.Count; i++)
            {
                bool has = optionTileIds != null && i < optionTileIds.Count;
                if (_tileButtons[i] != null) _tileButtons[i].gameObject.SetActive(has);
                if (!has || _tileFaces[i] == null) continue;
                Sprite sprite = tiles != null ? tiles.GetTileSprite(optionTileIds[i]) : null;
                _tileFaces[i].sprite = sprite;
                _tileFaces[i].color = sprite != null ? Color.white : new Color32(238, 232, 214, 255);
            }

            _body.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (_body != null) _body.SetActive(false);
        }

        /// <summary>
        /// 再生していないときに、足りない部品をシーンへ置く（<c>SceneFirstBaker</c> が呼ぶ）。
        /// 置いたあとは閉じた状態にしておく。
        /// </summary>
        public static string BakeForEditor()
        {
            MulliganChoiceUI ui = FindOrCreate();
            ui._built = false;
            string made = ui.Build();
            ui._body.SetActive(false);
            return made;
        }

        private void Toggle(int index)
        {
            if (_picked.Contains(index)) _picked.Remove(index);
            else if (_picked.Count < KeepCount) _picked.Add(index);
            else return;

            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio != null) audio.PlayPickTileSE();
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < _tileButtons.Count; i++)
            {
                bool on = _picked.Contains(i);
                if (_tileFrames[i] != null) _tileFrames[i].color = on ? PickFrame : Clear;
                if (_tileButtons[i] != null)
                {
                    ((RectTransform)_tileButtons[i].transform).anchoredPosition =
                        new Vector2(TileX[i], TileY + (on ? PickLift : 0f));
                }
            }

            int left = KeepCount - _picked.Count;
            if (_countText != null) _countText.text = left > 0 ? "あと " + left + " 枚" : "これで決定できます";
            if (_confirm != null) _confirm.interactable = left == 0;
        }

        // ------------------------------------------------------------
        //  組み立て（シーンに置いてあればそれを使う）
        // ------------------------------------------------------------

        private string Build()
        {
            if (_built) return "";
            _built = true;

            _tileButtons.Clear();
            _tileFaces.Clear();
            _tileFrames.Clear();
            _font = BorrowJapaneseFont();

            var canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = UISortingOrders.MulliganChoice;

                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800f, 600f);
                scaler.matchWidthOrHeight = 0.5f;

                gameObject.AddComponent<GraphicRaycaster>();
            }

            _body = SceneFirst.Child(transform, "Body", out bool madeBody, typeof(RectTransform));
            if (madeBody) Stretch((RectTransform)_body.transform);

            // 後ろの対局を暗く落とす。押しても下へ抜けないよう、クリックはここで受ける
            GameObject scrim = SceneFirst.Child(_body.transform, "Scrim", out bool madeScrim,
                typeof(RectTransform), typeof(Image));
            if (madeScrim)
            {
                Stretch((RectTransform)scrim.transform);
                var img = scrim.GetComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.8f);
                img.raycastTarget = true;
            }

            Image panel = Img(_body.transform, "Panel", PanelBg, Vector2.zero, PanelSize);
            Transform p = panel.transform;

            Txt(p, "Title", "残す3枚を選んでください", new Vector2(0f, 148f), new Vector2(600f, 28f),
                20f, TextAlignmentOptions.Left, TextMain);
            Img(p, "TitleRule", PanelEdge, new Vector2(0f, 130f), new Vector2(600f, 1f));

            Txt(p, "GivenHead", "出した牌", new Vector2(-160f, 88f), new Vector2(250f, 20f),
                14f, TextAlignmentOptions.Center, TextSub);
            Txt(p, "DrawnHead", "山札から引いた牌", new Vector2(160f, 88f), new Vector2(250f, 20f),
                14f, TextAlignmentOptions.Center, TextSub);
            Img(p, "MiddleRule", PanelEdge, new Vector2(0f, TileY), new Vector2(1f, 120f));

            for (int i = 0; i < OptionCount; i++)
            {
                int index = i;
                GameObject go = SceneFirst.Child(p, "Option" + i, out bool madeOption,
                    typeof(RectTransform), typeof(Image), typeof(Button));
                var rt = (RectTransform)go.transform;
                var frame = go.GetComponent<Image>();
                var button = go.GetComponent<Button>();
                if (madeOption)
                {
                    Center(rt, new Vector2(TileSize + 8f, TileSize + 8f));
                    rt.anchoredPosition = new Vector2(TileX[i], TileY);
                    frame.color = Clear;
                    frame.raycastTarget = true;
                    button.targetGraphic = frame;
                    button.transition = Selectable.Transition.None;
                }

                Image face = Img(go.transform, "Face", Color.white, Vector2.zero, new Vector2(TileSize, TileSize));
                face.preserveAspect = true;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Toggle(index));

                _tileButtons.Add(button);
                _tileFrames.Add(frame);
                _tileFaces.Add(face);
            }

            _countText = Txt(p, "Count", "あと 3 枚", new Vector2(0f, -72f), new Vector2(600f, 22f),
                16f, TextAlignmentOptions.Center, TextMain);
            Txt(p, "Note", "選ばなかった3枚は山札に戻ります。出した3枚を選べば、元のままです。", new Vector2(0f, -98f),
                new Vector2(620f, 20f), 13f, TextAlignmentOptions.Center, TextSub);

            // 名前に Confirm を使う（右クリックで戻る機能は Cancel / Close などのボタンを探すので、これは拾われない）
            _confirm = Btn(p, "ConfirmButton", "決定", new Vector2(0f, -142f), new Vector2(220f, 40f), 18f, Marker);
            _confirm.onClick.RemoveAllListeners();
            _confirm.onClick.AddListener(() =>
            {
                if (_picked.Count != KeepCount) return;
                var result = new List<int>(_picked);
                Hide();
                if (_onDecided != null) _onDecided(result);
            });

            return SceneFirst.Report("MulliganChoiceUI");
        }

        // ------------------------------------------------------------
        //  小物。どれも「シーンに置いてあればそれを使い、無ければ作る」（YakuSelectionUI と同じ）
        // ------------------------------------------------------------

        private static Image Img(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
        {
            GameObject go = SceneFirst.Child(parent, name, out bool created, typeof(RectTransform), typeof(Image));
            var img = go.GetComponent<Image>();
            if (created)
            {
                Center(img.rectTransform, size);
                img.rectTransform.anchoredPosition = pos;
                img.color = color;
                img.raycastTarget = false;
            }
            return img;
        }

        private TextMeshProUGUI Txt(Transform parent, string name, string text, Vector2 pos, Vector2 size,
            float fontSize, TextAlignmentOptions align, Color color)
        {
            GameObject go = SceneFirst.Child(parent, name, out bool created, typeof(RectTransform), typeof(TextMeshProUGUI));
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (created)
            {
                Center(tmp.rectTransform, size);
                tmp.rectTransform.anchoredPosition = pos;
                if (_font != null) tmp.font = _font;
                tmp.text = text;
                tmp.fontSize = fontSize;
                tmp.alignment = align;
                tmp.color = color;
                tmp.raycastTarget = false;
            }
            return tmp;
        }

        private Button Btn(Transform parent, string name, string label, Vector2 pos, Vector2 size, float fontSize, Color color)
        {
            GameObject go = SceneFirst.Child(parent, name, out bool created,
                typeof(RectTransform), typeof(Image), typeof(Button));
            var button = go.GetComponent<Button>();
            if (created)
            {
                var rt = (RectTransform)go.transform;
                Center(rt, size);
                rt.anchoredPosition = pos;
                var img = go.GetComponent<Image>();
                img.color = color;
                img.raycastTarget = true;
                button.targetGraphic = img;
            }
            Txt(go.transform, "Text", label, Vector2.zero, size, fontSize, TextAlignmentOptions.Center, TextMain);
            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static TMP_FontAsset BorrowJapaneseFont()
        {
            var labels = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var label in labels)
            {
                if (label != null && label.font != null) return label.font;
            }
            return TMP_Settings.defaultFontAsset;
        }
    }
}
