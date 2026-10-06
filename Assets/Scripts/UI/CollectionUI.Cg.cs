using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// コレクションの「CG」タブ（2026-10-06 のユーザー指示
    /// 「現在使っている絵と、それの名前を全部表示したい」）。
    ///
    /// **並べるのは、いま実際に使っている絵だけ。** `Assets/Resources/` の画像を
    /// シーン・プレハブ・.asset の GUID 参照と、スクリプト内の `Resources.Load` の
    /// 文字列の両方からたどって、どこからも参照されていないものを外してある
    /// （調べた時点で 175 枚中 164 枚が使用中）。
    ///
    /// **一覧は手で書かない。** 絵が増減したら
    /// `scratchpad/scan_images.py` → `gen_cg_list.py` → `write_cg_cs.py`
    /// の順で作り直す。手で足すと、使っていない絵が混ざっても気づけない。
    ///
    /// **絵は見えている分だけ読む。** 164枚を一度に読むと、開いた瞬間に
    /// 固まるうえテクスチャを抱え込む（2048px のものが混ざっている）。
    /// 枠に入った行だけ `Resources.Load` して、閉じるときにまとめて解放する。
    /// </summary>
    public sealed partial class CollectionUI
    {
        /// <summary>一覧に並べる1枚。</summary>
        private readonly struct Art
        {
            /// <summary>`Resources.Load` に渡す道。拡張子は付けない。</summary>
            public readonly string Path;

            /// <summary>画面に出す名前。ファイル名そのまま。</summary>
            public readonly string Label;

            /// <summary>区切りの見出し。</summary>
            public readonly string Section;

            public Art(string path, string label, string section)
            {
                Path = path;
                Label = label;
                Section = section;
            }
        }

        // ------------------------------------------------------------
        //  並べ方
        // ------------------------------------------------------------

        /// <summary>横に何枚並べるか。</summary>
        private const int CgColumns = 5;

        /// <summary>1枚ぶんの枠。絵の下に名前を置くので、縦が少し長い。</summary>
        private const float CgCellWidth = 134f;
        private const float CgCellHeight = 118f;

        /// <summary>絵を収める四角。はみ出さないよう `preserveAspect` に任せる。</summary>
        private const float CgThumbSize = 86f;

        /// <summary>区切りの見出しが取る高さ。</summary>
        private const float CgSectionHeight = 26f;

        /// <summary>一覧の幅。パネル(740)の内側に収める。</summary>
        private const float CgViewWidth = 700f;

        /// <summary>
        /// 一覧の高さと中心。**音のタブより広く取れる。**
        /// CGタブでは下の試聴バーを隠しているので、その分まで使う。
        /// </summary>
        private const float CgViewHeight = 390f;
        private const float CgViewCenterY = -58f;

        private static readonly Art[] Arts =
        {
            // --- 女の子（15枚） ---
            new Art("女の子/tutorial_girl_body", "tutorial_girl_body", "女の子"),
            new Art("女の子/tutorial_girl_face", "tutorial_girl_face", "女の子"),
            new Art("女の子/どや顔", "どや顔", "女の子"),
            new Art("女の子/コミカル", "コミカル", "女の子"),
            new Art("女の子/ピース笑顔", "ピース笑顔", "女の子"),
            new Art("女の子/ピース身体", "ピース身体", "女の子"),
            new Art("女の子/喜び", "喜び", "女の子"),
            new Art("女の子/困り", "困り", "女の子"),
            new Art("女の子/怒り", "怒り", "女の子"),
            new Art("女の子/目を閉じる顔", "目を閉じる顔", "女の子"),
            new Art("女の子/目を開ける顔", "目を開ける顔", "女の子"),
            new Art("女の子/笑顔", "笑顔", "女の子"),
            new Art("女の子/通常時身体", "通常時身体", "女の子"),
            new Art("女の子/通常顔", "通常顔", "女の子"),
            new Art("女の子/驚き？", "驚き？", "女の子"),
            // --- 牌（68枚） ---
            new Art("麻雀牌/イーピン", "イーピン", "牌"),
            new Art("麻雀牌/東", "東", "牌"),
            new Art("麻雀牌/東自身打", "東自身打", "牌"),
            new Art("麻雀牌/筒1", "筒1", "牌"),
            new Art("麻雀牌/筒1自身打", "筒1自身打", "牌"),
            new Art("麻雀牌/筒2", "筒2", "牌"),
            new Art("麻雀牌/筒2自身打", "筒2自身打", "牌"),
            new Art("麻雀牌/筒3", "筒3", "牌"),
            new Art("麻雀牌/筒3自身打", "筒3自身打", "牌"),
            new Art("麻雀牌/筒4", "筒4", "牌"),
            new Art("麻雀牌/筒4自身打", "筒4自身打", "牌"),
            new Art("麻雀牌/筒5", "筒5", "牌"),
            new Art("麻雀牌/筒5ドラ自身打", "筒5ドラ自身打", "牌"),
            new Art("麻雀牌/筒5自身打", "筒5自身打", "牌"),
            new Art("麻雀牌/筒5赤", "筒5赤", "牌"),
            new Art("麻雀牌/筒6", "筒6", "牌"),
            new Art("麻雀牌/筒6自身打", "筒6自身打", "牌"),
            new Art("麻雀牌/筒7", "筒7", "牌"),
            new Art("麻雀牌/筒7自身打", "筒7自身打", "牌"),
            new Art("麻雀牌/筒8", "筒8", "牌"),
            new Art("麻雀牌/筒8自身打", "筒8自身打", "牌"),
            new Art("麻雀牌/筒9", "筒9", "牌"),
            new Art("麻雀牌/筒9自身打", "筒9自身打", "牌"),
            new Art("麻雀牌/索1", "索1", "牌"),
            new Art("麻雀牌/索2", "索2", "牌"),
            new Art("麻雀牌/索3", "索3", "牌"),
            new Art("麻雀牌/索4", "索4", "牌"),
            new Art("麻雀牌/索5", "索5", "牌"),
            new Art("麻雀牌/索6", "索6", "牌"),
            new Art("麻雀牌/索7", "索7", "牌"),
            new Art("麻雀牌/索8", "索8", "牌"),
            new Art("麻雀牌/索9", "索9", "牌"),
            new Art("麻雀牌/能力発動ボタン", "能力発動ボタン", "牌"),
            new Art("麻雀牌/能力発動ボタン２", "能力発動ボタン２", "牌"),
            new Art("麻雀牌/草2自身打", "草2自身打", "牌"),
            new Art("麻雀牌/草3自身打", "草3自身打", "牌"),
            new Art("麻雀牌/草4自身打", "草4自身打", "牌"),
            new Art("麻雀牌/草5ドラ自身打", "草5ドラ自身打", "牌"),
            new Art("麻雀牌/草5自身打", "草5自身打", "牌"),
            new Art("麻雀牌/草6自身打", "草6自身打", "牌"),
            new Art("麻雀牌/草7自身打", "草7自身打", "牌"),
            new Art("麻雀牌/草8自身打", "草8自身打", "牌"),
            new Art("麻雀牌/草9自身打", "草9自身打", "牌"),
            new Art("麻雀牌/草１自身打", "草１自身打", "牌"),
            new Art("麻雀牌/萬1", "萬1", "牌"),
            new Art("麻雀牌/萬1自身打", "萬1自身打", "牌"),
            new Art("麻雀牌/萬2", "萬2", "牌"),
            new Art("麻雀牌/萬2自身打", "萬2自身打", "牌"),
            new Art("麻雀牌/萬3", "萬3", "牌"),
            new Art("麻雀牌/萬3自身打", "萬3自身打", "牌"),
            new Art("麻雀牌/萬4", "萬4", "牌"),
            new Art("麻雀牌/萬4自身打", "萬4自身打", "牌"),
            new Art("麻雀牌/萬5", "萬5", "牌"),
            new Art("麻雀牌/萬5ドラ自身打", "萬5ドラ自身打", "牌"),
            new Art("麻雀牌/萬5自身打", "萬5自身打", "牌"),
            new Art("麻雀牌/萬6", "萬6", "牌"),
            new Art("麻雀牌/萬6自身打", "萬6自身打", "牌"),
            new Art("麻雀牌/萬7", "萬7", "牌"),
            new Art("麻雀牌/萬7自身打", "萬7自身打", "牌"),
            new Art("麻雀牌/萬8", "萬8", "牌"),
            new Art("麻雀牌/萬8自身打", "萬8自身打", "牌"),
            new Art("麻雀牌/萬9", "萬9", "牌"),
            new Art("麻雀牌/萬9自身打", "萬9自身打", "牌"),
            new Art("麻雀牌/裏牌", "裏牌", "牌"),
            new Art("麻雀牌/西", "西", "牌"),
            new Art("麻雀牌/西自身打", "西自身打", "牌"),
            new Art("麻雀牌/赤5索", "赤5索", "牌"),
            new Art("麻雀牌/赤5萬", "赤5萬", "牌"),
            // --- 牌（敵の河）（32枚） ---
            new Art("Enemyphai/東敵打", "東敵打", "牌（敵の河）"),
            new Art("Enemyphai/筒1敵打", "筒1敵打", "牌（敵の河）"),
            new Art("Enemyphai/筒2敵打", "筒2敵打", "牌（敵の河）"),
            new Art("Enemyphai/筒3敵打", "筒3敵打", "牌（敵の河）"),
            new Art("Enemyphai/筒4敵打", "筒4敵打", "牌（敵の河）"),
            new Art("Enemyphai/筒5ドラ敵打", "筒5ドラ敵打", "牌（敵の河）"),
            new Art("Enemyphai/筒5敵打", "筒5敵打", "牌（敵の河）"),
            new Art("Enemyphai/筒6敵打", "筒6敵打", "牌（敵の河）"),
            new Art("Enemyphai/筒7敵打", "筒7敵打", "牌（敵の河）"),
            new Art("Enemyphai/筒8敵打", "筒8敵打", "牌（敵の河）"),
            new Art("Enemyphai/筒9敵打", "筒9敵打", "牌（敵の河）"),
            new Art("Enemyphai/草2敵打", "草2敵打", "牌（敵の河）"),
            new Art("Enemyphai/草3敵打", "草3敵打", "牌（敵の河）"),
            new Art("Enemyphai/草4敵打", "草4敵打", "牌（敵の河）"),
            new Art("Enemyphai/草5ドラ敵打", "草5ドラ敵打", "牌（敵の河）"),
            new Art("Enemyphai/草5敵打", "草5敵打", "牌（敵の河）"),
            new Art("Enemyphai/草6敵打", "草6敵打", "牌（敵の河）"),
            new Art("Enemyphai/草7敵打", "草7敵打", "牌（敵の河）"),
            new Art("Enemyphai/草8敵打", "草8敵打", "牌（敵の河）"),
            new Art("Enemyphai/草9敵打", "草9敵打", "牌（敵の河）"),
            new Art("Enemyphai/草１敵打", "草１敵打", "牌（敵の河）"),
            new Art("Enemyphai/萬1敵打", "萬1敵打", "牌（敵の河）"),
            new Art("Enemyphai/萬2敵打", "萬2敵打", "牌（敵の河）"),
            new Art("Enemyphai/萬3敵打", "萬3敵打", "牌（敵の河）"),
            new Art("Enemyphai/萬4敵打", "萬4敵打", "牌（敵の河）"),
            new Art("Enemyphai/萬5ドラ敵打", "萬5ドラ敵打", "牌（敵の河）"),
            new Art("Enemyphai/萬5敵打", "萬5敵打", "牌（敵の河）"),
            new Art("Enemyphai/萬6敵打", "萬6敵打", "牌（敵の河）"),
            new Art("Enemyphai/萬7敵打", "萬7敵打", "牌（敵の河）"),
            new Art("Enemyphai/萬8敵打", "萬8敵打", "牌（敵の河）"),
            new Art("Enemyphai/萬9敵打", "萬9敵打", "牌（敵の河）"),
            new Art("Enemyphai/西敵打", "西敵打", "牌（敵の河）"),
            // --- 演出・画面（4枚） ---
            new Art("Effects/attack_ripple_sheet_8x64", "attack_ripple_sheet_8x64", "演出・画面"),
            new Art("Title/TitleSparkle", "TitleSparkle", "演出・画面"),
            new Art("Tutorial/案内板_満貫", "案内板_満貫", "演出・画面"),
            new Art("Tutorial/麻雀のあそびかた", "麻雀のあそびかた", "演出・画面"),
            // --- UI・背景（45枚） ---
            new Art("2", "2", "UI・背景"),
            new Art("360_F_586519035_hSbo8FVHSEMcS1vjQsGB4tT9umf4dx3U", "360_F_586519035_hSbo8FVHSEMcS1vjQsGB4tT9umf4dx3U", "UI・背景"),
            new Art("8742ebda7ef654e9", "8742ebda7ef654e9", "UI・背景"),
            new Art("ENEHPUI_COVER", "ENEHPUI_COVER", "UI・背景"),
            new Art("ENEHPUI_NO", "ENEHPUI_NO", "UI・背景"),
            new Art("ENEHPUI_brad", "ENEHPUI_brad", "UI・背景"),
            new Art("HPCOVER", "HPCOVER", "UI・背景"),
            new Art("HPUCOVER", "HPUCOVER", "UI・背景"),
            new Art("HPUI0heart", "HPUI0heart", "UI・背景"),
            new Art("HPUITimer2", "HPUITimer2", "UI・背景"),
            new Art("HPUIhaert", "HPUIhaert", "UI・背景"),
            new Art("HPUNDER", "HPUNDER", "UI・背景"),
            new Art("HPnoread", "HPnoread", "UI・背景"),
            new Art("Hp画面", "Hp画面", "UI・背景"),
            new Art("Logbutton", "Logbutton", "UI・背景"),
            new Art("UI", "UI", "UI・背景"),
            new Art("UI/RonHands/ron_hand_01", "ron_hand_01", "UI・背景"),
            new Art("UI/RonHands/ron_hand_02", "ron_hand_02", "UI・背景"),
            new Art("UI/RonHands/ron_hand_03", "ron_hand_03", "UI・背景"),
            new Art("UI_Anim1", "UI_Anim1", "UI・背景"),
            new Art("UI_Anim10", "UI_Anim10", "UI・背景"),
            new Art("UI_Anim2", "UI_Anim2", "UI・背景"),
            new Art("UI_Anim3", "UI_Anim3", "UI・背景"),
            new Art("UI_Anim4", "UI_Anim4", "UI・背景"),
            new Art("UI_Anim5", "UI_Anim5", "UI・背景"),
            new Art("UI_Anim6", "UI_Anim6", "UI・背景"),
            new Art("UI_Anim7", "UI_Anim7", "UI・背景"),
            new Art("UI_Anim8", "UI_Anim8", "UI・背景"),
            new Art("UI_Anim9", "UI_Anim9", "UI・背景"),
            new Art("alert", "alert", "UI・背景"),
            new Art("background", "background", "UI・背景"),
            new Art("スクリーンショット 2026-06-23 172753", "スクリーンショット 2026-06-23 172753", "UI・背景"),
            new Art("チェックマーク", "チェックマーク", "UI・背景"),
            new Art("マウスカーソル", "マウスカーソル", "UI・背景"),
            new Art("マージャン卓", "マージャン卓", "UI・背景"),
            new Art("メスガキ", "メスガキ", "UI・背景"),
            new Art("人型メーター", "人型メーター", "UI・背景"),
            new Art("吹き出し", "吹き出し", "UI・背景"),
            new Art("吹き出し2", "吹き出し2", "UI・背景"),
            new Art("契約書", "契約書", "UI・背景"),
            new Art("役一覧", "役一覧", "UI・背景"),
            new Art("待ち牌.", "待ち牌.", "UI・背景"),
            new Art("能力UIcloseButton", "能力UIcloseButton", "UI・背景"),
            new Art("設定ボタン画像", "設定ボタン画像", "UI・背景"),
            new Art("透視アイコン", "透視アイコン", "UI・背景"),
        };

        private sealed class CgCell
        {
            public Image Thumb;
            public string Path;
            public float Top;       // Content の上端からの深さ（正の値）
            public bool Loaded;
        }

        private readonly List<CgCell> cgCells = new List<CgCell>();

        /// <summary>こちらで `Sprite.Create` した分。**自分で捨てる。**</summary>
        private readonly List<Sprite> cgCreatedSprites = new List<Sprite>();
        private RectTransform cgViewport;
        private RectTransform cgContent;

        private void BuildCgPage(Transform parent)
        {
            cgCells.Clear();

            Label(parent, "CgHead", $"いま使っている絵　{Arts.Length}枚", new Vector2(0f, 158f),
                new Vector2(CgViewWidth, 22f), 14f, TextAlignmentOptions.Left, Marker);

            // 枠。**透明でも raycastTarget は要る**（切るとホイールを拾えない）
            var viewport = NewImage(parent, "CgViewport", new Color(0f, 0f, 0f, 0f));
            Center(viewport.rectTransform, new Vector2(CgViewWidth, CgViewHeight));
            viewport.rectTransform.anchoredPosition = new Vector2(0f, CgViewCenterY);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            cgViewport = viewport.rectTransform;

            var contentGo = NewEmpty(viewport.transform, "Content");
            cgContent = contentGo.GetComponent<RectTransform>();
            cgContent.anchorMin = new Vector2(0.5f, 1f);
            cgContent.anchorMax = new Vector2(0.5f, 1f);
            cgContent.pivot = new Vector2(0.5f, 1f);
            cgContent.anchoredPosition = Vector2.zero;

            float y = 0f;
            string section = null;
            int column = 0;

            for (int i = 0; i < Arts.Length; i++)
            {
                if (Arts[i].Section != section)
                {
                    // 前の段の途中なら、見出しの前で行を送る
                    if (column != 0)
                    {
                        column = 0;
                        y += CgCellHeight;
                    }
                    section = Arts[i].Section;

                    var head = Label(cgContent, "Sec_" + section, section,
                        new Vector2(0f, 0f), new Vector2(CgViewWidth - 16f, CgSectionHeight),
                        13f, TextAlignmentOptions.Left, Marker);
                    var hr = head.rectTransform;
                    hr.anchorMin = new Vector2(0.5f, 1f);
                    hr.anchorMax = new Vector2(0.5f, 1f);
                    hr.pivot = new Vector2(0.5f, 1f);
                    hr.anchoredPosition = new Vector2(0f, -y);
                    y += CgSectionHeight;
                }

                float x = (column - (CgColumns - 1) * 0.5f) * CgCellWidth;

                var cell = NewEmpty(cgContent, "Cell_" + i);
                var cr = cell.GetComponent<RectTransform>();
                cr.anchorMin = new Vector2(0.5f, 1f);
                cr.anchorMax = new Vector2(0.5f, 1f);
                cr.pivot = new Vector2(0.5f, 1f);
                cr.sizeDelta = new Vector2(CgCellWidth, CgCellHeight);
                cr.anchoredPosition = new Vector2(x, -y);

                var thumb = NewImage(cell.transform, "Thumb", new Color(1f, 1f, 1f, 0f));
                Center(thumb.rectTransform, new Vector2(CgThumbSize, CgThumbSize));
                thumb.rectTransform.anchoredPosition = new Vector2(0f, -CgThumbSize * 0.5f - 4f);
                thumb.preserveAspect = true;
                thumb.raycastTarget = false;

                // **名前は2行まで。** 牌の名前は短いが、`tutorial_girl_body` のような
                // 長いものが混ざるので、1行に押し込むと読めなくなる
                var name = Label(cell.transform, "Name", Arts[i].Label,
                    new Vector2(0f, -CgCellHeight + 13f), new Vector2(CgCellWidth - 6f, 24f),
                    9.5f, TextAlignmentOptions.Center, TextMain);
                name.enableWordWrapping = true;
                name.overflowMode = TextOverflowModes.Ellipsis;

                cgCells.Add(new CgCell { Thumb = thumb, Path = Arts[i].Path, Top = y });

                column++;
                if (column >= CgColumns)
                {
                    column = 0;
                    y += CgCellHeight;
                }
            }
            if (column != 0) y += CgCellHeight;

            cgContent.sizeDelta = new Vector2(CgViewWidth, y + 8f);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = cgContent;
            scroll.viewport = cgViewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.inertia = false;
            scroll.onValueChanged.AddListener(_ => LoadVisibleCgThumbs());

            cgContent.anchoredPosition = Vector2.zero;
            scroll.verticalNormalizedPosition = 1f;

            LoadVisibleCgThumbs();
        }

        /// <summary>
        /// 枠に入っている（もうすぐ入る）絵だけ読む。
        /// **一度読んだものは離さない。** 行き来のたびに読み直すと、
        /// スクロールするだけで引っかかる。解放は画面を閉じるときにまとめて行う。
        /// </summary>
        private void LoadVisibleCgThumbs()
        {
            if (cgContent == null) return;

            // Content は上端基準で下へ伸びる。いくつぶん下げたかがそのまま見ている深さ
            float scrolled = cgContent.anchoredPosition.y;
            float top = scrolled - CgCellHeight;                       // 少し上から
            float bottom = scrolled + CgViewHeight + CgCellHeight;     // 少し下まで

            for (int i = 0; i < cgCells.Count; i++)
            {
                var cell = cgCells[i];
                if (cell.Loaded) continue;
                if (cell.Top < top || cell.Top > bottom) continue;

                var sprite = LoadArtSprite(cell.Path);
                cell.Loaded = true;
                if (sprite == null || cell.Thumb == null) continue;

                cell.Thumb.sprite = sprite;
                cell.Thumb.color = Color.white;
            }
        }

        /// <summary>
        /// 1枚読む。**Sprite で取れないときは Texture2D から作る。**
        /// 取り込み方が Sprite になっていない絵が混ざっていて、
        /// `Resources.Load&lt;Sprite&gt;` だけだと何も出ない。
        /// </summary>
        private Sprite LoadArtSprite(string path)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null) return sprite;

            var tex = Resources.Load<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning($"[CollectionUI] 絵が読めません: Resources/{path}");
                return null;
            }

            var made = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            cgCreatedSprites.Add(made);
            return made;
        }

        /// <summary>
        /// 読み込んだ絵を手放す。**閉じるときに必ず呼ぶ。**
        /// 抱えたままだと、コレクションを開くたびにテクスチャが積もる。
        /// </summary>
        private void ReleaseCgThumbs()
        {
            // まず参照を外す。付けたままだと「使われている」と見なされて落ちない。
            // **枠そのものは残す。** `Open` は作り直さず同じ画面を出すので、
            // ここで消すと2回目から絵が1枚も出なくなる。読み直せるよう旗だけ倒す
            foreach (var cell in cgCells)
            {
                if (cell.Thumb != null)
                {
                    cell.Thumb.sprite = null;
                    cell.Thumb.color = new Color(1f, 1f, 1f, 0f);
                }
                cell.Loaded = false;
            }

            // **自分で作った Sprite だけ Destroy する。**
            // `Resources.Load` で得たものはアセット本体なので、壊してはいけない
            foreach (var made in cgCreatedSprites)
            {
                if (made != null) Destroy(made);
            }
            cgCreatedSprites.Clear();

            // 参照が切れたテクスチャをここで落とす
            Resources.UnloadUnusedAssets();
        }
    }
}
