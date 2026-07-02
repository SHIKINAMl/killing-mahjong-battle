using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

namespace KillingMahjong.Editor
{
    public class GitHubPagesBuilder
    {
        [MenuItem("Tools/Build/GitHub Pages向けにビルド (WebGL)")]
        public static void BuildForGitHubPages()
        {
            // プロジェクト直下の docs フォルダに出力する
            string buildFolder = Path.Combine(Application.dataPath, "../docs");

            // 既に存在する場合は一度削除してクリーンビルドする
            if (Directory.Exists(buildFolder))
            {
                Directory.Delete(buildFolder, true);
            }
            Directory.CreateDirectory(buildFolder);

            // Build Settings で有効になっているシーンを全て取得
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("ビルド対象のシーンがありません。File > Build Settings... でシーンを追加してください。");
                return;
            }

            Debug.Log($"WebGLビルドを開始します... 出力先: {buildFolder}");

            // GitHub PagesでBrotli圧縮ファイルを正常に読み込めるようにFallbackを有効化する
            PlayerSettings.WebGL.decompressionFallback = true;

            // ビルドオプションの設定
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildFolder,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            // ターゲットをWebGLに切り替える（念のため）
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            // ビルド実行
            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            var summary = report.summary;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[GitHubPagesBuilder] ビルド成功！ サイズ: {summary.totalSize} bytes, 経過時間: {summary.totalTime}");
                Debug.Log("ビルドが完了しました。リポジトリをコミットしてプッシュし、GitHubの設定画面で 'docs' フォルダを公開設定してください。");
            }
            else if (summary.result == UnityEditor.Build.Reporting.BuildResult.Failed)
            {
                Debug.LogError("[GitHubPagesBuilder] ビルドに失敗しました。Consoleのエラーログを確認してください。");
            }
        }
    }
}
