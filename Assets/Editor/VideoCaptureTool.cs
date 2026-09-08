using System;
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    /// <summary>
    /// Starts and stops Game View MP4 recordings for presentation capture.
    /// This class is editor-only because it is located under Assets/Editor.
    /// </summary>
    public static class VideoCaptureTool
    {
        private const string Mp4Extension = ".mp4";

        private static RecorderController s_RecorderController;
        private static RecorderControllerSettings s_ControllerSettings;
        private static MovieRecorderSettings s_MovieRecorderSettings;
        private static string s_OutputPath = string.Empty;

        /// <summary>このゲーム本来の画面比（4:3）。</summary>
        private const int GameWidth = 800;

        /// <summary>このゲーム本来の画面比（4:3）。</summary>
        private const int GameHeight = 600;

        /// <summary>
        /// Starts an MP4 recording of the Game View. A duration of zero records until StopRecording is called.
        ///
        /// **既定は 800x600。このゲーム本来の画面比（4:3）に合わせてある。**
        /// 16:9（1920x1080 など）で撮ると画面比が変わり、UI の配置が崩れる。
        /// 実際、2026-09-06 に 1920x1080 で撮った映像では、自分の体力表示が
        /// 画面の右端で見切れていた。**比を変えないこと。**
        ///
        /// 大きく撮りたいときは 4:3 の倍数を使う（1600x1200 / 2400x1800 など）。
        /// </summary>
        /// <param name="outputMp4Path">Absolute destination path, including the .mp4 extension.</param>
        /// <param name="width">Output width in pixels. Must be positive and even.</param>
        /// <param name="height">Output height in pixels. Must be positive and even.</param>
        /// <param name="frameRate">Constant output frame rate.</param>
        /// <param name="durationSeconds">Optional duration in seconds. Zero means manual stop.</param>
        /// <returns>A STARTED or ERROR message that includes the output path or failure reason.</returns>
        public static string StartRecording(
            string outputMp4Path,
            int width = GameWidth,
            int height = GameHeight,
            int frameRate = 60,
            float durationSeconds = 0f)
        {
            if (IsRecording())
            {
                return $"ERROR: A recording is already active: {s_OutputPath}";
            }

            if (!EditorApplication.isPlaying)
            {
                return "ERROR: VideoCaptureTool.StartRecording requires Unity Play mode. Enter Play mode before calling it.";
            }

            try
            {
                ReleaseStoppedState();

                var fullOutputPath = ValidateAndNormalizeOutputPath(outputMp4Path, width, height, frameRate, durationSeconds);
                var recorderOutputPath = fullOutputPath.Substring(0, fullOutputPath.Length - Mp4Extension.Length);

                var outputDirectory = Path.GetDirectoryName(fullOutputPath);
                Directory.CreateDirectory(outputDirectory);

                s_ControllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
                s_ControllerSettings.name = "Video Capture Controller Settings";
                s_ControllerSettings.FrameRate = frameRate;
                s_ControllerSettings.FrameRatePlayback = FrameRatePlayback.Constant;
                s_ControllerSettings.CapFrameRate = true;
                s_ControllerSettings.ExitPlayMode = false;

                if (durationSeconds > 0f)
                {
                    s_ControllerSettings.SetRecordModeToTimeInterval(0f, durationSeconds);
                }
                else
                {
                    s_ControllerSettings.SetRecordModeToManual();
                }

                s_MovieRecorderSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                s_MovieRecorderSettings.name = "Video Capture MP4 Recorder";
                s_MovieRecorderSettings.Enabled = true;
                s_MovieRecorderSettings.EncoderSettings = new CoreEncoderSettings
                {
                    Codec = CoreEncoderSettings.OutputCodec.MP4,
                    EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
                };
                s_MovieRecorderSettings.ImageInputSettings = new GameViewInputSettings
                {
                    OutputWidth = width,
                    OutputHeight = height
                };

                // RecorderSettings.OutputFile appends the extension automatically.
                s_MovieRecorderSettings.OutputFile = recorderOutputPath;
                s_ControllerSettings.AddRecorderSettings(s_MovieRecorderSettings);

                s_RecorderController = new RecorderController(s_ControllerSettings);
                s_RecorderController.PrepareRecording();

                if (!s_RecorderController.StartRecording())
                {
                    ReleaseStoppedState();
                    return $"ERROR: Unity Recorder did not begin recording: {fullOutputPath}";
                }

                s_OutputPath = fullOutputPath;
                var durationDescription = durationSeconds > 0f
                    ? $"; auto-stop after {durationSeconds:0.###} second(s)"
                    : "; manual stop required";
                return $"STARTED: {s_OutputPath}{durationDescription}";
            }
            catch (Exception exception)
            {
                if (!IsRecording())
                {
                    ReleaseStoppedState();
                }

                Debug.LogException(exception);
                return $"ERROR: Failed to start Game View recording ({exception.GetType().Name}): {exception.Message}";
            }
        }

        /// <summary>
        /// Stops the current recording and finalizes its file.
        /// </summary>
        /// <returns>A STOPPED, NOT_RECORDING, or ERROR message.</returns>
        public static string StopRecording()
        {
            if (s_RecorderController == null || !s_RecorderController.IsRecording())
            {
                ReleaseStoppedState();
                return string.IsNullOrEmpty(s_OutputPath)
                    ? "NOT_RECORDING: No recording has been started."
                    : $"NOT_RECORDING: Last output path was {s_OutputPath}";
            }

            try
            {
                s_RecorderController.StopRecording();
                var completedOutputPath = s_OutputPath;
                ReleaseStoppedState();
                return $"STOPPED: {completedOutputPath}";
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return $"ERROR: Failed to stop Game View recording ({exception.GetType().Name}): {exception.Message}";
            }
        }

        /// <summary>
        /// Returns whether a recording is currently active.
        /// </summary>
        public static bool IsRecording()
        {
            return s_RecorderController != null && s_RecorderController.IsRecording();
        }

        /// <summary>
        /// Returns the active recording path, or the most recently configured path after it stops.
        /// </summary>
        public static string GetOutputPath()
        {
            return s_OutputPath;
        }

        private static string ValidateAndNormalizeOutputPath(
            string outputMp4Path,
            int width,
            int height,
            int frameRate,
            float durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(outputMp4Path))
            {
                throw new ArgumentException("Output path must be a non-empty absolute .mp4 path.", nameof(outputMp4Path));
            }

            if (!Path.IsPathRooted(outputMp4Path))
            {
                throw new ArgumentException("Output path must be absolute.", nameof(outputMp4Path));
            }

            var fullOutputPath = Path.GetFullPath(outputMp4Path);
            if (!string.Equals(Path.GetExtension(fullOutputPath), Mp4Extension, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Output path must use the .mp4 extension.", nameof(outputMp4Path));
            }

            if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(fullOutputPath)))
            {
                throw new ArgumentException("Output path must include a file name before .mp4.", nameof(outputMp4Path));
            }

            // **同じ名前があっても撮り直せるようにする（2026-09-09）。**
            //
            // 以前はここで例外にして上書きを拒んでいたが、撮り直しのたびに落ちていた。
            // 指示は毎回きまった名前を指定してくるので、1度失敗すると同じ名前では
            // 二度と撮れなくなる。**撮り直しを止めるほうが害が大きい。**
            //
            // 消すのではなく、前の分は名前を変えて残す。上書きで消えて困るのは
            // 「撮れていたのに撮り直して失敗した」場合なので、戻せる形にしておく。
            if (File.Exists(fullOutputPath))
            {
                string backup = Path.Combine(
                    Path.GetDirectoryName(fullOutputPath),
                    Path.GetFileNameWithoutExtension(fullOutputPath)
                        + "_prev_" + DateTime.Now.ToString("HHmmss")
                        + Path.GetExtension(fullOutputPath));
                try
                {
                    File.Move(fullOutputPath, backup);
                    Debug.Log("[VideoCaptureTool] 同じ名前があったので、前の分を残しました: " + backup);
                }
                catch (IOException moveError)
                {
                    // 名前を変えられない（他で開かれている等）ときだけ、はっきり止める。
                    throw new IOException(
                        "既にある出力ファイルを避けられませんでした: " + fullOutputPath, moveError);
                }
            }

            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Output width and height must both be positive.");
            }

            if ((width & 1) != 0 || (height & 1) != 0)
            {
                throw new ArgumentException("MP4 output width and height must both be even numbers.");
            }

            // 4:3 から外れたら知らせる。止めはしないが、たいていは指定の間違い。
            // 16:9 で撮ると UI の配置が崩れる（2026-09-06 に体力表示が見切れた）。
            if (width * GameHeight != height * GameWidth)
            {
                Debug.LogWarning(
                    $"[VideoCaptureTool] 画面比が {width}x{height} になっています。" +
                    $"このゲーム本来の比は {GameWidth}x{GameHeight}（4:3）です。" +
                    "比を変えると UI の配置が崩れます。大きく撮るなら 1600x1200 など 4:3 の倍数を使ってください。");
            }

            if (frameRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameRate), "Frame rate must be positive.");
            }

            if (float.IsNaN(durationSeconds) || float.IsInfinity(durationSeconds) || durationSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration must be zero or a positive number of seconds.");
            }

            return fullOutputPath;
        }

        private static void ReleaseStoppedState()
        {
            if (s_MovieRecorderSettings != null)
            {
                UnityEngine.Object.DestroyImmediate(s_MovieRecorderSettings);
                s_MovieRecorderSettings = null;
            }

            if (s_ControllerSettings != null)
            {
                UnityEngine.Object.DestroyImmediate(s_ControllerSettings);
                s_ControllerSettings = null;
            }

            s_RecorderController = null;
        }
    }
}
