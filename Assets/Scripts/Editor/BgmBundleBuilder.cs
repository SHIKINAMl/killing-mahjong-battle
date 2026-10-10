using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using KillingMahjong.Managers;

namespace KillingMahjong.EditorTools
{
    /// <summary>
    /// 対局BGMのうち、本体と別のファイルにする案（第4案・夜卓の灯火）のアセットバンドルを作る（2026-10-09）。
    ///
    /// なぜ分けるか・実行時にどう読むかは <see cref="BgmBank"/> の説明を見ること。
    ///
    ///   元   … `Assets/BgmBundles/<案>/*.wav`
    ///   出力 … `Assets/StreamingAssets/Bgm/bgm_p4`、`bgm_rt`
    ///           （ビルドすると `StreamingAssets/Bgm/` にそのまま写る）
    ///
    /// **出力は git に入れない**（.gitignore）。ビルドのたびに作り直す物なので。
    /// WebGL のビルド（`WebGLBuildSetup`）は、プレイヤーを作る前にここを呼ぶ。
    ///
    /// 案を足すときは、<see cref="BgmBank"/> の表と、下の <see cref="Build"/> の2か所に足す。
    /// </summary>
    public static class BgmBundleBuilder
    {
        [MenuItem("KillingMahjong/Build/BGM の別ファイルを作る（WebGL）")]
        private static void BuildForWebGLFromMenu()
        {
            Build(BuildTarget.WebGL);
        }

        /// <summary>バンドルを作る。作れたら true。</summary>
        public static bool Build(BuildTarget target)
        {
            var builds = new List<AssetBundleBuild>();
            if (!Add(builds, BgmBank.P4Bundle, BgmBank.P4SourceFolder)) return false;
            if (!Add(builds, BgmBank.RtBundle, BgmBank.RtSourceFolder)) return false;

            Directory.CreateDirectory(BgmBank.OutputFolder);

            // LZ4（塊ごとの圧縮）。曲は取り込みの時点でもう圧縮されているので、これ以上は縮まない。
            // まるごと圧縮（既定の LZMA）だと、WebGL では読むときに全部を展開し直すことになる
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                BgmBank.OutputFolder, builds.ToArray(), BuildAssetBundleOptions.ChunkBasedCompression, target);
            if (manifest == null)
            {
                Debug.LogError("[BgmBundleBuilder] BGM のバンドルが作れませんでした。");
                return false;
            }

            // 一緒に出てくる目録（.manifest と、フォルダ名のファイル）は実行時に使わない。ビルドに混ぜない
            string folderName = Path.GetFileName(BgmBank.OutputFolder);
            foreach (string path in Directory.GetFiles(BgmBank.OutputFolder))
            {
                string name = Path.GetFileName(path);
                bool wanted = name == BgmBank.P4Bundle || name == BgmBank.RtBundle;
                if (!wanted && (name.EndsWith(".manifest") || name.EndsWith(".manifest.meta")
                                || name == folderName || name == folderName + ".meta"))
                {
                    File.Delete(path);
                }
            }
            AssetDatabase.Refresh();

            var report = new System.Text.StringBuilder("[BgmBundleBuilder] BGM のバンドルを作りました（" + target + "）:");
            foreach (AssetBundleBuild build in builds)
            {
                var info = new FileInfo(Path.Combine(BgmBank.OutputFolder, build.assetBundleName));
                report.Append(' ').Append(build.assetBundleName).Append('=')
                      .Append((info.Exists ? info.Length / 1048576f : 0f).ToString("0.0")).Append("MB")
                      .Append('/').Append(build.assetNames.Length).Append("曲");
            }
            Debug.Log(report.ToString());
            return true;
        }

        private static bool Add(List<AssetBundleBuild> builds, string bundleName, string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogError("[BgmBundleBuilder] 元のフォルダがありません: " + folder);
                return false;
            }

            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }
            if (paths.Count == 0)
            {
                Debug.LogError("[BgmBundleBuilder] 曲がありません: " + folder);
                return false;
            }

            builds.Add(new AssetBundleBuild { assetBundleName = bundleName, assetNames = paths.ToArray() });
            return true;
        }
    }
}
