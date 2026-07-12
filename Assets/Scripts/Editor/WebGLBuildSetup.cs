using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.Build.Reporting;
using System.Linq;

namespace KillingMahjong.Editor
{
    public class WebGLBuildSetup
    {
        [MenuItem("KillingMahjong/Build/Build WebGL for GitHub Pages")]
        public static void BuildWebGLForGitHubPages()
        {
            // 1. GitHub Pages向けに圧縮を無効化（Unable to parse エラー対策）
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            // 念のためDecompression Fallbackも有効にしておく
            PlayerSettings.WebGL.decompressionFallback = true;
            // Color Spaceをチェック（URPはLinear必須だがWebGLでは警告が出る場合がある。今回はそのままLinearを維持）

            // 2. 出力先のフォルダ（docs）を設定
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            string buildPath = Path.Combine(projectPath, "docs");

            // フォルダが存在しない場合は作成
            if (!Directory.Exists(buildPath))
            {
                Directory.CreateDirectory(buildPath);
            }

            // 3. ビルドに含めるシーンを取得（Build Settingsでチェックが入っているもの）
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[WebGL Build] Build Settings に有効なシーンがありません！");
                return;
            }

            // 4. ビルドの実行
            Debug.Log("[WebGL Build] GitHub Pages向けのWebGLビルドを開始します...");
            
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.scenes = scenes;
            buildPlayerOptions.locationPathName = buildPath;
            buildPlayerOptions.target = BuildTarget.WebGL;
            buildPlayerOptions.options = BuildOptions.None;

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WebGL Build] ビルド成功！ 出力先: {buildPath} / サイズ: {summary.totalSize} bytes");
                // エクスプローラーでフォルダを開く
                EditorUtility.RevealInFinder(buildPath);
            }
            else if (summary.result == BuildResult.Failed)
            {
                Debug.LogError("[WebGL Build] ビルド失敗...");
            }
        }
    }
}
