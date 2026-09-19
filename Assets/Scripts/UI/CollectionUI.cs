using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    /// <summary>
    /// コレクション画面（2026-09-11）。いまは音楽だけ中身がある。
    ///
    /// **シーンには保存せず、実行時に専用 Canvas として組み立てる。**
    /// RoomScreenUI と同じ作りにしてある。シーンの YAML を触らずに済むので、
    /// 既存の配線を壊す心配がない。参照解像度も 800x600 で揃えてある。
    ///
    /// **再生は AudioManager を使わず、この画面専用の AudioSource で行う。**
    /// AudioManager 側はフェイズに応じて曲を差し替える仕組みを持っているので、
    /// そこへ試聴を混ぜると状態が食い違う。開いている間だけタイトルBGMを止め、
    /// 閉じるときに戻す。
    /// </summary>
    public sealed class CollectionUI : MonoBehaviour
    {
        private static readonly Color TextMain = new Color32(240, 232, 236, 255);
        private static readonly Color TextDim = new Color32(170, 156, 164, 255);
        private static readonly Color Marker = new Color32(214, 40, 62, 255);
        private static readonly Color PanelBg = new Color32(28, 18, 26, 252);
        private static readonly Color PanelEdge = new Color32(80, 48, 62, 255);
        private static readonly Color RowSelected = new Color32(64, 28, 40, 255);

        private struct Track
        {
            public string Id;       // Resources のファイル名
            public string Folder;   // "Bgm" / "Stingers"
            public string Label;    // 画面に出す名前
            public Track(string folder, string id, string label) { Folder = folder; Id = id; Label = label; }
        }

        // 並びは実際の対局の流れ順。ファイル名ではなく場面で探せるようにする。
        private static readonly Track[] Bgms =
        {
            new Track("Bgm", "bgm_title",        "タイトル"),
            new Track("Bgm", "bgm_tutorial",     "チュートリアル"),
            new Track("Bgm", "bgm_prepare",      "配牌・手牌選択"),
            new Track("Bgm", "bgm_betting",      "賭け"),
            new Track("Bgm", "bgm_tension",      "先行・後攻"),
            new Track("Bgm", "bgm_field_1",      "場 I　序盤"),
            new Track("Bgm", "bgm_field_2",      "場 II"),
            new Track("Bgm", "bgm_field_3",      "場 III"),
            new Track("Bgm", "bgm_field_4",      "場 IV　終盤"),
            new Track("Bgm", "bgm_discard",      "打牌"),
            new Track("Bgm", "bgm_discard_hot",  "打牌　激"),
            new Track("Bgm", "bgm_ron",          "ロン・決着"),
            new Track("Bgm", "bgm_draw",         "流局"),
            new Track("Bgm", "bgm_result",       "結果"),
            new Track("Bgm", "bgm_win",          "勝ち"),
            new Track("Bgm", "bgm_lose",         "負け"),
        };

        /// <summary>
        /// 「追加曲」タブの曲。**場面に割り当てていない**（2026-09-19 に追加）。
        ///
        /// ユーザーが別に作ったオリジナル曲（C:\Users\akira\Music\D_N_A_original_bgm\）を、
        /// 44100Hz/モノラル/16bit・RMS −18.91dBFS（既存曲の中央値）に揃えて取り込んだもの。
        /// 2026-09-19 にリマスター版（*_gm_remaster_v3_final / *_v2_final の **MP3**）へ差し替え、4曲追加。
        /// **同名の WAV は仕上げ前で −42dB と小さいので使わない。** 仕上げ済みは MP3。
        /// リマスター版は**無音で終わる**（末尾0.6〜1.2秒）ので、ループすると少し間が空く。
        /// 対局中には流れない。場面に当てるときは AudioManager の Tempos 表も足すこと。
        ///
        /// **「音楽」タブの列に足さないこと。** 行は y = 132 - i*21 で並べるだけで
        /// スクロールが無く、16曲で既に再生バーの上端に届いている。
        /// 足すと下の行が再生バーの裏へ潜って押せなくなる。
        /// </summary>
        private static readonly Track[] Originals =
        {
            new Track("Bgm", "bgm_ex_midnight",  "真夜中のアーケード"),
            new Track("Bgm", "bgm_ex_glitch",    "グリッチ"),
            new Track("Bgm", "bgm_ex_lofi",      "夕暮れのローファイ"),
            new Track("Bgm", "bgm_ex_summer",    "夏の空"),
            new Track("Bgm", "bgm_ex_fantasy",   "はるかな地平線"),
            new Track("Bgm", "bgm_ex_sporty",    "カウントダウン"),
            new Track("Bgm", "bgm_ex_mystery",   "時計じかけの謎"),
            new Track("Bgm", "bgm_ex_steampunk", "蒸気の夜想曲"),
            new Track("Bgm", "bgm_ex_incident",  "ひび割れた事件"),
            new Track("Bgm", "bgm_ex_surreal",   "奇妙な回廊"),
        };

        /// <summary>
        /// 「効果音」タブ右列: チュートリアルでセリフに合わせて鳴る効果音（2026-09-19 に追加）。
        /// 意味は km-docs/tutorial/04_演出と音_統合.md。並びは意味の対が隣り合うようにしてある
        /// （一滴↔一滴、ひび↔崩れる亀裂、選択↔選ばされた、能力の予兆↔崩壊）。
        /// </summary>
        private static readonly Track[] TutorialSes =
        {
            new Track("Stingers", "se_paper",       "契約書"),
            new Track("Stingers", "se_drop",        "一滴"),
            new Track("Stingers", "se_tube_slow",   "管を流れる　遅"),
            new Track("Stingers", "se_tube_fast",   "管を流れる　速"),
            new Track("Stingers", "se_choice",      "選択"),
            new Track("Stingers", "se_choice_dark", "選ばされた"),
            new Track("Stingers", "se_stack",       "積み上がる"),
            new Track("Stingers", "se_two_pulses",  "二つの鼓動"),
            new Track("Stingers", "se_crack",       "ひび"),
            new Track("Stingers", "se_crack_long",  "崩れる亀裂"),
            new Track("Stingers", "se_ability_1",   "能力の予兆　1"),
            new Track("Stingers", "se_ability_2",   "能力の予兆　2"),
            new Track("Stingers", "se_ability_3",   "能力の予兆　3"),
            new Track("Stingers", "se_collapse",    "崩壊"),
        };

        private static readonly Track[] Stingers =
        {
            new Track("Stingers", "br_band_open",  "黒帯　開く"),
            new Track("Stingers", "br_band_close", "黒帯　閉じる"),
            new Track("Stingers", "br_fall",       "暗転"),
            new Track("Stingers", "br_riser",      "明転"),
            new Track("Stingers", "br_blackout",   "着弾"),
            new Track("Stingers", "st_ron_impact", "ロンの一撃"),
            new Track("Stingers", "st_draw",       "流局"),
            new Track("Stingers", "st_ability",    "能力発動"),
            new Track("Stingers", "st_riichi",     "リーチ"),
            new Track("Stingers", "st_danger",     "危険牌"),
            new Track("Stingers", "st_bet_lock",   "賭け金確定"),
            new Track("Stingers", "st_win",        "勝ち"),
            new Track("Stingers", "st_lose",       "負け"),
        };

        private GameObject root;
        private GameObject musicPage;
        private GameObject originalsPage;   // 「追加曲」タブ
        private GameObject sePage;          // 「効果音」タブ
        private GameObject cgPage;
        private GameObject yakuPage;
        private TMP_FontAsset font;
        private Action onClosed;

        private AudioSource preview;
        private AudioLowPassFilter previewFilter;

        private readonly List<Track> flat = new List<Track>();
        private readonly List<Image> rowBgs = new List<Image>();
        private int selected = -1;

        private TextMeshProUGUI nowPlayingText;
        private TextMeshProUGUI timeText;
        private TextMeshProUGUI playLabel;
        private Slider seek;
        private bool seekIsBeingSetByCode;

        public bool IsOpen { get { return root != null && root.activeSelf; } }

        public void Open(Action closed)
        {
            onClosed = closed;
            if (root == null) Build();
            if (root == null) return;

            root.SetActive(true);
            ShowTab(0);

            // 試聴の邪魔になるので、開いている間はゲーム側のBGMを止める
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio != null) audio.StopBGM();
        }

        public void Close()
        {
            StopPreview();
            if (root != null) root.SetActive(false);

            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio != null) audio.PlayTitleBgm();

            if (onClosed != null) onClosed();
        }

        // ---------------- 組み立て ----------------

        private void Build()
        {
            font = BorrowJapaneseFont();

            root = new GameObject("CollectionScreen", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UISortingOrders.CollectionScreen;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.matchWidthOrHeight = 0.5f;
            Stretch(root.GetComponent<RectTransform>());

            var scrim = NewImage(root.transform, "Scrim", new Color(0f, 0f, 0f, 0.86f));
            Stretch(scrim.rectTransform);
            scrim.raycastTarget = true;

            var panel = NewImage(root.transform, "Panel", PanelBg);
            Center(panel.rectTransform, new Vector2(740f, 552f));
            panel.raycastTarget = true;
            var edge = NewImage(panel.transform, "Edge", PanelEdge);
            Center(edge.rectTransform, new Vector2(740f, 2f));
            edge.rectTransform.anchoredPosition = new Vector2(0f, 276f);
            edge.raycastTarget = false;

            Label(panel.transform, "Heading", "コレクション", new Vector2(-250f, 244f), new Vector2(240f, 34f),
                24f, TextAlignmentOptions.Left, TextMain);
            Button(panel.transform, "Close", "もどる", new Vector2(300f, 244f), new Vector2(110f, 32f), 17f, Close);

            BuildTabs(panel.transform);

            musicPage = NewEmpty(panel.transform, "MusicPage");
            Stretch(musicPage.GetComponent<RectTransform>());
            BuildMusicPage(musicPage.transform);

            // **音楽ページのあとに作る。** BuildMusicPage が flat を空にするので、
            // 先に作ると追加曲の行が消える。再生バーはパネル直下にあり、どのタブでも共有される
            originalsPage = NewEmpty(panel.transform, "OriginalsPage");
            Stretch(originalsPage.GetComponent<RectTransform>());
            BuildColumn(originalsPage.transform, -178f, "オリジナル曲", Originals);

            // 「効果音」タブ。左は演出の効果音、右はチュートリアルの効果音。
            // **行はスクロールしない**ので、1列14行まで（それ以上は再生バーの裏へ潜る）
            sePage = NewEmpty(panel.transform, "SePage");
            Stretch(sePage.GetComponent<RectTransform>());
            BuildColumn(sePage.transform, -178f, "演出", Stingers);
            BuildColumn(sePage.transform, 178f, "チュートリアル", TutorialSes);

            cgPage = NewEmpty(panel.transform, "CgPage");
            Stretch(cgPage.GetComponent<RectTransform>());
            Label(cgPage.transform, "Soon", "準備中", new Vector2(0f, 20f), new Vector2(400f, 40f),
                20f, TextAlignmentOptions.Center, TextDim);

            yakuPage = NewEmpty(panel.transform, "YakuPage");
            Stretch(yakuPage.GetComponent<RectTransform>());
            Label(yakuPage.transform, "Soon", "準備中", new Vector2(0f, 20f), new Vector2(400f, 40f),
                20f, TextAlignmentOptions.Center, TextDim);

            BuildPlayer(panel.transform);
            EnsurePreviewSource();
        }

        private readonly List<Image> tabMarks = new List<Image>();

        private void BuildTabs(Transform parent)
        {
            string[] names = { "音楽", "追加曲", "効果音", "CG", "役" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                float x = -300f + i * 92f;
                Button(parent, "Tab" + i, names[i], new Vector2(x, 202f), new Vector2(86f, 30f), 17f,
                    () => ShowTab(index));

                var mark = NewImage(parent, "TabMark" + i, Marker);
                Center(mark.rectTransform, new Vector2(86f, 3f));
                mark.rectTransform.anchoredPosition = new Vector2(x, 185f);
                mark.raycastTarget = false;
                tabMarks.Add(mark);
            }
        }

        private void ShowTab(int index)
        {
            if (musicPage != null) musicPage.SetActive(index == 0);
            if (originalsPage != null) originalsPage.SetActive(index == 1);
            if (sePage != null) sePage.SetActive(index == 2);
            if (cgPage != null) cgPage.SetActive(index == 3);
            if (yakuPage != null) yakuPage.SetActive(index == 4);
            for (int i = 0; i < tabMarks.Count; i++)
                if (tabMarks[i] != null) tabMarks[i].enabled = (i == index);
        }

        private void BuildMusicPage(Transform parent)
        {
            flat.Clear();
            rowBgs.Clear();

            // 効果音は「効果音」タブへ移した（2026-09-19 の指示）。
            // **空いた右の列へBGMの後ろを回す。** 行はスクロールしないので、1列16行だと
            // 最後の「負け」が再生バーの裏に隠れて押せなかった（以前からの不具合）。
            // 対局の流れの切れ目（ロンで決着するところ）で分ける
            BuildColumn(parent, -178f, "BGM　対局", Slice(Bgms, 0, BgmSplitAt));
            BuildColumn(parent, 178f, "BGM　決着", Slice(Bgms, BgmSplitAt, Bgms.Length - BgmSplitAt));
        }

        /// <summary>BGM を2列に分ける位置。ここから後ろ（ロン・決着〜負け）が右の列。</summary>
        private const int BgmSplitAt = 11;

        private static Track[] Slice(Track[] src, int start, int count)
        {
            var dst = new Track[count];
            System.Array.Copy(src, start, dst, 0, count);
            return dst;
        }

        private void BuildColumn(Transform parent, float centerX, string heading, Track[] tracks)
        {
            Label(parent, "Head_" + heading, heading, new Vector2(centerX, 158f), new Vector2(330f, 22f),
                14f, TextAlignmentOptions.Left, Marker);

            for (int i = 0; i < tracks.Length; i++)
            {
                int flatIndex = flat.Count;
                flat.Add(tracks[i]);

                float y = 132f - i * 21f;

                var rowBg = NewImage(parent, "Row_" + tracks[i].Id, new Color(0f, 0f, 0f, 0f));
                Center(rowBg.rectTransform, new Vector2(330f, 20f));
                rowBg.rectTransform.anchoredPosition = new Vector2(centerX, y);
                rowBg.raycastTarget = true;
                rowBgs.Add(rowBg);

                var btn = rowBg.gameObject.AddComponent<Button>();
                btn.targetGraphic = rowBg;
                btn.onClick.AddListener(() => PlayIndex(flatIndex));

                Label(rowBg.transform, "No", (i + 1).ToString("00"), new Vector2(-146f, 0f), new Vector2(28f, 18f),
                    11f, TextAlignmentOptions.Left, TextDim);
                Label(rowBg.transform, "Name", tracks[i].Label, new Vector2(-40f, 0f), new Vector2(180f, 18f),
                    13f, TextAlignmentOptions.Left, TextMain);
                Label(rowBg.transform, "File", tracks[i].Id, new Vector2(108f, 0f), new Vector2(110f, 18f),
                    9.5f, TextAlignmentOptions.Right, TextDim);
            }
        }

        private Toggle muffleToggle;

        private void BuildPlayer(Transform parent)
        {
            var bar = NewImage(parent, "PlayerBar", new Color32(18, 11, 18, 255));
            Center(bar.rectTransform, new Vector2(700f, 86f));
            bar.rectTransform.anchoredPosition = new Vector2(0f, -218f);
            bar.raycastTarget = false;

            Button(bar.transform, "PlayPause", "▶", new Vector2(-316f, 20f), new Vector2(40f, 32f), 20f, TogglePlay);
            playLabel = bar.transform.Find("PlayPause/Text").GetComponent<TextMeshProUGUI>();
            Button(bar.transform, "Stop", "■", new Vector2(-272f, 20f), new Vector2(40f, 32f), 17f, StopPreview);

            nowPlayingText = Label(bar.transform, "NowPlaying", "曲を選んでください", new Vector2(-40f, 20f),
                new Vector2(420f, 24f), 14f, TextAlignmentOptions.Left, TextMain);
            timeText = Label(bar.transform, "Time", "00:00 / 00:00", new Vector2(288f, 20f), new Vector2(120f, 24f),
                13f, TextAlignmentOptions.Right, TextDim);

            BuildSeek(bar.transform);

            // **対局中は打牌フェイズ以外に 1000Hz のローパスがかかる**（AudioManager.MuffledCutoff）。
            // 実際にどう聞こえるかはここで確かめられないと分からないので、同じフィルタを試聴にも付ける。
            muffleToggle = BuildToggle(bar.transform, "Muffle", "対局中のこもり（1000Hz）を再現",
                new Vector2(-190f, -26f), 12f);
            muffleToggle.onValueChanged.AddListener(on =>
            {
                if (previewFilter != null) previewFilter.enabled = on;
            });
        }

        private void BuildSeek(Transform parent)
        {
            var go = new GameObject("Seek", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Center(rect, new Vector2(300f, 16f));
            rect.anchoredPosition = new Vector2(196f, -26f);

            var bg = NewImage(go.transform, "Background", new Color32(60, 40, 50, 255));
            Stretch(bg.rectTransform);
            bg.rectTransform.sizeDelta = new Vector2(0f, -10f);

            var fillArea = NewEmpty(go.transform, "FillArea");
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            Stretch(fillAreaRect);
            fillAreaRect.sizeDelta = new Vector2(0f, -10f);
            var fill = NewImage(fillArea.transform, "Fill", Marker);
            Stretch(fill.rectTransform);

            seek = go.GetComponent<Slider>();
            seek.fillRect = fill.rectTransform;
            seek.targetGraphic = bg;
            seek.minValue = 0f;
            seek.maxValue = 1f;
            seek.value = 0f;
            seek.onValueChanged.AddListener(v =>
            {
                // 再生に合わせてコードから動かしている最中は、掴まれたと誤解しない
                if (seekIsBeingSetByCode) return;
                if (preview != null && preview.clip != null)
                    preview.time = Mathf.Clamp(v * preview.clip.length, 0f, preview.clip.length - 0.05f);
            });
        }

        // ---------------- 再生 ----------------

        private void EnsurePreviewSource()
        {
            if (preview != null) return;
            var host = new GameObject("CollectionPreview", typeof(AudioSource), typeof(AudioLowPassFilter));
            host.transform.SetParent(root.transform, false);
            preview = host.GetComponent<AudioSource>();
            preview.playOnAwake = false;
            preview.loop = false;
            previewFilter = host.GetComponent<AudioLowPassFilter>();
            previewFilter.cutoffFrequency = 1000f;   // AudioManager.MuffledCutoff と同じ値
            previewFilter.enabled = false;
        }

        private void PlayIndex(int index)
        {
            if (index < 0 || index >= flat.Count) return;
            EnsurePreviewSource();

            var t = flat[index];
            var clip = Resources.Load<AudioClip>(t.Folder + "/" + t.Id);
            if (clip == null)
            {
                Debug.LogWarning("[CollectionUI] 見つかりません: Resources/" + t.Folder + "/" + t.Id);
                if (nowPlayingText != null) nowPlayingText.text = t.Label + "（ファイルがありません）";
                return;
            }

            selected = index;
            HighlightSelected();

            preview.clip = clip;
            preview.loop = (t.Folder == "Bgm");   // スティンガーは一発物なので繰り返さない
            preview.volume = PreviewVolume();
            preview.time = 0f;
            preview.Play();

            if (nowPlayingText != null) nowPlayingText.text = t.Label;
            // **一時停止は `∥`(U+2225)。** `‖`(U+2016) は PixelMplus に無く □ になっていた（2026-09-19）
            if (playLabel != null) playLabel.text = "∥";
        }

        private float PreviewVolume()
        {
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio == null) return 1f;
            return Mathf.Clamp01(audio.bgmVolume * audio.masterVolume);
        }

        private void TogglePlay()
        {
            if (preview == null || preview.clip == null)
            {
                PlayIndex(selected >= 0 ? selected : 0);
                return;
            }
            if (preview.isPlaying) { preview.Pause(); if (playLabel != null) playLabel.text = "▶"; }
            else { preview.UnPause(); if (preview.isPlaying == false) preview.Play(); if (playLabel != null) playLabel.text = "∥"; }
        }

        private void StopPreview()
        {
            if (preview != null) { preview.Stop(); preview.time = 0f; }
            if (playLabel != null) playLabel.text = "▶";
        }

        private void HighlightSelected()
        {
            for (int i = 0; i < rowBgs.Count; i++)
                if (rowBgs[i] != null)
                    rowBgs[i].color = (i == selected) ? RowSelected : new Color(0f, 0f, 0f, 0f);
        }

        private void Update()
        {
            if (root == null || !root.activeSelf || preview == null) return;

            if (preview.clip != null)
            {
                float len = preview.clip.length;
                float pos = preview.time;
                if (timeText != null) timeText.text = Fmt(pos) + " / " + Fmt(len);
                if (seek != null && len > 0f)
                {
                    seekIsBeingSetByCode = true;
                    seek.value = Mathf.Clamp01(pos / len);
                    seekIsBeingSetByCode = false;
                }
                if (!preview.isPlaying && pos <= 0f && playLabel != null) playLabel.text = "▶";
            }

            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private static string Fmt(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int s = Mathf.FloorToInt(seconds);
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        // ---------------- 小物 ----------------

        private static GameObject NewEmpty(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 pos, Vector2 size,
            float fontSize, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Center(rect, size);
            rect.anchoredPosition = pos;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private void Button(Transform parent, string name, string label, Vector2 pos, Vector2 size,
            float fontSize, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Center(rect, size);
            rect.anchoredPosition = pos;

            var img = go.GetComponent<Image>();
            img.color = new Color32(52, 30, 42, 255);
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            Label(go.transform, "Text", label, Vector2.zero, size, fontSize, TextAlignmentOptions.Center, TextMain);
        }

        private Toggle BuildToggle(Transform parent, string name, string label, Vector2 pos, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            Center(rect, new Vector2(260f, 20f));
            rect.anchoredPosition = pos;

            var box = NewImage(go.transform, "Box", new Color32(52, 30, 42, 255));
            Center(box.rectTransform, new Vector2(14f, 14f));
            box.rectTransform.anchoredPosition = new Vector2(-122f, 0f);

            var check = NewImage(box.transform, "Check", Marker);
            Center(check.rectTransform, new Vector2(8f, 8f));

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = false;

            Label(go.transform, "Text", label, new Vector2(12f, 0f), new Vector2(232f, 18f),
                fontSize, TextAlignmentOptions.Left, TextDim);
            return toggle;
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

        /// <summary>
        /// シーンに既にある日本語フォントを借りる。RoomScreenUI と同じ手口。
        /// 専用のフォントアセットを持たせると、差し替えたときにここだけ取り残される。
        /// </summary>
        private static TMP_FontAsset BorrowJapaneseFont()
        {
            var labels = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var label in labels)
            {
                if (label != null && label.font != null) return label.font;
            }
            return null;
        }
    }
}
