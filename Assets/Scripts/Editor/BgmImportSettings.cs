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

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(BgmFolder)) return;

            var importer = (AudioImporter)assetImporter;
            var s = importer.defaultSampleSettings;
            if (s.loadType == AudioClipLoadType.CompressedInMemory) return;
            s.loadType = AudioClipLoadType.CompressedInMemory;
            importer.defaultSampleSettings = s;
        }
    }
}
