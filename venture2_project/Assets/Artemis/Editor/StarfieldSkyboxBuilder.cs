using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Artemis.EditorTools
{
    /// <summary>
    /// 星空のキューブマップをその場で生成し、シーンのスカイボックスに設定する。
    /// 外部アセットのダウンロードに依存させたくないため、テクスチャをコードで作る。
    /// 生成物は <c>Assets/Artemis/Materials</c> に置かれ、2回目以降は再利用する
    /// （作り直したいときはその2つのアセットを消してから再実行する）。
    /// </summary>
    public static class StarfieldSkyboxBuilder
    {
        const string k_CubemapPath = "Assets/Artemis/Materials/StarfieldCubemap.asset";
        const string k_MaterialPath = "Assets/Artemis/Materials/StarfieldSkybox.mat";
        const string k_MaterialDir = "Assets/Artemis/Materials";

        [Tooltip("キューブマップ1面の解像度。上げると星が細かくなるがアセットも重くなる。")]
        const int k_FaceSize = 512;
        const int k_StarsPerFace = 1200;
        const int k_Seed = 20260727;

        /// <summary>宇宙の地の色。真っ黒だとVRで奥行きが掴めないので、ごくわずかに青を残す。</summary>
        static readonly Color k_SpaceColor = new Color(0.006f, 0.008f, 0.016f);

        [MenuItem("Artemis/Build Starfield Skybox", false, 11)]
        public static void BuildAndApply()
        {
            ApplyToActiveScene(EnsureMaterial());
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[StarfieldSkyboxBuilder] {SceneManager.GetActiveScene().name} に星空スカイボックスを設定しました。");
        }

        /// <summary>星空マテリアルを返す。無ければキューブマップごと生成する。</summary>
        public static Material EnsureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_MaterialPath);
            if (material != null) return material;

            if (!AssetDatabase.IsValidFolder(k_MaterialDir))
                AssetDatabase.CreateFolder("Assets/Artemis", "Materials");

            var cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(k_CubemapPath);
            if (cubemap == null)
            {
                cubemap = GenerateCubemap();
                AssetDatabase.CreateAsset(cubemap, k_CubemapPath);
            }

            var shader = Shader.Find("Skybox/Cubemap");
            if (shader == null)
            {
                Debug.LogError("[StarfieldSkyboxBuilder] Skybox/Cubemap シェーダが見つかりません。");
                return null;
            }

            material = new Material(shader);
            material.SetTexture("_Tex", cubemap);
            AssetDatabase.CreateAsset(material, k_MaterialPath);
            return material;
        }

        /// <summary>
        /// 開いているシーンに適用する。スカイボックスが暗くなると環境光もほぼ0になり、
        /// 天体の影側が真っ黒で見えなくなるため、弱い環境光を別に入れておく。
        /// </summary>
        public static void ApplyToActiveScene(Material skybox)
        {
            if (skybox == null) return;
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.11f, 0.14f);
            DynamicGI.UpdateEnvironment();
        }

        // ------------------------------------------------------------------
        static Cubemap GenerateCubemap()
        {
            var cube = new Cubemap(k_FaceSize, TextureFormat.RGB24, true);
            var rng = new System.Random(k_Seed);
            var pixels = new Color[k_FaceSize * k_FaceSize];

            for (int face = 0; face < 6; face++)
            {
                for (int i = 0; i < pixels.Length; i++) pixels[i] = k_SpaceColor;

                int placed = 0;
                while (placed < k_StarsPerFace)
                {
                    float x = (float)rng.NextDouble() * k_FaceSize;
                    float y = (float)rng.NextDouble() * k_FaceSize;

                    // 面の中央から離れた画素ほど立体角が小さい。そのまま等確率で置くと
                    // キューブの角に星が密集するので、立体角に比例した確率で間引く。
                    float s = x / k_FaceSize * 2f - 1f;
                    float t = y / k_FaceSize * 2f - 1f;
                    float solidAngle = Mathf.Pow(1f + s * s + t * t, -1.5f);
                    if (rng.NextDouble() > solidAngle) continue;

                    SplatStar(pixels, x, y, rng);
                    placed++;
                }

                cube.SetPixels(pixels, (CubemapFace)face);
            }

            cube.Apply();
            return cube;
        }

        static void SplatStar(Color[] pixels, float cx, float cy, System.Random rng)
        {
            // 暗い星が大多数、明るい星がごく少数になるよう偏らせる。
            float magnitude = Mathf.Pow((float)rng.NextDouble(), 5f);
            float brightness = 0.20f + magnitude * 1.6f;
            float radius = 0.45f + magnitude * 1.1f;

            // 青白い星と赤みがかった星を少し混ぜる。
            float hueMix = (float)rng.NextDouble();
            var tint = Color.Lerp(new Color(0.78f, 0.85f, 1f), new Color(1f, 0.86f, 0.72f), hueMix);

            int reach = Mathf.CeilToInt(radius * 2.5f);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx) - reach);
            int x1 = Mathf.Min(k_FaceSize - 1, Mathf.FloorToInt(cx) + reach);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy) - reach);
            int y1 = Mathf.Min(k_FaceSize - 1, Mathf.FloorToInt(cy) + reach);

            float twoRSq = 2f * radius * radius;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float falloff = Mathf.Exp(-(dx * dx + dy * dy) / twoRSq);
                    if (falloff < 0.004f) continue;

                    int idx = y * k_FaceSize + x;
                    pixels[idx] += tint * (brightness * falloff);
                }
            }
        }
    }
}
