using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// 対局BGMのうち、**本体と別のファイルにしてある案**（第4案・夜卓の灯火）の置き場（2026-10-09）。
    ///
    /// **なぜ分けたか。** BGM を全部 `Resources` に入れていたら、WebGL のビルドの本体（.data）が
    /// 168MiB になり、GitHub の「1ファイル100MBまで」に当たって上げられなくなった。
    /// 案を1つ足すたびに 30〜40MB 増えるので、曲を消して凌ぐのには限りがある
    /// （ユーザー:「BGMのサイズが大きくなりすぎる問題どうにかできませんか？URLで取得するなど」）。
    ///
    /// **どう分けたか。** 案ごとに1つのアセットバンドルにして `StreamingAssets/Bgm/` へ置く。
    /// ビルドすると本体の隣（`StreamingAssets/Bgm/bgm_p4` など）に別のファイルとして出るので、
    /// ゲームが動き出してから、その案が要るときに URL で取りに行く。
    ///   ・1ファイルは 30〜40MB。上限に当たらない
    ///   ・最初に読む本体が軽くなる（選んでいない案は読まない）
    ///   ・**曲の中身は `Resources` に入れていたときと同じ**（同じ取り込み設定・同じサンプル数）。
    ///     MP3 などを直接 URL で読むやり方は、頭と尻に無音が付いて継ぎ目と小節がずれるので使わない
    ///
    /// 元の WAV は `Assets/BgmBundles/<案>/`。バンドルは `BgmBundleBuilder`（Editor）が作る。
    /// WebGL のビルド（`KillingMahjong/Build/Build WebGL for GitHub Pages`）は、先にこれを作り直す。
    ///
    /// **エディタで再生するときは、バンドルを使わず元の WAV をそのまま読む。**
    /// バンドルは WebGL 向けに作るので、エディタ（Windows）では読めない。
    /// つまり「届くまで待つ」所は、エディタでは一瞬も待たない。確かめるにはビルドした物をブラウザで動かすこと。
    ///
    /// 従来の曲・場面の曲・効果音は、今までどおり `Resources`。
    /// </summary>
    public static class BgmBank
    {
        /// <summary>1つの案。</summary>
        private sealed class BankSet
        {
            public string Prefix;        // 曲名の頭の印（"p4_" / "rt_"）
            public string BundleName;    // StreamingAssets/Bgm/ の中のファイル名
            public string SourceFolder;  // 元の WAV の場所（エディタ用）

            public AssetBundle Bundle;
            public bool Loading;
            public bool Failed;
            public float FailedAt;
            public readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        }

        public const string P4Prefix = "p4_";
        public const string RtPrefix = "rt_";

        /// <summary>バンドルの名前。`BgmBundleBuilder` がこの名前で作る。</summary>
        public const string P4Bundle = "bgm_p4";
        public const string RtBundle = "bgm_rt";

        /// <summary>元の WAV の場所。`BgmBundleBuilder` がここから作る。</summary>
        public const string P4SourceFolder = "Assets/BgmBundles/Proposal4";
        public const string RtSourceFolder = "Assets/BgmBundles/ReturningTheme";

        /// <summary>ビルドに入るバンドルの置き場（`Assets/` からの道）。実行時は StreamingAssets の下の `Bgm/`。</summary>
        public const string OutputFolder = "Assets/StreamingAssets/Bgm";

#if !UNITY_EDITOR
        private const string StreamingSubFolder = "Bgm";

        /// <summary>読めなかったあと、次に取りに行くまで空ける秒数。</summary>
        private const float RetrySeconds = 5f;

        private static BgmBankRunner s_runner;
#endif

        private static readonly BankSet[] Sets =
        {
            new BankSet { Prefix = P4Prefix, BundleName = P4Bundle, SourceFolder = P4SourceFolder },
            new BankSet { Prefix = RtPrefix, BundleName = RtBundle, SourceFolder = RtSourceFolder },
        };

        /// <summary>曲名が別ファイルの案の物なら、その頭の印（"p4_" / "rt_"）。そうでなければ null。</summary>
        public static string PrefixOf(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return null;
            foreach (BankSet set in Sets)
            {
                if (clipName.StartsWith(set.Prefix)) return set.Prefix;
            }
            return null;
        }

        private static BankSet Find(string prefix)
        {
            foreach (BankSet set in Sets)
            {
                if (set.Prefix == prefix) return set;
            }
            return null;
        }

        /// <summary>その案の曲を、いますぐ読めるか。</summary>
        public static bool IsReady(string prefix)
        {
            BankSet set = Find(prefix);
            if (set == null) return false;
#if UNITY_EDITOR
            return true;
#else
            return set.Bundle != null;
#endif
        }

        /// <summary>取りに行ったが読めなかったか（通信が切れている、ファイルが無い、など）。</summary>
        public static bool HasFailed(string prefix)
        {
            BankSet set = Find(prefix);
            return set != null && set.Failed && set.Bundle == null;
        }

        /// <summary>
        /// その案のファイルを取りに行かせる。もう読めている・取りに行っている最中なら何もしない。
        /// 読めなかったあとは、少し空けてからもう一度行く。
        /// </summary>
        public static void Request(string prefix)
        {
#if !UNITY_EDITOR
            BankSet set = Find(prefix);
            if (set == null || set.Bundle != null || set.Loading) return;
            if (set.Failed && Time.realtimeSinceStartup - set.FailedAt < RetrySeconds) return;
            if (!Application.isPlaying) return;

            if (s_runner == null)
            {
                var go = new GameObject("BgmBank");
                Object.DontDestroyOnLoad(go);
                s_runner = go.AddComponent<BgmBankRunner>();
            }
            set.Loading = true;
            set.Failed = false;
            s_runner.StartCoroutine(Download(set));
#endif
        }

        /// <summary>
        /// 曲を読む。その案のファイルがまだ届いていなければ null（<see cref="IsReady"/> で先に確かめること）。
        /// </summary>
        public static AudioClip Load(string clipName)
        {
            BankSet set = Find(PrefixOf(clipName));
            if (set == null) return null;

            AudioClip clip;
            if (set.Clips.TryGetValue(clipName, out clip) && clip != null) return clip;

#if UNITY_EDITOR
            clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(set.SourceFolder + "/" + clipName + ".wav");
#else
            if (set.Bundle == null) return null;
            clip = set.Bundle.LoadAsset<AudioClip>(clipName);
#endif
            if (clip != null) set.Clips[clipName] = clip;
            return clip;
        }

#if !UNITY_EDITOR
        private static IEnumerator Download(BankSet set)
        {
            string url = Application.streamingAssetsPath + "/" + StreamingSubFolder + "/" + set.BundleName;
            // PC 版などでは場所がファイルの道で返る。URL の形に直す
            if (!url.Contains("://")) url = "file://" + url;
            // ビルドごとに違う印を付ける。付けないと、上げ直したあとも
            // ブラウザが前のビルドの曲ファイルを手元の写しから使うことがある
            else url += "?v=" + Application.buildGUID;

            using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(url))
            {
                yield return request.SendWebRequest();

                AssetBundle bundle = null;
                if (request.result == UnityWebRequest.Result.Success)
                {
                    bundle = DownloadHandlerAssetBundle.GetContent(request);
                }

                set.Loading = false;
                if (bundle == null)
                {
                    set.Failed = true;
                    set.FailedAt = Time.realtimeSinceStartup;
                    Debug.LogWarning("[BgmBank] BGM のファイルが読めませんでした: " + url + " (" + request.error + ")");
                    yield break;
                }

                set.Bundle = bundle;
                Debug.Log("[BgmBank] BGM のファイルを読みました: " + set.BundleName);
            }
        }
#endif
    }
}
