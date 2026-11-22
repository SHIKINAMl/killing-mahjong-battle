using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 対局待ちの牌（<see cref="TileClatterEffect"/>）の動きを、光の当たり方で見せるための値を頂点へ書く（2026-11-22
    /// 「麻雀のジャラジャラは、影だけでなく、動きもノーマルマップで特徴づけられませんか」）。
    ///
    ///   ・<see cref="Tilt"/>: 牌の傾き（画面の向き。x 右・y 上。大きさは tan(角度) で 0.27 ≒ 15度まで）。
    ///     押されると進む向きへ傾き、止まると2〜3回揺れて落ち着く。KM/LitArt が法線をこの分だけ倒す
    ///   ・<see cref="Glint"/>: ぶつかった瞬間の照り返し（0〜1）。光の当たる側の縁が一瞬明るくなる
    ///
    /// UI は同じ Canvas の絵を1つのメッシュにまとめて描くので、牌ごとの値は行列では渡せない。
    /// 頂点の2つ目の UV（uv1。x,y = 傾き、z = 照り返し）に書き、Canvas の追加チャンネル TexCoord1 で送る。
    /// 値を決めるのは <see cref="TileClatterPhysics"/>（牌の速さ・ぶつかりを持っている）。
    /// 輪郭は変わらない（法線マップは光だけを変える）ので、傾きは光で分かる程度にとどめる。
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class TileLightMotion : BaseMeshEffect
    {
        private Vector2 _tilt;
        private float _glint;

        public Vector2 Tilt => _tilt;
        public float Glint => _glint;

        /// <summary>値を入れる。変わったときだけ描き直しを頼む。</summary>
        public void Set(Vector2 tilt, float glint)
        {
            if ((tilt - _tilt).sqrMagnitude < 1e-8f && Mathf.Abs(glint - _glint) < 1e-4f) return;
            _tilt = tilt; _glint = glint;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var uv1 = new Vector4(_tilt.x, _tilt.y, _glint, 0f);
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.uv1 = uv1;
                vh.SetUIVertex(v, i);
            }
        }

        /// <summary>牌が載る Canvas に、uv1 を送るチャンネルを足す（無いと Shader に 0 しか届かない）。</summary>
        public static void EnableChannel(Canvas canvas)
        {
            if (canvas == null) return;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }
    }
}
