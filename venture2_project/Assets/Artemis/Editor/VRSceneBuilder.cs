using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

namespace Artemis.EditorTools
{
    /// <summary>
    /// VRScene を「そのままVRで発表できる状態」に組み立てるエディタ拡張。
    /// VRScene には XR リグしか入っていなかったため、MainScene と同じ軌道シミュ一式
    /// （Earth / Moon / Orion / OrbitPlayer / TrajectoryTrail）と、VR空間内UI
    /// （<see cref="VRPanelUI"/>）・VR用視点切替（<see cref="VRViewpointRig"/>）を配置する。
    ///
    /// 何度実行しても同じ結果になる（既存オブジェクトは名前で見つけて設定を上書きするだけ）。
    /// MainScene には一切触らない。
    /// </summary>
    public static class VRSceneBuilder
    {
        const string k_ScenePath = "Assets/Scenes/VRScene.unity";
        const string k_ScaleConfigPath = "Assets/Artemis/ScaleConfig.asset";
        const string k_CsvPath = "Assets/Artemis/orion_trajectory.csv";
        const string k_MaterialDir = "Assets/Artemis/Materials";
        const string k_RigPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.5.0/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        const string k_Undo = "Build VR Scene";

        [MenuItem("Artemis/Build VR Scene", false, 10)]
        public static void Build()
        {
            if (!OpenTargetScene()) return;

            var scaleConfig = AssetDatabase.LoadAssetAtPath<ScaleConfig>(k_ScaleConfigPath);
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(k_CsvPath);
            if (scaleConfig == null || csv == null)
            {
                EditorUtility.DisplayDialog("Build VR Scene",
                    $"必要なアセットが見つかりません。\n{k_ScaleConfigPath}\n{k_CsvPath}", "OK");
                return;
            }

            var earth = EnsureBody("Earth", scaleConfig.earthDisplayRadiusUnit * 2f, new Color(0.18f, 0.42f, 0.85f));
            var moon = EnsureBody("Moon", scaleConfig.moonDisplayRadiusUnit * 2f, new Color(0.72f, 0.72f, 0.70f));
            var orion = EnsureBody("Orion", scaleConfig.orionDisplayRadiusUnit * 2f, new Color(1f, 0.62f, 0.15f));

            var trail = EnsureTrail();
            var player = EnsureOrbitPlayer(scaleConfig, csv, earth, moon, orion, trail);

            var origin = EnsureXRRig();
            EnsureVRUI(player, origin, earth, orion);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"[VRSceneBuilder] {k_ScenePath} を構成しました。Play して HMD で確認してください。");
        }

