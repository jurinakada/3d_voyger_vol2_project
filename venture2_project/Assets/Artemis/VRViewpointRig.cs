using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace Artemis
{
    /// <summary>
    /// §9.3 視点切替のVR版。HMDのカメラは TrackedPoseDriver が毎フレーム姿勢を書くため、
    /// カメラを直接動かす <see cref="ViewpointController"/> はVRで使えない。
    /// 代わりに XR Origin 自体を移動・拡大して「どこから見るか」を切り替える。
    ///
    /// 拡大は XR Origin の localScale（＝XRのワールドスケール）で行う。
    /// 利用者が巨人になるだけで、CSVの数値も 1 unit = 1000 km の定義（<see cref="ScaleConfig"/>）も一切変えない。
    /// </summary>
    public class VRViewpointRig : MonoBehaviour, IViewpointSwitcher
    {
        [Header("依存")]
        public OrbitPlayer player;
        public Transform orion;
        public Transform earth;
        [Tooltip("移動対象。未指定ならシーン内の XR Origin、それも無ければこのGameObject。")]
        public Transform rig;

        [Header("俯瞰視点")]
        [Tooltip("地球‑月系を見渡す立ち位置[unit]。1unit=1000km。")]
        public Vector3 overviewPosition = new Vector3(0f, 120f, -320f);
        public float overviewYaw = 0f;
        [Tooltip("XR Originの拡大率。大きいほど利用者が巨人になり、系全体が模型サイズに見える。")]
        public float overviewWorldScale = 30f;

        [Header("Orion視点")]
        [Tooltip("進行方向を基準にした追従位置[unit]。Z-が進行方向の後ろ、Y+が上。")]
        public Vector3 orionFollowOffset = new Vector3(0f, 0.6f, -2.5f);
        public float orionViewWorldScale = 1f;
        [Tooltip("ONで進行方向へ毎フレーム緩やかにヨー追従する。VR酔いの原因になるため既定はOFF（切替時のみ整列）。")]
        public bool continuousYawAlign = false;
        [Tooltip("continuousYawAlign時の追従の速さ[deg/s]。")]
        public float yawAlignSpeed = 20f;

        [Header("宇宙空間のロコモーション")]
        [Tooltip("XR Originの重力を切る。Starter Assetsのリグは床のある部屋向けで重力が有効なため、" +
                 "床の無いこのシーンではOFFにすると俯瞰視点で落下し続ける。")]
        public bool disableRigGravity = true;
        [Tooltip("スティック移動を上下方向にも効かせる（宇宙空間なので飛行が自然）。")]
        public bool enableFlyMovement = false;

        public Viewpoint Current { get; private set; } = Viewpoint.Overview;

        private float _orionYaw;

        void Reset()
        {
            player = FindAnyObjectByType<OrbitPlayer>();
        }

        void Awake()
        {
            if (player == null) player = FindAnyObjectByType<OrbitPlayer>();
            if (rig == null)
            {
                var origin = FindAnyObjectByType<XROrigin>();
                rig = origin != null ? origin.transform : transform;
            }
            if (player != null)
            {
                if (orion == null) orion = player.orion;
                if (earth == null) earth = player.earth;
            }
            ConfigureSpaceLocomotion(rig, disableRigGravity, enableFlyMovement);
        }

        /// <summary>
        /// Starter Assets のリグは床のある部屋を前提に重力が有効になっている。宇宙空間には床が無く、
        /// 俯瞰視点は切替時にしか位置を書かないため、そのままだと自由落下し続ける。
        /// </summary>
        public static void ConfigureSpaceLocomotion(Transform rig, bool disableGravity, bool enableFly)
        {
            if (rig == null) return;

            if (disableGravity)
            {
                foreach (var gravity in rig.GetComponentsInChildren<GravityProvider>(true))
                {
                    gravity.useGravity = false;
                    gravity.enabled = false;
                }
            }

            foreach (var move in rig.GetComponentsInChildren<ContinuousMoveProvider>(true))
                move.enableFly = enableFly;
        }

        void Start() => SetOverview();

        void LateUpdate()
        {
            // 俯瞰視点は切替時に一度だけ配置する（毎フレーム上書きすると
            // Starter Assets のロコモーション＝スティック移動・テレポートを打ち消してしまう）。
            if (Current != Viewpoint.OrionView) return;

            if (continuousYawAlign)
                _orionYaw = Mathf.MoveTowardsAngle(_orionYaw, VelocityYaw(), yawAlignSpeed * Time.deltaTime);

            ApplyOrionPose();
        }

        /// <summary>Orionの現在位置に、保持しているヨーのままリグを置く。</summary>
        private void ApplyOrionPose()
        {
            if (rig == null || orion == null) return;
            var rot = Quaternion.Euler(0f, _orionYaw, 0f);
            rig.SetPositionAndRotation(orion.position + rot * orionFollowOffset, rot);
        }

        // ---- IViewpointSwitcher ------------------------------------------
        public void SetOverview()
        {
            Current = Viewpoint.Overview;
            if (rig == null) return;
            rig.SetPositionAndRotation(overviewPosition, Quaternion.Euler(0f, overviewYaw, 0f));
            rig.localScale = Vector3.one * Mathf.Max(0.001f, overviewWorldScale);
        }

        public void SetOrionView()
        {
            Current = Viewpoint.OrionView;
            if (rig == null) return;
            rig.localScale = Vector3.one * Mathf.Max(0.001f, orionViewWorldScale);
            _orionYaw = VelocityYaw();
            ApplyOrionPose();
        }

        public void ToggleView()
        {
            if (Current == Viewpoint.Overview) SetOrionView(); else SetOverview();
        }

        /// <summary>進行方向（またはOrion→地球方向）の水平角。ロール・ピッチは付けない（酔い対策）。</summary>
        private float VelocityYaw()
        {
            Vector3 dir = player != null ? player.CurrentOrionVelocityDir : Vector3.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            return Quaternion.LookRotation(dir, Vector3.up).eulerAngles.y;
        }
    }
}
