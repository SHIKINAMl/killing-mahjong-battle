using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    /// <summary>
    /// チュートリアルの通し録画（2026-09-12）。
    ///
    /// **外から呼べる形にしてあるのは、録画が Play をまたぐため。**
    /// `execute_code` は呼ぶたびに別のコードとして組まれるので、
    /// 録画機を握っておく場所がどこかに要る。ここの static がその置き場。
    ///
    /// **必ず Play に入ってから <see cref="Start"/> を呼ぶこと。**
    /// Play に入るときドメインが読み直されて static が消えるので、
    /// 先に始めると握っていたものを見失う。
    ///
    /// 出力は Game ビューをそのまま。既定は 800x600（このゲーム本来の画面比）。
    /// </summary>
    public static class TutorialRecorder
    {
        private static RecorderController _controller;

        public static bool IsRecording
        {
            get { return _controller != null && _controller.IsRecording(); }
        }

        /// <summary>録り始める。`outputPathNoExt` に拡張子は付けない（Recorder が付ける）。</summary>
        public static string Start(string outputPathNoExt, int width, int height, float fps)
        {
            if (IsRecording) return "already recording";

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            settings.SetRecordModeToManual();
            settings.FrameRate = fps;

            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = "TutorialRun";
            movie.Enabled = true;
            movie.OutputFile = outputPathNoExt;

            movie.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = width,
                OutputHeight = height,
            };

            // **音も録る（2026-09-13）。** 揺れを曲の拍に合わせたので、
            // 絵だけ録っても合っているかどうかが分からない。
            // ゲームの音を止めているあいだは無音のまま録れるだけで、害は無い。
            movie.AudioInputSettings.PreserveAudio = true;

            settings.AddRecorderSettings(movie);

            _controller = new RecorderController(settings);
            _controller.PrepareRecording();
            bool ok = _controller.StartRecording();

            return "started=" + ok + " -> " + outputPathNoExt + " (" + width + "x" + height + " @" + fps + ")";
        }

        /// <summary>録り終える。書き出しは Recorder が閉じるときに行う。</summary>
        public static string Stop()
        {
            if (_controller == null) return "not recording";

            bool was = _controller.IsRecording();
            _controller.StopRecording();
            _controller = null;
            return "stopped (wasRecording=" + was + ")";
        }
    }
}
