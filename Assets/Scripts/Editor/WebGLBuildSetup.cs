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
            Build(development: false);
        }

        /// <summary>
        /// 原因調査用のビルド。**出力先が違うので、公開中の docs は壊さない。**
        ///
        /// ブラウザのエラーが `wasm-function[137755]` のような番号でしか出ないとき、
        /// こちらで出すと C# のメソッド名がそのままスタックに出る。
        /// **重い・大きいので、配布には使わないこと。**
        /// </summary>
        [MenuItem("KillingMahjong/Build/Build WebGL (Development / 原因調査用)")]
        public static void BuildWebGLDevelopment()
        {
            Build(development: true);
        }

        private static void Build(bool development)
        {
            // 0. 対局BGMのうち、本体と別のファイルにしてある案（第4案・夜卓の灯火）を先に作る。
            //    `Assets/StreamingAssets/Bgm/` に出て、ビルドの `StreamingAssets/Bgm/` へそのまま写る。
            //    これを入れないと、その案を選んだときに曲が鳴らない（KillingMahjong.Managers.BgmBank）
            if (!KillingMahjong.EditorTools.BgmBundleBuilder.Build(BuildTarget.WebGL))
            {
                Debug.LogError("[WebGL Build] BGM の別ファイルが作れなかったので、ビルドを止めます。");
                return;
            }

            // 1. 転送量を減らすため Brotli で圧縮する（2026-09-27）。
            //
            // **`decompressionFallback` があるので GitHub Pages でも動く。**
            // 以前ここを Disabled にしていたのは「Unable to parse」対策だが、
            // あれは GitHub Pages が `Content-Encoding: br` を返さないために起きる。
            // 折り返しの復号（次の行）を入れてあれば、サーバが何も返さなくても
            // 読み込み側で解いてくれるので、圧縮したまま置ける。
            //
            // 調査用ビルドは読み込みの速さより分かりやすさを優先して、圧縮しない。
            PlayerSettings.WebGL.compressionFormat = development
                ? WebGLCompressionFormat.Disabled
                : WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            // WebGLの基本解像度を800x600 (4:3)に固定
            PlayerSettings.defaultScreenWidth = 800;
            PlayerSettings.defaultScreenHeight = 600;
            // **自前のテンプレートを使う（2026-09-30）。**
            // 内蔵の Minimal は canvas を 800x600 の直書きで出すため、どんな大きさの
            // 画面でも小さいままだった（プランナーから「比率はいいがサイズが小さい」）。
            // `Assets/WebGLTemplates/KillingMahjong/index.html` で、4:3 を保ったまま
            // ブラウザいっぱいに広げている。
            PlayerSettings.WebGL.template = "PROJECT:KillingMahjong";

            // RuntimeError: null function などのWASMクラッシュ対策
            // **`stripEngineCode` は false のまま触らないこと。** 過去にこれを有効にして
            // WASMが落ちた経緯があり、ビルドが軽くなる代わりに動かなくなる。
            PlayerSettings.WebGL.exceptionSupport = development
                ? WebGLExceptionSupport.FullWithStacktrace
                : WebGLExceptionSupport.FullWithoutStacktrace;
            PlayerSettings.stripEngineCode = false;

            // 調査用は関数名を wasm に埋め込む。これが無いと
            // ブラウザのスタックが `wasm-function[137755]` のような番号のままになる。
            PlayerSettings.WebGL.debugSymbolMode = development
                ? WebGLDebugSymbolMode.Embedded
                : WebGLDebugSymbolMode.Off;

            // 2. 出力先のフォルダを設定。**調査用は docs を上書きしない。**
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            string buildPath = Path.Combine(projectPath, development ? "build-dev" : "docs");

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
            Debug.Log(development
                ? "[WebGL Build] 原因調査用（Development）のWebGLビルドを開始します..."
                : "[WebGL Build] GitHub Pages向けのWebGLビルドを開始します...");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.scenes = scenes;
            buildPlayerOptions.locationPathName = buildPath;
            buildPlayerOptions.target = BuildTarget.WebGL;
            buildPlayerOptions.options = development
                ? BuildOptions.Development
                : BuildOptions.None;

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string kind = development ? "Development" : "GitHub Pages";
                Debug.Log($"[WebGL Build] ビルド成功（{kind}）！ 出力先: {buildPath} / サイズ: {summary.totalSize} bytes");
                
                // ビルド後に自動で index.html をレスポンシブ（全画面対応）に書き換える
                string indexPath = Path.Combine(buildPath, "index.html");
                if (File.Exists(indexPath))
                {
                    string html = File.ReadAllText(indexPath);
                    html = html.Replace("canvas.style.width = \"960px\";", "canvas.style.width = \"100%\";");
                    html = html.Replace("canvas.style.height = \"600px\";", "canvas.style.height = \"100%\";");
                    html = html.Replace("width: 960px; height: 600px;", "width: 100%; height: 100%; aspect-ratio: 4/3; max-width: 800px; max-height: 600px; margin: auto;");

                    // **ビルドのファイルの URL に、版の印（ビルドした時刻）を付ける（2026-10-10）。**
                    // ファイル名が毎回同じ（docs.wasm.unityweb など）で、GitHub Pages は「10分間は手元の写しを
                    // 使ってよい」（max-age=600）と配る。上げ直した直後に開くと、ブラウザは古いプログラムを
                    // 使い続ける（データだけ新しい、という混ざり方もする）。直したはずの不具合が
                    // 上げ直しても直らず、原因を取り違えた。URL が変われば、必ず新しいファイルを取りに行く。
                    string stamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");
                    html = System.Text.RegularExpressions.Regex.Replace(
                        html, "\"(Build/[^\"?]+)\"", "\"$1?v=" + stamp + "\"");

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
