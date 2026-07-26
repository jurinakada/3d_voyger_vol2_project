using UnityEngine;

namespace Artemis
{
    public enum Viewpoint { Overview, OrionView }

    /// <summary>
    /// §9.3 視点切替：地球‑月系を外から見渡す俯瞰視点と、Orion宇宙船から見た視点を切り替える。
    /// Main Camera に付与し、OrbitPlayer が毎フレーム更新する天体位置を追従する。
    /// </summary>
    public class ViewpointController : MonoBehaviour
    {
        [Header("依存")]
        public OrbitPlayer player;
        public Transform orion;
        public Transform earth;

        [Header("俯瞰視点")]
        public Vector3 overviewPosition = new Vector3(0, 600, 0);
        public Vector3 overviewEulerAngles = new Vector3(90, 0, 0);

        [Header("Orion視点")]
        [Tooltip("Orion位置からのローカルオフセット（機体をよける程度の微小値）")]
        public Vector3 orionViewOffset = new Vector3(0, 0.2f, 0);
        [Tooltip("速度方向ではなく常に地球を向く場合はON")]
        public bool orionViewLooksAtEarth = false;

        public Viewpoint Current { get; private set; } = Viewpoint.Overview;

        void Reset()
        {
            player = FindFirstObjectByType<OrbitPlayer>();
        }

        void LateUpdate()
        {
            if (Current == Viewpoint.Overview)
            {
                transform.position = overviewPosition;
                transform.eulerAngles = overviewEulerAngles;
            }
            else if (orion != null)
            {
                transform.position = orion.position + orionViewOffset;
                if (orionViewLooksAtEarth && earth != null)
                    transform.rotation = Quaternion.LookRotation(earth.position - transform.position, Vector3.up);
                else
                {
                    Vector3 dir = player != null ? player.CurrentOrionVelocityDir : Vector3.forward;
                    if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
                    transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                }
            }
        }

        public void SetOverview() => Current = Viewpoint.Overview;
        public void SetOrionView() => Current = Viewpoint.OrionView;
        public void ToggleView() => Current = (Current == Viewpoint.Overview) ? Viewpoint.OrionView : Viewpoint.Overview;
    }
}
