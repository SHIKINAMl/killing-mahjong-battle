using UnityEngine;
using UnityEditor;
using KillingMahjong.Common;

namespace KillingMahjong.Editor
{
    public class TitleEffectCreator : MonoBehaviour
    {
        // 一時的に非表示。Player Settings の Scripting Define Symbols に KM_ALL_TOOLS を足すと戻る
#if KM_ALL_TOOLS
        [MenuItem("Tools/UI/タイトル用の可愛いキラキラエフェクトを作成")]
#endif
        public static void CreateTitleEffect()
        {
            GameObject effectObj = new GameObject("TitleSparkleParticles");
            var ps = effectObj.AddComponent<ParticleSystem>();
            var psRenderer = effectObj.GetComponent<ParticleSystemRenderer>();
            
            // 女の子やボタンの後ろに描画させる
            psRenderer.sortingOrder = UISortingOrders.TitleParticle;
            
            // メイン設定
            var main = ps.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 15f); // ふんわり動く
            main.startSize = new ParticleSystem.MinMaxCurve(10f, 30f); // キラキラを少し大きめに
            main.startColor = new Color(1f, 0.6f, 0.8f, 0.8f); // 可愛いピンク色
            main.gravityModifier = -0.01f; // さらにゆっくり上に舞う
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            
            // エミッション設定
            var emission = ps.emission;
            emission.rateOverTime = 15f; // 数を少し抑えて上品に
            
            // シェイプ設定（画面の下から広範囲に発生）
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2000f, 100f, 1f); // 画面幅を覆うサイズ（適宜調整）
            shape.position = new Vector3(0, -600f, 0); // 画面下部に配置
            
            // Color Over Lifetime（徐々に現れて、徐々に消える）
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.8f, 0.9f), 0.0f), new GradientColorKey(new Color(1f, 0.4f, 0.6f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1.0f) }
            );
            colorOverLifetime.color = grad;

            // Noise（シャボン玉や光のようにふわふわ舞う）
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f; // 揺れを優しく
            noise.frequency = 0.3f;
            
            Selection.activeGameObject = effectObj;
            Debug.Log("【成功】タイトル用の可愛いキラキラエフェクトを作成しました！");
        }
    }
}
