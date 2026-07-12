using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

namespace KillingMahjong.Editor
{
    public class SilhouetteCreator : MonoBehaviour
    {
        [MenuItem("Tools/UI/選択した画像の白シルエットを背面に作成する")]
        public static void CreateSilhouette()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null || selected.GetComponent<Image>() == null)
            {
                Debug.LogWarning("【エラー】対象のUI画像（女の子など）を選択してから実行してください！");
                return;
            }

            // マテリアル保存用のフォルダを作成
            if (!Directory.Exists("Assets/Materials"))
            {
                Directory.CreateDirectory("Assets/Materials");
                AssetDatabase.Refresh();
            }

            // マテリアルがなければ作成
            string matPath = "Assets/Materials/SilhouetteMaterial.mat";
            Material silhouetteMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (silhouetteMat == null)
            {
                Shader flashShader = Shader.Find("UI/Flash");
                if (flashShader == null)
                {
                    Debug.LogError("UI/Flash シェーダーが見つかりません。先にシェーダーを作成してください。");
                    return;
                }
                silhouetteMat = new Material(flashShader);
                silhouetteMat.SetFloat("_FlashAmount", 1.0f);
                silhouetteMat.SetColor("_FlashColor", Color.white);
                AssetDatabase.CreateAsset(silhouetteMat, matPath);
                AssetDatabase.SaveAssets();
            }

            // 1. オブジェクトを複製
            GameObject silhouetteObj = Instantiate(selected, selected.transform.parent);
            silhouetteObj.name = selected.name + "_Silhouette (白フチ)";
            
            // 2. ヒエラルキーの順序を「元画像より上（背面に表示されるように）」にする
            silhouetteObj.transform.SetSiblingIndex(selected.transform.GetSiblingIndex());

            // 3. マテリアルを適用
            Image silhouetteImage = silhouetteObj.GetComponent<Image>();
            silhouetteImage.material = silhouetteMat;

            // 4. 少し拡大してフチっぽく見せる
            silhouetteObj.transform.localScale = selected.transform.localScale * 1.03f;

            // 複製元の子要素（不要なもの）があれば削除
            foreach (Transform child in silhouetteObj.transform)
            {
                DestroyImmediate(child.gameObject);
            }

            Selection.activeGameObject = silhouetteObj;
            Debug.Log("【成功】背面に白いシルエットを作成しました！位置やサイズ(Scale)を微調整してください。");
        }
    }
}
