using UnityEngine;

namespace Artemis
{
    /// <summary>
    /// §9.1 スケールと精度。CSV(km) → Unity 単位への唯一の変換基準。
    /// 1 Unityユニット = 1,000 km（地球‑月 ≈ 384 ユニット）。
    /// この縮尺により全座標が O(400) に収まり、float32 でも精度劣化（§3のジッタ）を回避する。
    /// </summary>
    [CreateAssetMenu(fileName = "ScaleConfig", menuName = "Artemis/Scale Config")]
    public class ScaleConfig : ScriptableObject
    {
        [Header("縮尺（§9.1 唯一の基準）")]
        [Tooltip("1 Unityユニットあたりの km。既定 1000 km/unit。")]
        public double kmPerUnit = 1000.0;

        [Header("天体サイズ（§9.1 視認用に誇張・実寸ではない）")]
        [Tooltip("地球の表示半径[unit]。実半径6371km=6.371unitは点になるため誇張。")]
        public float earthDisplayRadiusUnit = 8f;
        public float moonDisplayRadiusUnit = 3f;
        public float orionDisplayRadiusUnit = 0.6f;

        [Header("実半径（参考・最接近判定用 [km]）")]
        public double earthRealRadiusKm = 6371.0;
        public double moonRealRadiusKm = 1737.0;

        /// <summary>km(double) → Unity座標(double)。表示直前に float へ落とす。</summary>
        public double KmToUnit(double km) => km / kmPerUnit;

        /// <summary>km3次元 → Unity Vector3。CSVは地球中心慣性系xy(z=0)。
        /// Unityは左手系・Yが上のため (x,z,y) に割当て、xy平面を水平面へ寝かせる。</summary>
        public Vector3 KmToUnityPos(double xKm, double yKm, double zKm)
        {
            return new Vector3(
                (float)(xKm / kmPerUnit),
                (float)(zKm / kmPerUnit),   // 物理zを上方向へ（簡易3D, 通常0）
                (float)(yKm / kmPerUnit));
        }
    }
}
