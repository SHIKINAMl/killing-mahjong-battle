using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    /// <summary>
    /// コレクション画面。音楽・効果音・CG・役・没案・演出を一覧から確認する。
    /// 没案と演出は同じ一覧表（<see cref="GameEffectCatalog"/>）から、没案の印で振り分けている。
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
    public sealed partial class CollectionUI : MonoBehaviour
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

        // ------------------------------------------------------------
        //  「音楽」タブの曲。**まとまり（階層）ごとに表を分けてある**（2026-10-09）。
        //
        //      場面の曲            タイトル・部屋・チュートリアル
        //      対局BGM
        //        新2曲 / 従来 / 第3案 / 第4案   設定の「対局BGM」で選ぶ4つの案
        //      未使用              作ったが、いまどこでも鳴っていない曲
        //
        //  以前は「使用中」「未使用」の2列だけで、対局の曲は従来のものしか並んでいなかった。
        //  第3案・第4案を足したとき、同じ列へ続けて並べると32曲が混ざって探せなくなるので、
        //  案ごとに分けた（ユーザーの指示:「無作為に追加するのではなく、階層をつくってまとめて」）。
        //
        //  **案を足すときは、表を1つ足して <see cref="BuildMusicGroups"/> に1行足す。** 既存の列へ混ぜないこと。
        //  行数の上限は無い。枠からはみ出た分はスクロールで見る。
        // ------------------------------------------------------------

        /// <summary>
        /// 対局の外で鳴る曲。
        ///   bgm_ex_summer / bgm_ex_lofi  AudioManager.PlayTitleBgm / PlayRoomBgm
        ///   tut_lesson                   TutorialAudioDirector の台本（契約書のあと）
        /// </summary>
        private static readonly Track[] BgmsScenes =
        {
            new Track("Bgm", "bgm_ex_summer",    "タイトル"),
            new Track("Bgm", "bgm_ex_lofi",      "部屋の待機"),
            new Track("Bgm", "tut_lesson",       "チュートリアル"),
        };

        // 対局BGM「新2曲（切替）」と「第3案」は、2026-10-09 に曲ごと消した
        // （WebGL のビルドが100MBを超えたため。ユーザーの判断）。ここにも並べない。

        /// <summary>
        /// 対局の場面と曲名の対応。**並びは対局の流れ順。**
        /// 従来・第4案・夜卓の灯火は同じ場面割り（AudioManager.PhaseBgmNames）なので、表を1つにして使い回す。
        /// </summary>
        private static readonly string[,] MatchScenes =
        {
            { "bgm_prepare", "配牌・手牌選択" },
            { "bgm_betting", "賭け" },
            { "bgm_tension", "先行・後攻" },
            { "bgm_field_1", "場 I　序盤" },
            { "bgm_field_2", "場 II" },
            { "bgm_field_3", "場 III" },
            { "bgm_field_4", "場 IV　終盤" },
            { "bgm_ron",     "ロン・決着" },
            { "bgm_draw",    "流局" },
            { "bgm_result",  "結果" },
            { "bgm_win",     "勝ち" },
            { "bgm_lose",    "負け" },
        };

        /// <summary>
        /// 第4案・夜卓の灯火に入っているが、**対局では自動で鳴らない曲**（場面の割当に出番が無い）。
        /// 聴けるように並べるだけ。
        /// </summary>
        private static readonly string[,] MatchExtras =
        {
            { "bgm_phase_normal", "通常（対局では鳴らない）" },
            { "bgm_phase_turn",   "高揚（対局では鳴らない）" },
            { "bgm_discard",      "打牌（対局では鳴らない）" },
            { "bgm_discard_hot",  "打牌　激（対局では鳴らない）" },
        };

        /// <summary>場面の表から、1つの案ぶんの曲の並びを作る。</summary>
        private static Track[] MatchTracks(string folder, string prefix, bool withExtras, params Track[] more)
        {
            var list = new List<Track>();
            for (int i = 0; i < MatchScenes.GetLength(0); i++)
                list.Add(new Track(folder, prefix + MatchScenes[i, 0], MatchScenes[i, 1]));
            if (withExtras)
            {
                for (int i = 0; i < MatchExtras.GetLength(0); i++)
                    list.Add(new Track(folder, prefix + MatchExtras[i, 0], MatchExtras[i, 1]));
            }
            list.AddRange(more);
            return list.ToArray();
        }

        /// <summary>
        /// 対局BGM「従来」。層あり・層なしで曲は同じ。違うのは打牌中の鳴らし方だけで、
        /// 層ありは場 I〜IV の代わりに下の4本の層（土台＋旋律＋打楽器＋きらめき）を重ねる。
        ///
        /// **「負け」の曲（bgm_lose）は 2026-10-09 に消した**ので、ここからも外す（ユーザーの判断）。
        /// 従来で負けたときは、結果の曲がそのまま続く。
        /// </summary>
        private static readonly Track[] BgmsLegacy = Without("bgm_lose", MatchTracks("Bgm", "", false,
            new Track("Bgm", "field_base",    "場の層　土台（層あり）"),
            new Track("Bgm", "field_melody",  "場の層　旋律（層あり）"),
            new Track("Bgm", "field_drums",   "場の層　打楽器（層あり）"),
            new Track("Bgm", "field_sparkle", "場の層　きらめき（層あり）")));

        /// <summary>曲の並びから、名前の合う1曲を除く。</summary>
        private static Track[] Without(string id, Track[] tracks)
        {
            var list = new List<Track>();
            foreach (Track t in tracks)
            {
                if (t.Id != id) list.Add(t);
            }
            return list.ToArray();
        }

        /// <summary>
        /// 対局BGM「第4案」。AudioManager.Proposal4。
        /// 流局は対局では「土台＋モチーフ」の2本で鳴る（足すと「流局」の完成版と同じ音）。
        /// </summary>
        private static readonly Track[] BgmsProposal4 = MatchTracks("Bgm/Proposal4", "p4_", true,
            new Track("Bgm/Proposal4", "p4_bgm_draw_base",  "流局　土台だけ"),
            new Track("Bgm/Proposal4", "p4_bgm_draw_motif", "流局　モチーフだけ"));

        /// <summary>
        /// 対局BGM「夜卓の灯火」。AudioManager.Proposal4 の仕組みで鳴る（頭の印 `rt_`）。
        /// 掛け金は「合図の余白 v2（メインなし）」。流局は第4案と同じく「土台＋モチーフ」の2本で鳴る。
        /// </summary>
        private static readonly Track[] BgmsReturningTheme = MatchTracks("Bgm/ReturningTheme", "rt_", true,
            new Track("Bgm/ReturningTheme", "rt_bgm_draw_base",  "流局　土台だけ"),
            new Track("Bgm/ReturningTheme", "rt_bgm_draw_motif", "流局　モチーフだけ"));

        /// <summary>
        /// 「音楽」タブの左に並べる、まとまりの1行。
        /// </summary>
        private sealed class MusicGroup
        {
            public string Label;      // 左の列に出す名前
            public string Path;       // 右の見出しに出す道筋（「対局BGM ＞ 第4案」）
            public int Indent;        // 字下げの段
            public Track[] Tracks;    // null は見出しだけの行（選べない）
            public string Note;       // 右の見出しの横に小さく出す補足。無ければ null
        }

        /// <summary>
        /// 左の列の並び。上から順。
        ///
        /// **静的な配列にせず、使うときに作る。** 静的な初期化は書いた順に走るので、
        /// 配列にすると、これより下に書いてある表（BgmsUnused）がまだ空のうちに読んでしまい、
        /// 「未使用」が中身の無い見出しになる。
        /// </summary>
        private static MusicGroup[] BuildMusicGroups()
        {
            return new[]
            {
            new MusicGroup { Label = "場面の曲", Path = "場面の曲", Tracks = BgmsScenes },
            new MusicGroup { Label = "対局BGM" },
            new MusicGroup { Label = "第4案", Path = "対局BGM ＞ 第4案", Indent = 1, Tracks = BgmsProposal4 },
            new MusicGroup { Label = "夜卓の灯火", Path = "対局BGM ＞ 夜卓の灯火", Indent = 1, Tracks = BgmsReturningTheme },
            new MusicGroup { Label = "従来", Path = "対局BGM ＞ 従来", Indent = 1, Tracks = BgmsLegacy },
            new MusicGroup { Label = "未使用", Path = "未使用", Tracks = BgmsUnused },
            };
        }

        /// <summary>
        /// **作ったが、いまどこでも鳴っていない曲**（2026-09-27）。
        /// 消さずに残してあるので、ここから試聴できる。
        ///
        ///   bgm_title      タイトルは追加曲の bgm_ex_midnight に差し替えた
        ///   （bgm_tutorial と tut_* の7曲は、2026-10-09 に消した。チュートリアルは tut_lesson 1曲を
        ///     最後まで流す形になっていて鳴っておらず、WebGL のビルドを100MB未満にするために外した。ユーザーの判断）
        ///   bgm_discard    打牌フェイズは場のBGMが受け持つようになり、出番が無くなった
        ///   bgm_discard_hot  同上
        ///   bgm_battle     どこからも参照されていない
        ///   bgm_kake_1/2   ユーザーが作った「賭けの合図」（2026-09-27 に追加）。
        ///                  元は Assets/Se/ にあるが、あちらは Resources の外なので
        ///                  試聴できない。**コピーを置いている**（元はそのまま）。
        ///                  2 の方はシーンの battleBgm 枠に入っているが、
        ///                  あの経路は UsePhaseBgm が false のときしか通らないので鳴らない。
        ///
        /// 使い始めるときは AudioManager の Tempos 表にも足すこと。拍が引けないと
        /// 音ハメ（FloatingAnimator など）が効かない。
        ///
        /// **bgm_ex_* は元「追加曲」タブの曲**（2026-09-27 にこちらへまとめた）。
        /// ユーザーが別に作ったオリジナル曲（C:\Users\akira\Music\D_N_A_original_bgm\）を、
        /// 44100Hz/モノラル/16bit・RMS −18.91dBFS（既存曲の中央値）に揃えて取り込んだもの。
        /// 2026-09-19 にリマスター版（*_gm_remaster_v3_final / *_v2_final の **MP3**）へ差し替えた。
        /// **同名の WAV は仕上げ前で −42dB と小さいので使わない。** 仕上げ済みは MP3。
        /// リマスター版は**無音で終わる**（末尾0.6〜1.2秒）ので、ループすると少し間が空く。
        /// </summary>
        private static readonly Track[] BgmsUnused =
        {
            new Track("Bgm", "bgm_title",        "タイトル（旧）"),
            new Track("Bgm", "bgm_discard",      "打牌"),
            new Track("Bgm", "bgm_discard_hot",  "打牌　激"),
            new Track("Bgm", "bgm_battle",       "対局（旧）"),
            new Track("Bgm", "bgm_kake_1",       "賭けの合図 I"),
            new Track("Bgm", "bgm_kake_2",       "賭けの合図 II"),
            new Track("Bgm", "bgm_ex_glitch",    "グリッチ"),
            new Track("Bgm", "bgm_ex_midnight",  "真夜中のアーケード"),
            new Track("Bgm", "bgm_ex_fantasy",   "はるかな地平線"),
            new Track("Bgm", "bgm_ex_sporty",    "カウントダウン"),
            new Track("Bgm", "bgm_ex_mystery",   "時計じかけの謎"),
            new Track("Bgm", "bgm_ex_steampunk", "蒸気の夜想曲"),
            new Track("Bgm", "bgm_ex_incident",  "ひび割れた事件"),
            new Track("Bgm", "bgm_ex_surreal",   "奇妙な回廊"),
            new Track("Bgm", "bgm_ex_think",     "長考"),
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
        private GameObject playerBar;       // 下の試聴バー。音のあるタブでだけ出す
        private GameObject sePage;          // 「効果音」タブ
        private GameObject cgPage;
        private GameObject yakuPage;
        private GameObject unusedPage;
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

        /// <summary>「没案」「演出」タブの番号。<see cref="BuildTabs"/> の並びと揃えること。</summary>
        private const int UnusedTab = 4;
        private const int EffectsTab = 5;

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
            if (EffectPreviewPlayer.Current != null) EffectPreviewPlayer.Current.Close();
            StopPreview();

            // **CGタブの絵はここで手放す。** 抱えたままだと、開くたびに
            // テクスチャが積もる（2048px のものが混ざっている）
            ReleaseCgThumbs();

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

            Label(panel.transform, "Heading", "コレクション", new Vector2(-200f, 244f), new Vector2(300f, 34f),
                24f, TextAlignmentOptions.Left, TextMain);
            Button(panel.transform, "Close", "もどる", new Vector2(300f, 244f), new Vector2(110f, 32f), 17f, Close);

            BuildTabs(panel.transform);

            musicPage = NewEmpty(panel.transform, "MusicPage");
            Stretch(musicPage.GetComponent<RectTransform>());
            BuildMusicPage(musicPage.transform);

            // 「効果音」タブ。左は演出の効果音、右はチュートリアルの効果音。
            // 行数の上限は無い。枠からはみ出た分はスクロールで見る（2026-09-27）
            sePage = NewEmpty(panel.transform, "SePage");
            Stretch(sePage.GetComponent<RectTransform>());
            BuildColumn(sePage.transform, -178f, "演出", Stingers);
            BuildColumn(sePage.transform, 178f, "チュートリアル", TutorialSes);

            cgPage = NewEmpty(panel.transform, "CgPage");
            Stretch(cgPage.GetComponent<RectTransform>());
            BuildCgPage(cgPage.transform);

            yakuPage = NewEmpty(panel.transform, "YakuPage");
            Stretch(yakuPage.GetComponent<RectTransform>());
            BuildYakuPage(yakuPage.transform);   // CollectionUI.Yaku.cs

            unusedPage = NewEmpty(panel.transform, "UnusedPage");
            Stretch(unusedPage.GetComponent<RectTransform>());
            BuildUnusedPage(unusedPage.transform);

            effectsPage = NewEmpty(panel.transform, "EffectsPage");
            Stretch(effectsPage.GetComponent<RectTransform>());
            BuildEffectsPage(effectsPage.transform);

            BuildPlayer(panel.transform);
            EnsurePreviewSource();
        }

        private readonly List<Image> tabMarks = new List<Image>();

        private void BuildTabs(Transform parent)
        {
            // **「追加曲」タブは畳んだ**（2026-09-27 の指示で、曲を全部「音楽」へまとめた）。
            //
            // **「没案」タブは一度畳んで、同じ日に戻した**（2026-10-07 の指示）。
            // 演出の一覧に没案が混ざっていたので、没案だけをこちらへ分けて並べる。
            // 並びを変えたら <see cref="UnusedTab"/>／<see cref="EffectsTab"/> も合わせること。
            string[] names = { "音楽", "効果音", "CG", "役", "没案", "演出" };
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
            if (sePage != null) sePage.SetActive(index == 1);
            if (cgPage != null) cgPage.SetActive(index == 2);
            if (yakuPage != null) yakuPage.SetActive(index == 3);
            if (unusedPage != null) unusedPage.SetActive(index == UnusedTab);
            if (effectsPage != null) effectsPage.SetActive(index == EffectsTab);

            // **試聴バーは音のあるタブだけ。** CG や役のタブに「曲を選んでください」が
            // 残っていると、何を選ぶ画面なのか分からなくなる。
            // 鳴らしたまま移ると音だけ残るので、ここで止める
            bool hasAudio = index == 0 || index == 1;
            if (playerBar != null) playerBar.SetActive(hasAudio);
            if (!hasAudio) StopPreview();

            for (int i = 0; i < tabMarks.Count; i++)
                if (tabMarks[i] != null) tabMarks[i].enabled = (i == index);
        }

        // 「音楽」タブの割り付け。左に細い列（まとまり）、右に広い列（曲）
        private const float GroupColumnCenterX = -264f;
        private const float GroupColumnWidth = 160f;
        private const float GroupRowPitch = 26f;
        private const float GroupIndent = 16f;
        private const float TrackColumnCenterX = 92f;
        private const float TrackColumnWidth = 500f;

        private readonly List<GameObject> musicGroupPages = new List<GameObject>();
        private readonly List<Image> musicGroupRows = new List<Image>();
        private int musicGroupSelected = 0;

        /// <summary>
        /// 「音楽」タブ。**左でまとまりを選び、右にその曲が並ぶ**（2026-10-09）。
        ///
        /// 効果音は「効果音」タブにある（2026-09-19 の指示）。
        /// 2026-09-27 からは「使用中」「未使用」の2列だったが、対局の曲が従来のものしか無く、
        /// 第3案・第4案を聴く場所が無かった。まとまりの中身は <see cref="BuildMusicGroups"/>。
        /// </summary>
        private void BuildMusicPage(Transform parent)
        {
            flat.Clear();
            rowBgs.Clear();
            musicGroupPages.Clear();
            musicGroupRows.Clear();

            Label(parent, "Head_Groups", "まとまり", new Vector2(GroupColumnCenterX, 158f),
                new Vector2(GroupColumnWidth, 22f), 14f, TextAlignmentOptions.Left, Marker);

            // 左の列と右の列を分ける細い線
            var divider = NewImage(parent, "GroupDivider", PanelEdge);
            Center(divider.rectTransform, new Vector2(1f, ViewportHeight + 24f));
            divider.rectTransform.anchoredPosition =
                new Vector2(GroupColumnCenterX + GroupColumnWidth * 0.5f + 10f, ViewportCenterY + 10f);
            divider.raycastTarget = false;

            MusicGroup[] groups = BuildMusicGroups();
            float top = ViewportCenterY + ViewportHeight * 0.5f - RowTopMargin * 0.5f;
            for (int i = 0; i < groups.Length; i++)
            {
                MusicGroup group = groups[i];
                int index = i;
                float indent = group.Indent * GroupIndent;
                float y = top - i * GroupRowPitch - GroupRowPitch * 0.5f;

                var row = NewImage(parent, "Group_" + i, new Color(0f, 0f, 0f, 0f));
                Center(row.rectTransform, new Vector2(GroupColumnWidth, GroupRowPitch - 2f));
                row.rectTransform.anchoredPosition = new Vector2(GroupColumnCenterX, y);
                musicGroupRows.Add(row);

                bool selectable = group.Tracks != null;
                row.raycastTarget = selectable;
                if (selectable)
                {
                    var btn = row.gameObject.AddComponent<Button>();
                    btn.targetGraphic = row;
                    btn.onClick.AddListener(() => ShowMusicGroup(index));
                }

                // 見出しだけの行（「対局BGM」）は、選べないことが分かるよう色を落とす
                Label(row.transform, "Name", group.Label,
                    new Vector2(indent * 0.5f + 4f, 0f), new Vector2(GroupColumnWidth - indent - 12f, 20f),
                    selectable ? 14f : 13f, TextAlignmentOptions.Left, selectable ? TextMain : TextDim);

                if (!selectable)
                {
                    musicGroupPages.Add(null);
                    continue;
                }

                // 右の列。まとまりごとに1枚ずつ作っておき、選ばれた1枚だけを出す
                var page = NewEmpty(parent, "GroupPage_" + i);
                Stretch(page.GetComponent<RectTransform>());
                BuildColumn(page.transform, TrackColumnCenterX, group.Path, group.Tracks, TrackColumnWidth);
                if (!string.IsNullOrEmpty(group.Note))
                {
                    Label(page.transform, "Note", group.Note, new Vector2(TrackColumnCenterX, 158f),
                        new Vector2(TrackColumnWidth, 22f), 11f, TextAlignmentOptions.Right, TextDim);
                }
                musicGroupPages.Add(page);
            }

            ShowMusicGroup(musicGroupSelected);
        }

        /// <summary>左で選んだまとまりの曲を、右に出す。鳴っている曲は止めない。</summary>
        private void ShowMusicGroup(int index)
        {
            if (index < 0 || index >= musicGroupPages.Count || musicGroupPages[index] == null) index = 0;
            musicGroupSelected = index;

            for (int i = 0; i < musicGroupPages.Count; i++)
            {
                if (musicGroupPages[i] != null) musicGroupPages[i].SetActive(i == index);
                if (i < musicGroupRows.Count && musicGroupRows[i] != null)
                    musicGroupRows[i].color = (i == index) ? RowSelected : new Color(0f, 0f, 0f, 0f);
            }
        }

        /// <summary>行の間隔。</summary>
        private const float RowPitch = 21f;

        /// <summary>枠の上端と1行目のあいだの余白。</summary>
        private const float RowTopMargin = 8f;

        /// <summary>1列の幅。</summary>
        private const float ColumnWidth = 330f;

        /// <summary>
        /// 行を見せる枠の高さと中心。**ここからはみ出た分はスクロールで見る**（2026-09-27）。
        ///
        /// 以前は行を並べるだけでスクロールが無く、曲が増えるたびに
        /// 「1列16行まで」「行間を詰める」と場当たりに逃げていた。下は再生バーで止まるので、
        /// 見出し（y=158）の下から再生バーの上（y=-175 あたり）までを枠にする。
        /// </summary>
        private const float ViewportHeight = 316f;
        private const float ViewportCenterY = -17f;

        private void BuildColumn(Transform parent, float centerX, string heading, Track[] tracks)
        {
            BuildColumn(parent, centerX, heading, tracks, ColumnWidth);
        }

        /// <param name="width">列の幅。「効果音」タブは 330 の2列、「音楽」タブの曲の列は広い1列</param>
        private void BuildColumn(Transform parent, float centerX, string heading, Track[] tracks, float width)
        {
            float ColumnWidth = width;   // 下の式はこの名前で幅を見ている
            // ファイル名の欄。広い列では、長い名前（p4_bgm_phase_normal など）が入るよう広げる
            float fileWidth = width > 400f ? 170f : 110f;

            Label(parent, "Head_" + heading, heading, new Vector2(centerX, 158f), new Vector2(ColumnWidth, 22f),
                14f, TextAlignmentOptions.Left, Marker);

            // 枠。**透明でも raycastTarget は要る。** 切っているとホイールを拾えず、
            // カーソルを乗せてもスクロールしない
            var viewport = NewImage(parent, "Viewport_" + heading, new Color(0f, 0f, 0f, 0f));
            Center(viewport.rectTransform, new Vector2(ColumnWidth, ViewportHeight));
            viewport.rectTransform.anchoredPosition = new Vector2(centerX, ViewportCenterY);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            var contentGo = NewEmpty(viewport.transform, "Content");
            var content = contentGo.GetComponent<RectTransform>();
            // 上端を基準にして下へ伸ばす。行数が変わっても上の見え方が動かない
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(ColumnWidth, tracks.Length * RowPitch + RowTopMargin);
            content.anchoredPosition = Vector2.zero;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            // 端で止める。跳ね返りは、行を選ぶ画面では落ち着かない
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 18f;
            scroll.inertia = false;

            // はみ出す列だけ、右端に細いつまみを出す。**無いとスクロールできると気づけない。**
            if (tracks.Length * RowPitch + RowTopMargin > ViewportHeight)
            {
                scroll.verticalScrollbar = BuildScrollbar(parent, heading, centerX, ColumnWidth);
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            }

            for (int i = 0; i < tracks.Length; i++)
            {
                int flatIndex = flat.Count;
                flat.Add(tracks[i]);

                float y = -RowTopMargin * 0.5f - i * RowPitch;

                var rowBg = NewImage(content, "Row_" + tracks[i].Id, new Color(0f, 0f, 0f, 0f));
                // 行も上端基準。枠の中で上から順に積む
                rowBg.rectTransform.anchorMin = new Vector2(0.5f, 1f);
                rowBg.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                rowBg.rectTransform.pivot = new Vector2(0.5f, 1f);
                rowBg.rectTransform.sizeDelta = new Vector2(ColumnWidth, 20f);
                rowBg.rectTransform.anchoredPosition = new Vector2(0f, y);
                rowBg.raycastTarget = true;
                rowBgs.Add(rowBg);

                var btn = rowBg.gameObject.AddComponent<Button>();
                btn.targetGraphic = rowBg;
                btn.onClick.AddListener(() => PlayIndex(flatIndex));

                // 位置は列の幅から決める（幅 330 のとき、番号 -146・名前 -40・ファイル名 108 で、以前と同じ）
                float nameWidth = ColumnWidth - 40f - fileWidth;
                Label(rowBg.transform, "No", (i + 1).ToString("00"),
                    new Vector2(-ColumnWidth * 0.5f + 19f, 0f), new Vector2(28f, 18f),
                    11f, TextAlignmentOptions.Left, TextDim);
                Label(rowBg.transform, "Name", tracks[i].Label,
                    new Vector2(-ColumnWidth * 0.5f + 35f + nameWidth * 0.5f, 0f), new Vector2(nameWidth, 18f),
                    13f, TextAlignmentOptions.Left, TextMain);
                Label(rowBg.transform, "File", tracks[i].Id,
                    new Vector2(ColumnWidth * 0.5f - 2f - fileWidth * 0.5f, 0f), new Vector2(fileWidth, 18f),
                    9.5f, TextAlignmentOptions.Right, TextDim);
            }

            // **必ず上端から見せる。** 入れ直さないと、列によって途中から始まってしまう
            // （実機で右の列だけ1行目が隠れた）。ScrollRect が最初の配置で
            // 位置を決めるので、行を並べ終えたあとに当て直す。
            content.anchoredPosition = Vector2.zero;
            scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>列の右端に置く細いつまみ。</summary>
        private Scrollbar BuildScrollbar(Transform parent, string heading, float centerX, float width)
        {
            var track = NewImage(parent, "Bar_" + heading, new Color32(40, 24, 34, 255));
            Center(track.rectTransform, new Vector2(4f, ViewportHeight));
            track.rectTransform.anchoredPosition = new Vector2(centerX + width * 0.5f + 4f, ViewportCenterY);

            var area = NewEmpty(track.transform, "SlidingArea");
            var areaRect = area.GetComponent<RectTransform>();
            Stretch(areaRect);

            var handle = NewImage(area.transform, "Handle", Marker);
            var hr = handle.rectTransform;
            hr.anchorMin = new Vector2(0f, 0f);
            hr.anchorMax = new Vector2(1f, 1f);
            hr.sizeDelta = Vector2.zero;

            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = hr;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            return bar;
        }

        private Toggle muffleToggle;

        private void BuildPlayer(Transform parent)
        {
            var bar = NewImage(parent, "PlayerBar", new Color32(18, 11, 18, 255));
            Center(bar.rectTransform, new Vector2(700f, 86f));
            bar.rectTransform.anchoredPosition = new Vector2(0f, -218f);
            bar.raycastTarget = false;
            playerBar = bar.gameObject;

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

            // **第4案・夜卓の灯火の曲は、本体と別のファイルにある**（KillingMahjong.Managers.BgmBank）。
            // まだ届いていなければ取りに行かせて、届いたら鳴らす
            AudioClip clip;
            string bankPrefix = KillingMahjong.Managers.BgmBank.PrefixOf(t.Id);
            if (bankPrefix != null)
            {
                if (!KillingMahjong.Managers.BgmBank.IsReady(bankPrefix))
                {
                    KillingMahjong.Managers.BgmBank.Request(bankPrefix);
                    selected = index;
                    HighlightSelected();
                    if (preview.isPlaying) preview.Stop();
                    if (nowPlayingText != null) nowPlayingText.text = t.Label + "（読み込み中）";
                    pendingBankIndex = index;
                    StartCoroutine(PlayWhenBankReady(index, bankPrefix));
                    return;
                }
                clip = KillingMahjong.Managers.BgmBank.Load(t.Id);
            }
            else
            {
                clip = Resources.Load<AudioClip>(t.Folder + "/" + t.Id);
            }
            pendingBankIndex = -1;

            if (clip == null)
            {
                Debug.LogWarning("[CollectionUI] 見つかりません: " + t.Folder + "/" + t.Id);
                if (nowPlayingText != null) nowPlayingText.text = t.Label + "（ファイルがありません）";
                return;
            }

            selected = index;
            HighlightSelected();

            preview.clip = clip;
            // スティンガーは一発物なので繰り返さない。案ごとのフォルダ（Bgm/Proposal4 など）も曲として扱う
            preview.loop = t.Folder.StartsWith("Bgm");
            preview.volume = PreviewVolume();
            preview.time = 0f;
            preview.Play();

            if (nowPlayingText != null) nowPlayingText.text = t.Label;
            // **一時停止は `∥`(U+2225)。** `‖`(U+2016) は PixelMplus に無く □ になっていた（2026-09-19）
            if (playLabel != null) playLabel.text = "∥";
        }

        /// <summary>届くのを待っている曲の番号。別の曲を選び直されたら、古い待ちは鳴らさない。</summary>
        private int pendingBankIndex = -1;

        private System.Collections.IEnumerator PlayWhenBankReady(int index, string bankPrefix)
        {
            while (!KillingMahjong.Managers.BgmBank.IsReady(bankPrefix)
                   && !KillingMahjong.Managers.BgmBank.HasFailed(bankPrefix))
            {
                if (pendingBankIndex != index) yield break;
                yield return null;
            }
            if (pendingBankIndex != index) yield break;
            pendingBankIndex = -1;

            if (KillingMahjong.Managers.BgmBank.IsReady(bankPrefix))
            {
                PlayIndex(index);
            }
            else if (nowPlayingText != null && index >= 0 && index < flat.Count)
            {
                nowPlayingText.text = flat[index].Label + "（読み込めませんでした）";
            }
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
            if (preview != null) { preview.Stop(); if (preview.clip != null) preview.time = 0f; }
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
            if (EffectPreviewPlayer.IsOpen) return;
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

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Close();
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