        // ------------------------------------------------------------------
        static bool OpenTargetScene()
        {
            var active = SceneManager.GetActiveScene();
            if (active.path == k_ScenePath) return true;

            if (!EditorUtility.DisplayDialog("Build VR Scene",
                    $"VRScene を開いてから実行します。\n現在のシーン: {(string.IsNullOrEmpty(active.path) ? "(未保存)" : active.path)}",
                    "VRScene を開く", "キャンセル"))
                return false;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);
            return true;
        }

        static GameObject FindRoot(string name) =>
            SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name);

        static T EnsureComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        // ------------------------------------------------------------------
        // 天体（§9.1 表示半径は ScaleConfig の誇張値をそのまま使う）
        // ------------------------------------------------------------------
        static Transform EnsureBody(string name, float diameterUnit, Color color)
        {
            var go = FindRoot(name);
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = name;
                Undo.RegisterCreatedObjectUndo(go, k_Undo);
            }

            // VRではレイ操作の邪魔になるだけなので当たり判定は外す。
            var col = go.GetComponent<Collider>();
            if (col != null) Undo.DestroyObjectImmediate(col);

            Undo.RecordObject(go.transform, k_Undo);
            go.transform.localScale = Vector3.one * diameterUnit;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = EnsureMaterial(name, color);

            return go.transform;
        }

        static Material EnsureMaterial(string bodyName, Color color)
        {
            var path = $"{k_MaterialDir}/Artemis_{bodyName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader) { color = color };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

            if (!AssetDatabase.IsValidFolder(k_MaterialDir))
                AssetDatabase.CreateFolder("Assets/Artemis", "Materials");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ------------------------------------------------------------------
        static TrajectoryRenderer EnsureTrail()
        {
            var go = FindRoot("TrajectoryTrail");
            if (go == null)
            {
                go = new GameObject("TrajectoryTrail", typeof(LineRenderer), typeof(TrajectoryRenderer));
                Undo.RegisterCreatedObjectUndo(go, k_Undo);
            }

            var lr = EnsureComponent<LineRenderer>(go);
            Undo.RecordObject(lr, k_Undo);
            lr.useWorldSpace = false;
            lr.widthMultiplier = 1.5f;          // 1500 km 相当。俯瞰でも視認できる太さ。
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (lr.sharedMaterial == null)
                lr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Line.mat");

            return EnsureComponent<TrajectoryRenderer>(go);
        }

        static OrbitPlayer EnsureOrbitPlayer(ScaleConfig scaleConfig, TextAsset csv,
            Transform earth, Transform moon, Transform orion, TrajectoryRenderer trail)
        {
            var go = FindRoot("OrbitPlayer");
            if (go == null)
            {
                go = new GameObject("OrbitPlayer", typeof(OrbitPlayer));
                Undo.RegisterCreatedObjectUndo(go, k_Undo);
            }

            var player = EnsureComponent<OrbitPlayer>(go);
            Undo.RecordObject(player, k_Undo);
            player.scale = scaleConfig;
            player.csvFile = csv;
            player.earth = earth;
            player.moon = moon;
            player.orion = orion;
            player.trailRenderer = trail;
            player.playbackSeconds = 180f;
            player.speed = 1f;
            player.playing = true;
            player.frame = ReferenceFrame.Earth;
            return player;
        }

        // ------------------------------------------------------------------
        // XRリグ。既存リグはコントローラのモデルだけで TrackedPoseDriver も
        // インタラクタも無く、レイでUIを押せない。Starter Assets の完成品に置き換える。
        // ------------------------------------------------------------------
        static XROrigin EnsureXRRig()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>();
            bool hasInteractor =
                Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include).Length > 0 ||
                Object.FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include).Length > 0;

            if (origin == null || !hasInteractor)
                origin = ReplaceRig(origin);

            if (origin != null && origin.Camera != null)
            {
                var cam = origin.Camera;
                Undo.RecordObject(cam, k_Undo);
                cam.farClipPlane = 5000f;   // 地球‑月系は 400 unit 超。既定1000だと復路が切れる。
                cam.nearClipPlane = 0.05f;
            }

            if (origin != null) DisableGravity(origin);

            if (Object.FindAnyObjectByType<XRInteractionManager>() == null)
                Undo.RegisterCreatedObjectUndo(
                    new GameObject("XR Interaction Manager", typeof(XRInteractionManager)), k_Undo);

            if (Object.FindAnyObjectByType<InputActionManager>() == null)
                Debug.LogWarning("[VRSceneBuilder] InputActionManager が見つかりません。" +
                                 "XRI Default Input Actions を割り当てた InputActionManager が無いとコントローラ入力が来ません。");

            return origin;
        }

        /// <summary>
        /// Starter Assets のリグは床のある部屋を前提に重力が有効。宇宙空間には床が無いため、
        /// 切らないと俯瞰視点で自由落下し続ける。実行時にも <see cref="VRViewpointRig"/> が
        /// 同じことをするが、シーン側にも保存してインスペクタ上で分かるようにしておく。
        /// </summary>
        static void DisableGravity(XROrigin origin)
        {
            foreach (var gravity in origin.GetComponentsInChildren<GravityProvider>(true))
            {
                Undo.RecordObject(gravity, k_Undo);
                gravity.useGravity = false;
                gravity.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(gravity);
            }
        }

        static XROrigin ReplaceRig(XROrigin current)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_RigPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[VRSceneBuilder] Starter Assets のリグが見つかりません: {k_RigPrefabPath}");
                return current;
            }

            // 立ち位置は引き継ぐ（無ければ俯瞰視点の既定位置）。
            var position = current != null ? current.transform.position : new Vector3(0f, 120f, -320f);
            var rotation = current != null ? current.transform.rotation : Quaternion.identity;

            if (current != null)
            {
                bool replace = EditorUtility.DisplayDialog("Build VR Scene",
                    "現在の XR Origin にはコントローラのモデルしか無く、TrackedPoseDriver も" +
                    "インタラクタも入っていないため、VRでUIを押せません。\n\n" +
                    "Starter Assets の「XR Origin (XR Rig)」（トラッキング・レイ・移動が設定済み）に" +
                    "置き換えますか？",
                    "置き換える", "そのままにする");
                if (!replace) return current;

                Undo.DestroyObjectImmediate(current.gameObject);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, k_Undo);
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance.GetComponent<XROrigin>();
        }

        // ------------------------------------------------------------------
        static void EnsureVRUI(OrbitPlayer player, XROrigin origin, Transform earth, Transform orion)
        {
            var go = FindRoot("VR UI");
            if (go == null)
            {
                go = new GameObject("VR UI", typeof(VRViewpointRig), typeof(VRPanelUI));
                Undo.RegisterCreatedObjectUndo(go, k_Undo);
            }

            var rig = EnsureComponent<VRViewpointRig>(go);
            Undo.RecordObject(rig, k_Undo);
            rig.player = player;
            rig.earth = earth;
            rig.orion = orion;
            rig.rig = origin != null ? origin.transform : null;

            var panel = EnsureComponent<VRPanelUI>(go);
            Undo.RecordObject(panel, k_Undo);
            panel.player = player;
            panel.viewpointBehaviour = rig;
        }
    }
}
