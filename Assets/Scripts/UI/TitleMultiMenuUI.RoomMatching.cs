using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{

    /// <summary>
    /// フレンドマッチの「条件を整える」の先、ルームマッチング画面（2026-11-23 仮案スレッド、プランナーの
    /// 「06_roommaching.aseprite」。2026-11-24 ユーザー指示「まず見た目だけ」「右は募集中の部屋の一覧」
    /// 「コードは作成後に待機画面で出す」）。
    ///
    ///   左のページ: ルームコード（作るまでは「-----」）・部屋の条件・「ルームを作成」
    ///   右のページ: 募集中の部屋の一覧。**見た目だけ**（帯と「参加」の札を絵のとおりに置くだけで、押しても何もしない）
    ///
    /// **サーバーに問い合わせる部分が無いところは、動きを作らず見た目だけにする**（2026-11-24 ユーザー指示
    /// 「サーバー側はこちらのものではないので、勝手にいじらないで」「サーバーに問い合わせる部分がないなら、
    /// そこは無視して作らないように。仮に見た目だけ作る」）。部屋の一覧を返す仕組みはサーバーに無い。
    ///
    /// **部屋の条件はまだ対局に効かない（見た目だけ）。** 能力の有無・最大局数・初期HP・最大賭け金・賭け金の単位・
    /// 勝利点数（プランナーが挙げた項目）を選べるが、サーバーへは送っていない。初期値は今の決まり
    /// （game_engine.py: 体力 20000・25局・累計 30000 点で勝ち、game_state.py: 最大賭け金 5000・単位 200）。
    /// 効かせるときは、サーバーの部屋に条件を持たせ、対局の規則へ渡す（計算はサーバーだけで持つ）。
    ///
    /// 盤（枠・ページ・仕切り・下線）は、絵の色を写した単色の四角で組んだ（項目の数に合わせて動かせるように）。
    /// 文字の絵（ルームコード・ルームを作成・参加）と部屋の帯は、アセプライトの層を切り出しただけ。
    /// 配置は元の 400x300 の座標を 2倍（800x600 基準）にしたもの（<see cref="PixelRect"/>）。
    /// </summary>
    public partial class TitleMultiMenuUI
    {
        private static readonly Color BoardFrame = new Color32(102, 57, 49, 255);
        private static readonly Color BoardPage = new Color32(238, 195, 154, 255);
        private static readonly Color BoardInk = new Color32(0, 0, 0, 255);
        private static readonly Color BoardText = new Color32(70, 36, 30, 255);
        private static readonly Color ScrollTrack = new Color32(155, 173, 183, 255);
        private static readonly Color ScrollHandle = new Color32(132, 126, 135, 255);
        private static readonly Color RoomErrorText = new Color32(172, 50, 50, 255);

        private const float SettingRowHeight = 36f;

        /// <summary>部屋の条件の1項目。値は <see cref="Choices"/> の中から選ぶ。見た目だけでサーバーへは送らない。</summary>
        private sealed class RoomSetting
        {
            public string Label;
            public string[] Choices;
            public int Index;
            public bool IsToggle;
            public TMP_Text ValueText;
            public Image Check;
        }

        /// <summary>選んだ条件はゲームを閉じるまで覚えておく（画面を開き直しても戻らない）。</summary>
        private static readonly List<RoomSetting> RoomSettings = new List<RoomSetting>
        {
            new RoomSetting { Label = "能力", Choices = new[] { "あり", "なし" }, Index = 0, IsToggle = true },
            new RoomSetting { Label = "最大局数", Choices = new[] { "10", "15", "20", "25", "30" }, Index = 3 },
            new RoomSetting { Label = "初期HP", Choices = new[] { "10000", "15000", "20000", "30000" }, Index = 2 },
            new RoomSetting { Label = "最大賭け金", Choices = new[] { "3000", "5000", "10000" }, Index = 1 },
            new RoomSetting { Label = "賭け金の単位", Choices = new[] { "100", "200", "500", "1000" }, Index = 1 },
            new RoomSetting { Label = "勝利点数", Choices = new[] { "20000", "30000", "50000" }, Index = 1 },
        };

        private TMP_Text _roomCodeText;
        private TMP_Text _roomStatus;

        /// <summary>元の 400x300 の絵の四角（左上 x0,y0 ～ 右下 x1,y1）を、800x600 基準の中心と大きさに直す。</summary>
        private static void PixelRect(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            Center(rt, new Vector2((x1 - x0) * 2f, (y1 - y0) * 2f));
            rt.anchoredPosition = new Vector2(x0 + x1 - 400f, 300f - (y0 + y1));
        }

        private GameObject Block(Transform parent, string name, Color color, float x0, float y0, float x1, float y1)
        {
            var obj = SceneFirst.Child(parent, name, out bool created, typeof(RectTransform), typeof(Image));
            if (created)
            {
                PixelRect(obj.GetComponent<RectTransform>(), x0, y0, x1, y1);
                var img = obj.GetComponent<Image>();
                img.color = color;
                img.raycastTarget = false;
            }
            return obj;
        }

        private GameObject Art(Transform parent, string name, string texture, float x0, float y0, float x1, float y1, bool button)
        {
            var types = button
                ? new[] { typeof(RectTransform), typeof(RawImage), typeof(Button) }
                : new[] { typeof(RectTransform), typeof(RawImage) };
            var obj = SceneFirst.Child(parent, name, out bool created, types);
            if (created)
            {
                PixelRect(obj.GetComponent<RectTransform>(), x0, y0, x1, y1);
                var raw = obj.GetComponent<RawImage>();
                raw.texture = Resources.Load<Texture2D>(FriendCallResourceDir + texture);
                raw.raycastTarget = button;
                if (button) obj.GetComponent<Button>().targetGraphic = raw;
            }
            if (button) UIInteractionFeedback.Attach(obj);
            return obj;
        }

        private TMP_Text BoardLabel(Transform parent, string name, string text, float size, TextAlignmentOptions align,
            float x0, float y0, float x1, float y1, bool display = false)
        {
            var obj = SceneFirst.Child(parent, name, out bool created, typeof(RectTransform), typeof(TextMeshProUGUI));
            var tmp = obj.GetComponent<TextMeshProUGUI>();
            if (created)
            {
                PixelRect(obj.GetComponent<RectTransform>(), x0, y0, x1, y1);
                tmp.font = display ? GameUiTypography.Display : GameUiTypography.Body;
                tmp.text = text;
                tmp.fontSize = size;
                tmp.color = BoardText;
                tmp.alignment = align;
                tmp.raycastTarget = false;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
            }
            return tmp;
        }

        private static void OnClick(GameObject obj, System.Action action)
        {
            var b = obj.GetComponent<Button>();
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => action?.Invoke());
        }

        // ------------------------------------------------------------------

        private void BuildRoomMatching()
        {
            _friendCallConditions = SceneFirst.Child(_friendCall.transform, "RoomMatching", out bool made,
                typeof(RectTransform), typeof(Image));
            if (made)
            {
                Stretch(_friendCallConditions.GetComponent<RectTransform>());
                var bg = _friendCallConditions.GetComponent<Image>();
                bg.color = Color.black;     // 絵の外は黒（後ろの電話の絵は見せない）
                bg.raycastTarget = true;
            }
            var root = _friendCallConditions.transform;

            // 盤: 枠 → ページ → 仕切り → 下線
            Block(root, "Frame", BoardFrame, 22, 14, 378, 286);
            Block(root, "Page", BoardPage, 32, 21, 368, 274);
            Block(root, "Divider", BoardInk, 199, 21, 201, 274);
            Block(root, "CodeUnderline", BoardInk, 48, 106, 180, 107);

            // もどる（ページ左上の小さな字。白い文字は紙の上で読めないので、濃い茶色）
            var back = SceneFirst.Child(root, "Back", out bool madeBack, typeof(RectTransform), typeof(Image), typeof(Button));
            if (madeBack)
            {
                PixelRect(back.GetComponent<RectTransform>(), 36, 24, 76, 38);
                var hit = back.GetComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                var label = BoardLabel(back.transform, "Label", "< もどる", 16f, TextAlignmentOptions.Left, 0, 0, 0, 0);
                Stretch(label.rectTransform);
            }
            UIInteractionFeedback.Attach(back);
            OnClick(back, ShowFriendCallMain);

            // 左: ルームコード
            Art(root, "CodeLabel", "RoomCodeLabel", 89, 43, 137, 54, false);
            _roomCodeText = BoardLabel(root, "Code", new string('-', MatchJoinRequest.PasswordLength), 72f,
                TextAlignmentOptions.Center, 52, 58, 176, 102, display: true);
            _roomCodeText.color = BoardInk;

            BuildRoomSettings(root);

            var create = Art(root, "Create", "RoomCreate", 49, 232, 181, 267, true);
            OnClick(create, OnPrivateCreateSelected);

            // 参加・作成の結果（エラーなど）。ルームを作成の上
            _roomStatus = BoardLabel(root, "Status", "", 14f, TextAlignmentOptions.Center, 40, 222, 190, 231);
            _roomStatus.text = "";

            BuildRoomList(root);
        }

        private void BuildRoomSettings(Transform root)
        {
            var scroll = SceneFirst.Child(root, "Settings", out bool made, typeof(RectTransform), typeof(ScrollRect));
            var view = SceneFirst.Child(scroll.transform, "Viewport", out bool madeView,
                typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            var content = SceneFirst.Child(view.transform, "Content", out bool madeContent, typeof(RectTransform));
            var bar = SceneFirst.Child(root, "SettingsScrollbar", out bool madeBar, typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            var handleArea = SceneFirst.Child(bar.transform, "Sliding", out bool madeArea, typeof(RectTransform));
            var handle = SceneFirst.Child(handleArea.transform, "Handle", out bool madeHandle, typeof(RectTransform), typeof(Image));

            if (made)
            {
                PixelRect(scroll.GetComponent<RectTransform>(), 40, 112, 180, 222);
                Stretch(view.GetComponent<RectTransform>());
                var viewImg = view.GetComponent<Image>();
                viewImg.color = new Color(0f, 0f, 0f, 0f); // ホイールとドラッグを受けるため
                var crt = content.GetComponent<RectTransform>();
                crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(0.5f, 1f);
                crt.anchoredPosition = Vector2.zero;
                crt.sizeDelta = new Vector2(0f, SettingRowHeight * RoomSettings.Count);

                PixelRect(bar.GetComponent<RectTransform>(), 183, 117, 191, 218);
                bar.GetComponent<Image>().color = ScrollTrack;
                Stretch(handleArea.GetComponent<RectTransform>());
                Stretch(handle.GetComponent<RectTransform>());
                handle.GetComponent<Image>().color = ScrollHandle;
                var sb = bar.GetComponent<Scrollbar>();
                sb.direction = Scrollbar.Direction.BottomToTop;
                sb.handleRect = handle.GetComponent<RectTransform>();
                sb.targetGraphic = handle.GetComponent<Image>();

                var sr = scroll.GetComponent<ScrollRect>();
                sr.horizontal = false;
                sr.viewport = view.GetComponent<RectTransform>();
                sr.content = crt;
                sr.verticalScrollbar = sb;
                sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                sr.movementType = ScrollRect.MovementType.Clamped;
                sr.scrollSensitivity = 18f;
            }

            for (int i = 0; i < RoomSettings.Count; i++)
                BuildSettingRow(content.transform, RoomSettings[i], i);
        }

        private void BuildSettingRow(Transform content, RoomSetting setting, int index)
        {
            var row = SceneFirst.Child(content, "Setting" + index, out bool made, typeof(RectTransform));
            if (made)
            {
                var rt = row.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, SettingRowHeight);
                rt.anchoredPosition = new Vector2(0f, -SettingRowHeight * index);
            }

            // 白い四角（プランナーの絵のチェック欄）。オンオフの項目は押すと中に印が付く
            var box = SceneFirst.Child(row.transform, "Box", out bool madeBox, typeof(RectTransform), typeof(Image), typeof(Button));
            var check = SceneFirst.Child(box.transform, "Check", out bool madeCheck, typeof(RectTransform), typeof(Image));
            if (madeBox)
            {
                var brt = box.GetComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 0.5f); brt.pivot = new Vector2(0f, 0.5f);
                brt.anchoredPosition = new Vector2(10f, 0f);
                brt.sizeDelta = new Vector2(18f, 18f);
                box.GetComponent<Image>().color = Color.white;
                var crt = check.GetComponent<RectTransform>();
                Center(crt, new Vector2(10f, 10f));
                check.GetComponent<Image>().color = BoardFrame;
                check.GetComponent<Image>().raycastTarget = false;
            }
            setting.Check = check.GetComponent<Image>();

            var label = SceneFirst.Child(row.transform, "Label", out bool madeLabel, typeof(RectTransform), typeof(TextMeshProUGUI));
            if (madeLabel)
            {
                var lrt = label.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0.55f, 1f);
                lrt.offsetMin = new Vector2(38f, 0f); lrt.offsetMax = Vector2.zero;
                var t = label.GetComponent<TextMeshProUGUI>();
                t.font = GameUiTypography.Body; t.fontSize = 16f; t.color = BoardText;
                t.alignment = TextAlignmentOptions.Left; t.raycastTarget = false;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.text = setting.Label;
            }

            // 値。数の項目は左右を押して選ぶ（< 25 >）
            var value = SceneFirst.Child(row.transform, "Value", out bool madeValue,
                typeof(RectTransform), typeof(Image), typeof(Button));
            var valueText = SceneFirst.Child(value.transform, "Text", out bool madeValueText, typeof(RectTransform), typeof(TextMeshProUGUI));
            if (madeValue)
            {
                var vrt = value.GetComponent<RectTransform>();
                vrt.anchorMin = new Vector2(0.55f, 0f); vrt.anchorMax = new Vector2(1f, 1f);
                vrt.offsetMin = Vector2.zero; vrt.offsetMax = new Vector2(-6f, 0f);
                value.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
                Stretch(valueText.GetComponent<RectTransform>());
                var t = valueText.GetComponent<TextMeshProUGUI>();
                t.font = GameUiTypography.Body; t.fontSize = 16f; t.color = BoardText;
                t.alignment = TextAlignmentOptions.Right; t.raycastTarget = false;
                t.textWrappingMode = TextWrappingModes.NoWrap;
            }
            setting.ValueText = valueText.GetComponent<TMP_Text>();

            UIInteractionFeedback.Attach(box);
            UIInteractionFeedback.Attach(value);
            OnClick(box, () => StepSetting(setting, 1));
            OnClick(value, () => StepSetting(setting, StepFromPointer(value.GetComponent<RectTransform>())));
            RefreshSetting(setting);
        }

        /// <summary>値の欄の左半分を押したら前へ、右半分なら次へ。</summary>
        private static int StepFromPointer(RectTransform rt)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 mouse = UnityEngine.InputSystem.Mouse.current != null
                ? UnityEngine.InputSystem.Mouse.current.position.ReadValue()
                : Vector2.zero;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, mouse, cam, out var local)) return 1;
            return local.x < rt.rect.center.x ? -1 : 1;
        }

        private static void StepSetting(RoomSetting setting, int step)
        {
            int n = setting.Choices.Length;
            setting.Index = ((setting.Index + step) % n + n) % n;
            RefreshSetting(setting);
        }

        private static void RefreshSetting(RoomSetting setting)
        {
            string v = setting.Choices[setting.Index];
            if (setting.ValueText != null) setting.ValueText.text = setting.IsToggle ? v : "< " + v + " >";
            // 数の項目は「条件として使う」印としていつも付けておく。オンオフの項目は「あり」のときだけ
            if (setting.Check != null) setting.Check.enabled = !setting.IsToggle || setting.Index == 0;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// 右のページの部屋の帯と「参加」の札。案の絵の位置（400x300 の座標）にそのまま置く。
        /// いちばん下の帯はページの下端で切れている（絵のとおり。切れた分は画像の下を落とす）。
        /// </summary>
        private static readonly int[] RoomRowTops = { 45, 82, 122, 163, 206, 251 };
        private static readonly int[] RoomRowLefts = { 214, 214, 214, 215, 215, 216 };

        private void BuildRoomList(Transform root)
        {
            const int rowW = 141, rowH = 29, pageBottom = 274;
            for (int i = 0; i < RoomRowTops.Length; i++)
            {
                int x0 = RoomRowLefts[i], y0 = RoomRowTops[i];
                int y1 = Mathf.Min(y0 + rowH, pageBottom);
                var row = Art(root, "RoomRow" + i, "RoomRow", x0, y0, x0 + rowW, y1, false);
                ClipBottom(row, y1 - y0, rowH);

                // 札は帯の左端から 107、上端から 7（案の絵の1段目）
                int tx0 = x0 + 107, ty0 = y0 + 7;
                int ty1 = Mathf.Min(ty0 + 17, pageBottom);
                var tag = Art(root, "RoomJoin" + i, "RoomJoinTag", tx0, ty0, tx0 + 30, ty1, false);
                ClipBottom(tag, ty1 - ty0, 17);
            }
        }

        /// <summary>下が切れた絵は、縦に縮めず下を落とす。</summary>
        private static void ClipBottom(GameObject obj, int shown, int full)
        {
            var raw = obj.GetComponent<RawImage>();
            float v = (float)shown / full;
            raw.uvRect = new Rect(0f, 1f - v, 1f, v);
        }

        private void ShowRoomMatching()
        {
            if (_roomStatus != null) _roomStatus.text = "";
            if (_roomCodeText != null) _roomCodeText.text = new string('-', MatchJoinRequest.PasswordLength);
        }
    }
}
