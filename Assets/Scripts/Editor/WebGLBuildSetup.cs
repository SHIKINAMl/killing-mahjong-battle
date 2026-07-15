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
            PlayerSettings.WebGL.decompressionFallback = true;
            // WebGLの基本解像度を800x600 (4:3)に固定
            PlayerSettings.defaultScreenWidth = 800;
            PlayerSettings.defaultScreenHeight = 600;
            // 余計なフッター(960x600固定)を消し、画面全体にフィットするMinimalテンプレートを使用する
            PlayerSettings.WebGL.template = "APPLICATION:Minimal";
            
            // RuntimeError: null function などのWASMクラッシュ対策
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithoutStacktrace;
            PlayerSettings.stripEngineCode = false;

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
                
                // ビルド後に自動で index.html をレスポンシブ（全画面対応）に書き換える
                string indexPath = Path.Combine(buildPath, "index.html");
                if (File.Exists(indexPath))
                {
                    string html = File.ReadAllText(indexPath);
                    html = html.Replace("canvas.style.width = \"960px\";", "canvas.style.width = \"100%\";");
                    html = html.Replace("canvas.style.height = \"600px\";", "canvas.style.height = \"100%\";");
                    html = html.Replace("width: 960px; height: 600px;", "width: 100%; height: 100%; aspect-ratio: 4/3; max-width: 800px; max-height: 600px; margin: auto;");
                    File.WriteAllText(indexPath, html);
                    Debug.Log("[WebGL Build] index.html をレスポンシブ対応に書き換えました。");
                }

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
