using System.Collections.Generic;
using UnityEngine;

namespace Artemis
{
    /// <summary>
    /// 軌跡（往路/フライバイ/復路）を LineRenderer で描画。フェーズ別に色分け。
    /// 参照枠オフセットに追従し、Moon基準でも正しく相対描画する。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class TrajectoryRenderer : MonoBehaviour
    {
        public Color outboundColor = new Color(0.30f, 0.64f, 1f);
        public Color flybyColor = new Color(1f, 0.82f, 0.30f);
        public Color returnColor = new Color(1f, 0.42f, 0.62f);
        [Tooltip("現在時刻までの軌跡のみ描く（true）か全体を薄く描く（false）")]
        public bool revealProgressively = true;

        private List<TrajectorySample> data;
        private ScaleConfig scale;
        private LineRenderer lr;
        private Vector3[] worldPts;     // 参照枠オフセット前の絶対Unity座標

        public void Init(List<TrajectorySample> d, ScaleConfig s)
        {
            data = d; scale = s;
            lr = GetComponent<LineRenderer>();
            lr.useWorldSpace = false;
            worldPts = new Vector3[d.Count];
            for (int i = 0; i < d.Count; i++)
                worldPts[i] = scale.KmToUnityPos(d[i].ox, d[i].oy, d[i].oz);
        }

        public void UpdateTrail(double tSec, Vector3 originOffset)
        {
            if (data == null) return;
            int count = data.Count;
            int upto = count;
            if (revealProgressively)
            {
                upto = 1;
                while (upto < count && data[upto].tSec <= tSec) upto++;
            }
            lr.positionCount = upto;
            for (int i = 0; i < upto; i++)
                lr.SetPosition(i, worldPts[i] - originOffset);

            // フェーズ色（簡易: 末尾サンプルの相で全体色を切替えるのではなく勾配着色）
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(outboundColor, 0.0f),
                    new GradientColorKey(flybyColor, 0.5f),
                    new GradientColorKey(returnColor, 1.0f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            lr.colorGradient = grad;
        }
    }
}
