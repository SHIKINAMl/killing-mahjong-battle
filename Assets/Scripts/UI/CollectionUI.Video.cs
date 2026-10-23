using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace KillingMahjong.UI
{
    /// <summary>
    /// コレクションの「動画」タブ（2026-10-23 ユーザー指示「デバッグルームのコレクションに動画を追加しておいて」）。
    ///
    /// 左に動画の一覧、右に 4:3 の画面と再生ボタン。いまある動画は紹介PV（タイトルの放置画面で流す物）だけ。
    /// 流し方はタイトルのランキング板（<see cref="TitleRankingBoardUI"/>）と同じで、
    /// WebGL でも読めるよう StreamingAssets から URL で流す。
    ///
    /// **動画を足すときは <see cref="Videos"/> に1行足す。** ファイルは `Assets/StreamingAssets/` の下に置く。
    /// 画面の絵（RenderTexture）は閉じるときに手放す。
    /// </summary>
    public sealed partial class CollectionUI
    {
        private readonly struct Clip
        {
            /// <summary>StreamingAssets からの場所。</summary>
            public readonly string Path;
            public readonly string Label;
            /// <summary>どこで使っているか。</summary>
            public readonly string Note;
            public Clip(string path, string label, string note) { Path = path; Label = label; Note = note; }
        }

        private static readonly Clip[] Videos =
        {
            new Clip("Video/title_pv.mp4", "紹介PV", "タイトルで放置すると、ランキングと交互に流れる"),
        };

        private const int VideoTab = 6;

        private GameObject videoPage;
        private VideoPlayer videoPlayer;
        private RenderTexture videoTexture;
        private RawImage videoScreen;
        private TextMeshProUGUI videoTitle, videoNote, videoTime, videoPlayLabel;
        private readonly System.Collections.Generic.List<Image> videoRows = new System.Collections.Generic.List<Image>();
        private int videoSelected = -1;

        private void BuildVideoPage(Transform parent)
        {
            videoRows.Clear();
            Label(parent, "Head_Videos", "動画", new Vector2(GroupColumnCenterX, 158f),
                new Vector2(GroupColumnWidth, 22f), 14f, TextAlignmentOptions.Left, Marker);

            var divider = NewImage(parent, "VideoDivider", PanelEdge);
            Center(divider.rectTransform, new Vector2(1f, 400f));
            divider.rectTransform.anchoredPosition = new Vector2(GroupColumnCenterX + GroupColumnWidth * 0.5f + 10f, -30f);
            divider.raycastTarget = false;

            for (int i = 0; i < Videos.Length; i++)
            {
                int index = i;
                var row = NewImage(parent, "Video_" + i, new Color(0f, 0f, 0f, 0f));
                Center(row.rectTransform, new Vector2(GroupColumnWidth, GroupRowPitch - 2f));
                row.rectTransform.anchoredPosition = new Vector2(GroupColumnCenterX, 132f - i * GroupRowPitch);
                row.raycastTarget = true;
                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = row;
                button.onClick.AddListener(() => SelectVideo(index));
                Label(row.transform, "Text", Videos[i].Label, new Vector2(4f, 0f),
                    new Vector2(GroupColumnWidth - 8f, GroupRowPitch - 2f), 15f, TextAlignmentOptions.Left, TextMain);
                videoRows.Add(row);
            }

            // 右の画面。黒い枠の中に 4:3 で収める
            const float screenX = 92f;
            var frame = NewImage(parent, "VideoFrame", Color.black);
            Center(frame.rectTransform, new Vector2(440f, 330f));
            frame.rectTransform.anchoredPosition = new Vector2(screenX, 0f);
            frame.raycastTarget = false;
            var screen = new GameObject("Screen", typeof(RectTransform), typeof(RawImage));
            screen.transform.SetParent(frame.transform, false);
            Stretch((RectTransform)screen.transform);
            videoScreen = screen.GetComponent<RawImage>();
            videoScreen.color = Color.black;
            videoScreen.raycastTarget = false;

            videoTitle = Label(parent, "VideoTitle", "", new Vector2(screenX, 180f), new Vector2(440f, 22f),
                16f, TextAlignmentOptions.Left, TextMain);
            videoNote = Label(parent, "VideoNote", "", new Vector2(screenX, -182f), new Vector2(440f, 18f),
                12f, TextAlignmentOptions.Left, TextDim);

            Button(parent, "VideoPlay", "▶", new Vector2(screenX - 170f, -216f), new Vector2(90f, 32f), 17f, ToggleVideo);
            videoPlayLabel = parent.Find("VideoPlay/Text").GetComponent<TextMeshProUGUI>();
            Button(parent, "VideoStop", "■", new Vector2(screenX - 70f, -216f), new Vector2(90f, 32f), 17f, StopVideo);
            videoTime = Label(parent, "VideoTime", "00:00 / 00:00", new Vector2(screenX + 130f, -216f),
                new Vector2(170f, 24f), 15f, TextAlignmentOptions.Right, TextDim);

            videoPlayer = parent.gameObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.source = VideoSource.Url;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            videoPlayer.errorReceived += (source, message) =>
            {
                Debug.LogWarning("[CollectionUI] 動画を流せませんでした: " + message);
                if (videoNote != null) videoNote.text = "動画を読み込めませんでした";
                if (videoPlayLabel != null) videoPlayLabel.text = "▶";
            };
        }

        private void SelectVideo(int index)
        {
            if (index < 0 || index >= Videos.Length) return;
            StopVideo();
            videoSelected = index;
            for (int i = 0; i < videoRows.Count; i++)
                if (videoRows[i] != null) videoRows[i].color = i == index ? RowSelected : new Color(0f, 0f, 0f, 0f);
            videoTitle.text = Videos[index].Label + "　" + System.IO.Path.GetFileName(Videos[index].Path);
            videoNote.text = Videos[index].Note;
            videoPlayer.url = System.IO.Path.Combine(Application.streamingAssetsPath, Videos[index].Path);
        }

        private void ToggleVideo()
        {
            if (videoPlayer == null) return;
            if (videoSelected < 0) SelectVideo(0);
            if (videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
                videoPlayLabel.text = "▶";
                return;
            }
            if (videoTexture == null)
            {
                videoTexture = new RenderTexture(960, 720, 0) { name = "CollectionVideo" };
                videoTexture.Create();
            }
            videoPlayer.targetTexture = videoTexture;
            videoScreen.texture = videoTexture;
            videoScreen.color = Color.white;
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            videoPlayer.SetDirectAudioVolume(0, audio != null ? audio.BgmOutputVolume : 1f);
            videoPlayer.Play();
            videoPlayLabel.text = "∥";
        }

        private void StopVideo()
        {
            if (videoPlayer != null) videoPlayer.Stop();
            if (videoPlayLabel != null) videoPlayLabel.text = "▶";
            if (videoScreen != null) { videoScreen.texture = null; videoScreen.color = Color.black; }
            if (videoTime != null) videoTime.text = "00:00 / 00:00";
        }

        /// <summary>閉じるときに呼ぶ。動画の絵を手放す</summary>
        private void ReleaseVideo()
        {
            StopVideo();
            if (videoPlayer != null) videoPlayer.targetTexture = null;
            if (videoTexture != null) { videoTexture.Release(); Destroy(videoTexture); videoTexture = null; }
        }

        private void UpdateVideoPage()
        {
            if (videoPage == null || !videoPage.activeSelf || videoPlayer == null || videoTime == null) return;
            if (!videoPlayer.isPrepared) return;
            videoTime.text = Fmt((float)videoPlayer.time) + " / " + Fmt((float)videoPlayer.length);
            // 最後まで流れたら止まった扱いにする
            if (!videoPlayer.isPlaying && videoPlayer.frame > 0 && (ulong)videoPlayer.frame + 1 >= videoPlayer.frameCount)
                videoPlayLabel.text = "▶";
        }
    }
}
