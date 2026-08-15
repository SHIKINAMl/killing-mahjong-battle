using UnityEngine;
using UnityEditor;

public class DoraEffectCreator
{
    // 一時的に非表示。Player Settings の Scripting Define Symbols に KM_ALL_TOOLS を足すと戻る
#if KM_ALL_TOOLS
    [MenuItem("Tools/Create Dora Cyber Effect Prefab")]
#endif
    public static void CreatePrefab()
    {
        // 1. ルートオブジェクトの作成
        GameObject root = new GameObject("DoraCyberEffect");

        // 2. パーティクルシステム（机から上に伸びる光の柱）の作成
        GameObject particleObj = new GameObject("LightPillarParticle");
        particleObj.transform.SetParent(root.transform);
        particleObj.transform.localPosition = Vector3.zero;
        // 真上（Y軸正方向）にパーティクルを飛ばすための回転
        particleObj.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psRenderer = particleObj.GetComponent<ParticleSystemRenderer>();
        
        // パーティクルの基本設定
        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f); // 上に素早く伸びる
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f); // 細い線
        main.startColor = new Color(0f, 0.8f, 1f, 1f); // 青色（シアン系）
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 100;

        // 発生量
        var emission = ps.emission;
        emission.rateOverTime = 80f; // 大量の線を出す

        // 形状（Coneを使って真っ直ぐ上に飛ばす）
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 0f;
        shape.radius = 0.2f;

        // 色の変化（上に行くにつれて透明に）
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0f, 0.8f, 1f), 0.0f), new GradientColorKey(new Color(0f, 0.2f, 1f), 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(1.0f, 0.2f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        colorOverLifetime.color = grad;

        // サイズの変化
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 0f);
        curve.AddKey(0.1f, 1f);
        curve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

        // レンダラー設定（Stretched Billboardで線を長く見せる）
        psRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        psRenderer.lengthScale = 4.0f; // 進行方向に伸ばす
        psRenderer.velocityScale = 0.1f;

        // マテリアルはスクリプトで生成せず、ユーザーに手動で割り当てていただく
        // (ピンク色エラーを防ぐため)

        // 3. 麻雀牌オブジェクトの作成（既存のプレハブを使用）
        GameObject tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Preafb/イーピン.prefab");
        GameObject tileObj = null;
        if (tilePrefab != null)
        {
            tileObj = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab, root.transform);
            tileObj.name = "DoraTile";
            tileObj.transform.localPosition = new Vector3(0f, 1.5f, 0f); // 光の柱の中心あたりに浮かせる
            tileObj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        }

        // 4. アニメータースクリプトのアタッチ
        if (tileObj != null)
        {
            var animator = tileObj.AddComponent<KillingMahjong.Visuals.DoraFloatAnimator>();
            animator.floatSpeed = 2f;
            animator.floatAmplitude = 0.3f;
            animator.rotationSpeed = new Vector3(0f, 60f, 0f);
        }

        // 5. プレハブの保存
        string localPath = "Assets/Prefabs/DoraCyberEffect.prefab";
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        PrefabUtility.SaveAsPrefabAsset(root, localPath);
        GameObject.DestroyImmediate(root);
        Debug.Log("DoraCyberEffect prefab created successfully at " + localPath);
    }
}
