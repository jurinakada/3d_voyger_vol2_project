using System.Collections.Generic;
using UnityEngine;

namespace Artemis
{
    /// <summary>
    /// 物理再生(OrbitPlayer)の上に NASA 実データ軌道を重ね描きする。
    /// 時刻は OrbitPlayer.CurrentMissionTimeSec に同期(整合済みCSVは物理タイムラインへ変換済み)。
    /// 実月(NASA Moon)も別球で表示し、物理月(固定半径384,400km)との差を見せる。
    /// マーカー・軌跡は実行時生成なので、シーンにはこのコンポーネントを置くだけでよい。
    /// </summary>
    public class NasaOverlayPlayer : MonoBehaviour
    {
        [Tooltip("scripts/build_comparison_csv.py が出力する整合済み NASA CSV")]
        public TextAsset csvFile;
        public Color orionColor = new Color(1f, 0.55f, 0.1f);      // オレンジ
        public Color moonGhostColor = new Color(0.35f, 0.85f, 1f); // 水色
        [Tooltip("UIから切替。false で NASA 系を非表示")]
        public bool visible = true;

        /// <summary>物理Orionと NASA Orion の現在距離 [km](UI表示用)。範囲外は NaN。</summary>
        public double CurrentSeparationKm { get; private set; } = double.NaN;

        private OrbitPlayer orbit;
        private List<TrajectorySample> data;
        private Transform orionN, moonN;
        private TrajectoryRenderer trail;

        void Awake()
        {
            if (csvFile != null)
                data = TrajectoryLoader.Parse(csvFile.text);
        }

        void Start()
        {
            orbit = Object.FindFirstObjectByType<OrbitPlayer>();
            if (orbit == null || orbit.scale == null || data == null || data.Count == 0)
            {
                Debug.LogWarning("NasaOverlayPlayer: OrbitPlayer or aligned CSV missing; overlay disabled.");
                enabled = false;
                return;
            }
            var s = orbit.scale;

            orionN = CreateMarker("NASA Orion", 1.2f, orionColor);
            float moonDia = (float)(2.0 * s.moonRealRadiusKm / s.kmPerUnit); // 実寸(3.474unit)
            moonN = CreateMarker("NASA Moon (real)", moonDia, moonGhostColor);

            var trailGo = new GameObject("NASA Trail");
            trailGo.transform.SetParent(transform, false);
            var lr = trailGo.AddComponent<LineRenderer>();
            var physTrail = Object.FindFirstObjectByType<TrajectoryRenderer>();
            if (physTrail != null && physTrail.TryGetComponent(out LineRenderer physLr))
            {
                lr.sharedMaterial = physLr.sharedMaterial;   // 既存軌跡の見た目を踏襲
                lr.widthMultiplier = physLr.widthMultiplier;
            }
            else
            {
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.widthMultiplier = 0.5f;
            }
            trail = trailGo.AddComponent<TrajectoryRenderer>();
            trail.outboundColor = orionColor;  // NASA側は単色グラデで区別
            trail.flybyColor = orionColor;
            trail.returnColor = moonGhostColor;
            trail.Init(data, s);
        }

        Transform CreateMarker(string markerName, float scaleUnits, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = markerName;
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * scaleUnits;
            go.GetComponent<MeshRenderer>().material.color = color;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            return go.transform;
        }

        void Update()
        {
            if (orbit == null || data == null || data.Count == 0) return;

            // 参照枠は地球基準のみ対応(月基準時は非表示)
            double t = orbit.CurrentMissionTimeSec;
            bool inRange = t >= data[0].tSec && t <= data[data.Count - 1].tSec;
            bool show = visible && orbit.frame == ReferenceFrame.Earth && inRange;

            if (orionN) orionN.gameObject.SetActive(show);
            if (moonN) moonN.gameObject.SetActive(show);
            if (trail) trail.gameObject.SetActive(visible && orbit.frame == ReferenceFrame.Earth);

            if (!show)
            {
                CurrentSeparationKm = double.NaN;
                return;
            }

            var s = orbit.scale;
            var smp = TrajectoryLoader.Interpolate(data, t);
            orionN.localPosition = s.KmToUnityPos(smp.ox, smp.oy, smp.oz);
            moonN.localPosition = s.KmToUnityPos(smp.mx, smp.my, smp.mz);
            trail.UpdateTrail(t, Vector3.zero);

            if (orbit.orion != null)
                CurrentSeparationKm =
                    (orionN.position - orbit.orion.position).magnitude * s.kmPerUnit;
        }
    }
}
