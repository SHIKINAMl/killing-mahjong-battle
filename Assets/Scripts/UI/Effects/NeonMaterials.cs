using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 蛍光色に光らせる用のマテリアルを作る（2026-09-30）。
    ///
    /// シェーダは <c>KillingMahjong/NeonKeyAdd</c>（<c>Assets/Shaders/NeonKeyAdd.shader</c>）。
    /// 加算合成なので、下の絵へ光を足して明るくできる。
    ///
    /// **マテリアルは使う側ごとに作る。** 使い回すと、1か所で強さを変えたつもりが
    /// 別の所まで一緒に動く。作られる数は自分と相手のHP・立ち絵の数だけなので数個で済む。
    /// </summary>
    public static class NeonMaterials
    {
        public const string ShaderName = "KillingMahjong/NeonKeyAdd";

        private static Shader _shader;

        private static Shader NeonShader
        {
            get
            {
                if (_shader == null) _shader = Shader.Find(ShaderName);
                if (_shader == null)
                {
                    // **見つからないときは黙って諦める。** ここで例外を投げると、
                    // 光らせたいだけで画面が止まる
                    Debug.LogWarning("[NeonMaterials] シェーダが見つからない: " + ShaderName);
                }
                return _shader;
            }
        }

        /// <summary>
        /// 絵の全部を光らせるマテリアル（色で絞り込まない）。血のように
        /// 「その絵自体が光ってほしい」ものに使う。
        /// </summary>
        public static Material CreateAll(Color neonColor, float intensity = 1f)
        {
            // 許容値 2 は、0〜1 に正規化した RGB 空間で取りうる最大距離(√3≒1.73)より大きい。
            // つまり必ず一致するので、絵の全部が光る
            return Create(neonColor, Color.white, 2f, intensity);
        }

        /// <summary>
        /// <paramref name="keyColor"/> に近い所だけを光らせるマテリアル。
        /// 髪の黄色だけを光らせる、といった使い方をする。
        /// </summary>
        /// <param name="tolerance">0〜1 に正規化した RGB 空間での距離。255階調の 70 なら 0.275。</param>
        public static Material Create(Color neonColor, Color keyColor, float tolerance, float intensity = 1f)
        {
            var shader = NeonShader;
            if (shader == null) return null;

            var mat = new Material(shader);
            mat.name = "NeonKeyAdd (runtime)";
            mat.SetColor("_NeonColor", neonColor);
            mat.SetColor("_KeyColor", keyColor);
            mat.SetFloat("_Tolerance", tolerance);
            mat.SetFloat("_Intensity", intensity);
            return mat;
        }
    }
}
