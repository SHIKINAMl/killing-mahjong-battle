using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace KillingMahjong.Managers.Tutorial
{
    /// <summary>
    /// チュートリアルのセリフの強調（2026-10-08）。
    ///
    /// プランナーの指示: **フロー図で青い太字になっている語句を、ゲームでは黄色の太字にする。**
    /// フォントに太字が無いときは、代わりに文字を 1pt 大きくする。
    ///
    /// **どの語句を強調するかは、フロー図から自動で起こした表が持つ**
    /// （<c>TutorialEmphasis.Table.cs</c>、作り方は km-docs/tools/tutorial_emphasis）。
    /// 台本の文には印を書き込まない。図が変わったら表を作り直すだけで済み、
    /// 台本のほうは文面だけを見ていればよい。
    ///
    /// 例外は、実行時に牌名や倍率が入る行。文面が決まっていないので表では引けない。
    /// そういう行だけ、コード側で <see cref="Open"/>／<see cref="Close"/> の印を直接書く。
    ///
    /// **太字は、太字用のフォントを実行時に用意して出す。**
    /// セリフに使っているフォントアセット（PixelMplus10_DynamicFixed）には太字が入っておらず、
    /// TMP の擬似太字（&lt;b&gt;）は輪郭を太らせる方式なので、このアセットの焼き方
    /// （90pt に対して余白 9）では 15pt のセリフで 0.3 画素ほどしか太らず、見分けがつかない。
    /// 同じ書体の太字（PixelMplus10-Bold.ttf）は Resources に入っているので、そこから作る。
    ///
    /// **既存のフォントアセットの「太字」欄には登録しない。** 登録すると、このフォントで
    /// 太字指定をしている全画面（対局・部屋・タイトル）の見た目が一斉に変わる。
    /// ここでは &lt;font&gt; タグで、強調した語句だけを太字のフォントへ切り替えている。
    /// </summary>
    internal static partial class TutorialEmphasis
    {
        /// <summary>強調の始まりと終わりの印。表で引けない行（実行時に語が入る行）に直接書く。</summary>
        public const char Open = '⟦';
        public const char Close = '⟧';

        /// <summary>強調の色。範囲を囲む枠（TutorialHighlightUI）と同じ金色。</summary>
        private const string ColorTag = "<color=#FFD700>";

        private const string BoldFontResource = "PixelMplus-20130602/PixelMplus-20130602/PixelMplus10-Bold";
        private const string BoldFontName = "PixelMplus10_TutorialBold";

        // セリフのフォントアセットと同じ焼き方にそろえる（90pt・余白9・SDF）。
        // 違えると、太字の語句だけ輪郭の締まり方が変わる
        private const int SamplingPointSize = 90;
        private const int AtlasPadding = 9;
        private const int AtlasSize = 1024;

        private static TMP_FontAsset _bold;
        private static bool _boldTried;
        private static bool _requestHooked;

        /// <summary>太字のフォントが使えるか。使えなければ「黄色＋1pt 大きく」で出す。確認用にも見せている。</summary>
        public static bool UsesBoldFont
        {
            get { EnsureBoldFont(); return _bold != null; }
        }

        /// <summary>
        /// セリフに強調を入れて返す。強調する所が無ければ、渡された文をそのまま返す。
        /// </summary>
        public static string Apply(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 印が直接書いてある行
            if (text.IndexOf(Open) >= 0) return ReplaceMarkers(text);

            // すでにタグで飾ってある文には手を出さない（二重に掛けない）
            if (text.IndexOf('<') >= 0) return text;

            int[] map;
            string key = Normalize(text, out map);
            string[] phrases;
            if (key.Length == 0 || !Table.TryGetValue(key, out phrases)) return text;

            // 語句の位置を、詰めた文の上で前から順に探す
            var ranges = new List<KeyValuePair<int, int>>();   // 元の文での [始まり, 終わり)
            int from = 0;
            foreach (string phrase in phrases)
            {
                int at = key.IndexOf(phrase, from, StringComparison.Ordinal);
                if (at < 0) continue;
                ranges.Add(new KeyValuePair<int, int>(map[at], map[at + phrase.Length - 1] + 1));
                from = at + phrase.Length;
            }
            if (ranges.Count == 0) return text;

            string open = OpenTag, close = CloseTag;
            var sb = new StringBuilder(text.Length + ranges.Count * (open.Length + close.Length));
            int pos = 0;
            foreach (var range in ranges)
            {
                sb.Append(text, pos, range.Key - pos);
                sb.Append(open);
                sb.Append(text, range.Key, range.Value - range.Key);
                sb.Append(close);
                pos = range.Value;
            }
            sb.Append(text, pos, text.Length - pos);
            return sb.ToString();
        }

        /// <summary>
        /// 表を引くための詰め方。**空白・改行・かぎ括弧を抜く。**
        /// フロー図と台本とで、改行の位置や括弧の有無が違っていても同じ文として引ける。
        /// km-docs/tools/tutorial_emphasis/gen_emphasis.py の normalize と同じにしておくこと。
        /// </summary>
        private static string Normalize(string text, out int[] map)
        {
            var sb = new StringBuilder(text.Length);
            var indices = new List<int>(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ' || c == '　' || c == '\n' || c == '\r' || c == '\t'
                    || c == '「' || c == '」') continue;
                sb.Append(c);
                indices.Add(i);
            }
            map = indices.ToArray();
            return sb.ToString();
        }

        private static string ReplaceMarkers(string text)
        {
            return text.Replace(Open.ToString(), OpenTag).Replace(Close.ToString(), CloseTag);
        }

        private static string OpenTag
        {
            get
            {
                return UsesBoldFont
                    ? ColorTag + "<font=\"" + BoldFontName + "\">"
                    : ColorTag + "<size=+1>";
            }
        }

        private static string CloseTag
        {
            get { return UsesBoldFont ? "</font></color>" : "</size></color>"; }
        }

        /// <summary>
        /// 太字のフォントを用意する。**一度だけ試す。** 失敗したら「1pt 大きく」へ切り替えて、
        /// セリフのたびに作り直そうとしない。
        /// </summary>
        private static void EnsureBoldFont()
        {
            if (_bold != null || _boldTried) return;
            _boldTried = true;

            try
            {
                var ttf = Resources.Load<Font>(BoldFontResource);
                if (ttf == null)
                {
                    Debug.LogWarning("[TutorialEmphasis] 太字の書体が見つかりません: Resources/" + BoldFontResource
                                     + "。強調は「黄色＋1pt 大きく」で出します。");
                    return;
                }

                var bold = TMP_FontAsset.CreateFontAsset(ttf, SamplingPointSize, AtlasPadding,
                    GlyphRenderMode.SDFAA, AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, true);
                if (bold == null)
                {
                    Debug.LogWarning("[TutorialEmphasis] 太字のフォントを作れませんでした。強調は「黄色＋1pt 大きく」で出します。");
                    return;
                }
                bold.name = BoldFontName;

                // 行の高さや基線は、セリフのフォントアセットに合わせる。
                // 同じ書体の太字なので寸法は同じはずだが、あちらは手で調整が入っていることがある。
                // 違うまま混ぜると、太字の語句だけ上下にずれる
                var regular = TMP_Settings.defaultFontAsset;
                if (regular != null) bold.faceInfo = regular.faceInfo;

                // セリフと同じ影を付ける。付けないと、強調した語句だけ背景に沈む
                var mat = bold.material;
                if (mat != null)
                {
                    mat.name = BoldFontName + " Material";
                    mat.EnableKeyword("UNDERLAY_ON");
                    mat.SetColor("_UnderlayColor", Color.black);
                    mat.SetFloat("_UnderlayOffsetX", UI.DialogueUI.UnderlayOffsetX);
                    mat.SetFloat("_UnderlayOffsetY", UI.DialogueUI.UnderlayOffsetY);
                    mat.SetFloat("_UnderlayDilate", UI.DialogueUI.UnderlayDilate);
                    mat.SetFloat("_UnderlaySoftness", UI.DialogueUI.UnderlaySoftness);
                }

                _bold = bold;

                // <font="名前"> で引けるようにする。Resources には置いていないので、
                // 名前を聞かれたときにここから渡す
                if (!_requestHooked)
                {
                    TMP_Text.OnFontAssetRequest += OnFontAssetRequest;
                    _requestHooked = true;
                }
            }
            catch (Exception e)
            {
                _bold = null;
                Debug.LogWarning("[TutorialEmphasis] 太字のフォントの用意に失敗しました。強調は「黄色＋1pt 大きく」で出します: "
                                 + e.Message);
            }
        }

        private static TMP_FontAsset OnFontAssetRequest(int hashCode, string name)
        {
            return name == BoldFontName ? _bold : null;
        }

        /// <summary>
        /// 再生を始めるたびに静的な持ち物を捨てる。エディタは設定によって再生のたびに
        /// スクリプトを読み直さないので、前の再生で作ったフォント（もう破棄されている）を
        /// 握ったままになる。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (_requestHooked) TMP_Text.OnFontAssetRequest -= OnFontAssetRequest;
            _requestHooked = false;
            _bold = null;
            _boldTried = false;
        }
    }
}
