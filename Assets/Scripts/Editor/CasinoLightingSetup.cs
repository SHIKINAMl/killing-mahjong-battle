using UnityEditor;
using UnityEngine;

namespace KillingMahjong.Editor
{
    public class CasinoLightingSetup : EditorWindow
    {
        [MenuItem("Tools/Lighting/カジノ風スポットライトを配置")]
        public static void SetupCasinoLighting()
        {
            // 既存のDirectional Lightがあれば少し暗くする（消してしまっていてもOK）
            Light[] existingLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var light in existingLights)
            {
                if (light.type == LightType.Directional)
                {
                    light.intensity = 0.2f;
                    light.color = ColorUtility.TryParseHtmlString("#4A5568", out Color c) ? c : Color.gray; // 暗いブルーグレー
                }
            }

            // 麻雀卓を照らすメインのスポットライトを作成
            GameObject spotLightObj = new GameObject("TableSpotlight");
            Light spotLight = spotLightObj.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.spotAngle = 60f; // 照射角を広めに
            spotLight.innerSpotAngle = 40f;
            spotLight.range = 20f;
            spotLight.intensity = 5f; // 明るめ
            spotLight.color = ColorUtility.TryParseHtmlString("#FFF5E1", out Color sc) ? sc : Color.white; // 暖かみのあるクリーム色
            
            // 影を有効にする（より立体的になる）
            spotLight.shadows = LightShadows.Soft;
            spotLight.shadowStrength = 0.8f;

            // 位置を卓の真上（少し手前）に配置して下に向ける
            // ※座標は一般的な中央配置を想定。卓の位置に合わせて後でUnity上で微調整してください
            spotLightObj.transform.position = new Vector3(0f, 6f, -1f);
            spotLightObj.transform.rotation = Quaternion.Euler(75f, 0f, 0f); // 真下より少しだけ斜め

            // 環境光（Ambient Light）を暗いネイビーに設定して、真っ暗になるのを防ぐ
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color32(20, 25, 40, 255); // 暗いネイビー

            Undo.RegisterCreatedObjectUndo(spotLightObj, "Create Casino Spotlight");
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            
            Debug.Log("カジノ風のスポットライトを配置し、環境光を調整しました！");
        }
    }
}
