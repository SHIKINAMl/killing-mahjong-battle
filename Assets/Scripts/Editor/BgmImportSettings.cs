using UnityEditor;
using UnityEngine;

namespace KillingMahjong.EditorTools
{
    /// <summary>
    /// `Resources/Bgm` に入れた曲を、**圧縮したまま持つ**設定で取り込む（2026-09-19）。
    ///
    /// Unity の既定は「読み込み時に全部展開（Decompress On Load）」で、
    /// 1分の曲を読むたびに**メインスレッドが30〜50ms止まっていた**（対局開始は4層同時で約170ms）。
    /// チュートリアルは場面ごとに曲が替わるので、そのたびに引っかかりが出ていた。
    /// 圧縮したまま（Compressed In Memory）にすると読み込みはほぼ0msになり、
    /// 1曲あたりのメモリも約10MB から約1MB に減る。
    ///
    /// **Streaming にはしない。** 場のBGMは4層を PlayScheduled で揃えて鳴らしており、
    /// 読み込みが遅れると層がずれるおそれがある。
    /// </summary>
    public class BgmImportSettings : AssetPostprocessor
    {
        private const string BgmFolder = "Assets/Resources/Bgm/";

        /// <summary>本体と別のファイルにする案（第4案・夜卓の灯火）の元の WAV。同じ設定で取り込む。</summary>
        private const string BundleSourceFolder = "Assets/BgmBundles/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(BgmFolder) && !assetPath.StartsWith(BundleSourceFolder)) return;

            var importer = (AudioImporter)assetImporter;
            var s = importer.defaultSampleSettings;
            if (s.loadType == AudioClipLoadType.CompressedInMemory) return;
            s.loadType = AudioClipLoadType.CompressedInMemory;

            // **圧縮の音質は 50%（2026-10-09）。** 既定の 100% のままだと WebGL のビルドが100MBを超えた。
            // WebGL では 50% で1曲あたり約33%小さくなり、それより下げても変わらない（測った）。
            // 50% でも約250kbpsある。ここを通るのは初めて取り込む曲だけ
            // （もう取り込んである曲の設定は .meta が正で、ここでは上書きしない）
            s.quality = 0.5f;
            importer.defaultSampleSettings = s;
        }
    }
}
