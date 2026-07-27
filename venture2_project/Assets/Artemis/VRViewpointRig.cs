using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace Artemis
{
    /// <summary>
    /// §9.3 視点切替のVR版。HMDのカメラは TrackedPoseDriver が毎フレーム姿勢を書くため、
    /// カメラを直接動かす <see cref="ViewpointController"/> はVRで使えない。
    /// 代わりに XR Origin 自体を移動・拡大して「どこから見るか」を切り替える。
    ///
    /// 拡大は XR Origin の localScale（＝XRのワールドスケール）で行う。
    /// 利用者が巨人になるだけで、CSVの数値も 1 unit = 1000 km の定義（<see cref="ScaleConfig"/>）も一切変えない。
    ///
    /// あわせて Starter Assets のリグを「床のある部屋」向けから「宇宙空間」向けに設定し直す
    /// （重力OFF・スティックはテレポートではなく飛行・レイの到達距離を拡大率に追従）。
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
                 "床の無いこのシーンではONにしないと俯瞰視点で落下し続ける。")]
        public bool disableRigGravity = true;
        [Tooltip("Starter Assets の既定はスティック＝テレポートで、Moveアクション自体が無効になっている。" +
                 "宇宙空間にはテレポート先の床が無いので、ONにして連続移動へ切り替える。")]
        public bool useContinuousMove = true;
        [Tooltip("見ている方向へ上下も含めて自由に飛ぶ。OFFだと水平移動のみ。")]
        public bool enableFlyMovement = true;
        [Tooltip("スティック移動の速さ[unit/s]。実際の速さには XR Origin の拡大率が掛かる" +
                 "（俯瞰は30倍なので 2.5 → 75 unit/s ＝ 地球‑月間を約5秒）。0以下ならリグの設定のまま。")]
        public float moveSpeed = 2.5f;
        [Tooltip("右スティックをスナップターンに割り当てる（左スティック＝飛行）。" +
                 "OFFだと両スティックが飛行になり、向きは頭を回して変える。")]
        public bool rightStickTurns = true;
        [Tooltip("テレポートを止める。床が無い宇宙空間では飛び先が無く、狙っている間はUIのレイも消えるため。")]
        public bool disableTeleport = true;
        [Tooltip("レイの到達距離を XR Origin の拡大率に合わせて伸ばす。" +
                 "レイの距離はワールド固定値なので、伸ばさないと俯瞰視点（30倍）でパネルまで届かない。")]
        public bool scaleInteractorReach = true;

        public Viewpoint Current { get; private set; } = Viewpoint.Overview;

        private float _orionYaw;

        /// <summary>レイの到達距離の初期値。拡大率を掛ける元になるので、拡大前に控えておく。</summary>
        private struct CasterReach
        {
            public CurveInteractionCaster caster;
            public float castDistance;
        }

        private struct VisualReach
        {
            public CurveVisualController visual;
            public LineRenderer line;
            public float maxCurveDistance;
            public float restingLength;
            public float lineWidth;
        }

        private readonly List<CasterReach> _casters = new List<CasterReach>();
        private readonly List<VisualReach> _visuals = new List<VisualReach>();

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
            CacheInteractorReach();
        }

        void Start()
        {
            // ロコモーションの設定は Start で行う。ControllerInputActionManager が OnEnable で
            // アクションの有効/無効を書き戻すため、Awake でやると上書きされてしまう。
            ConfigureSpaceLocomotion();
            SetOverview();
        }

        void LateUpdate()
        {
            // 俯瞰視点は切替時に一度だけ配置する（毎フレーム上書きすると
            // Starter Assets のロコモーション＝スティック移動を打ち消してしまう）。
            if (Current != Viewpoint.OrionView) return;

            if (continuousYawAlign)
                _orionYaw = Mathf.MoveTowardsAngle(_orionYaw, VelocityYaw(), yawAlignSpeed * Time.deltaTime);

            ApplyOrionPose();
        }

        // ------------------------------------------------------------------
        // 宇宙空間向けのリグ設定
        // ------------------------------------------------------------------
        /// <summary>
        /// Starter Assets のリグは「床のある部屋」を前提にしている。宇宙空間で使うには
        /// 重力を切り（切らないと床が無いので落下し続ける）、スティックをテレポートから
        /// 飛行へ切り替える必要がある。
        /// </summary>
        void ConfigureSpaceLocomotion()
        {
            if (rig == null) return;

            if (disableRigGravity)
            {
                foreach (var gravity in rig.GetComponentsInChildren<GravityProvider>(true))
                {
                    gravity.useGravity = false;
                    gravity.enabled = false;
                }
            }

            foreach (var move in rig.GetComponentsInChildren<ContinuousMoveProvider>(true))
            {
                move.enableFly = enableFlyMovement;
                if (moveSpeed > 0f) move.moveSpeed = moveSpeed;
            }

            if (useContinuousMove)
                ConfigureControllers();

            if (disableTeleport)
                DisableTeleportActions();
        }

        /// <summary>
        /// <c>smoothMotionEnabled</c> が false（Starter Assets の既定）だと Move アクションが
        /// 無効化され、スティックはテレポート専用になる。宇宙空間ではテレポート先が無いので
        /// 「スティックを倒しても何も起きない」状態になってしまう。
        /// </summary>
        void ConfigureControllers()
        {
            foreach (var controller in rig.GetComponentsInChildren<ControllerInputActionManager>(true))
            {
                // Starter Assets のリグでは "Left Controller" / "Right Controller" という名前。
                bool isRight = controller.name.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (rightStickTurns && isRight)
                {
                    // 右スティックは移動に使わずスナップターンへ回す（座ったままでも向きを変えられる）。
                    controller.smoothTurnEnabled = false;
                    controller.smoothMotionEnabled = false;
                }
                else
                {
                    controller.smoothMotionEnabled = true;
                }
            }
        }

        /// <summary>
        /// テレポートのアクションを止める。<see cref="ControllerInputActionManager"/> は OnEnable で
        /// アクションを有効化するため、Start でこちらから止める必要がある。
        /// </summary>
        static void DisableTeleportActions()
        {
            var managers = FindObjectsByType<InputActionManager>(FindObjectsInactive.Include);

            foreach (var manager in managers)
            {
                foreach (var asset in manager.actionAssets)
                {
                    if (asset == null) continue;
                    foreach (var action in asset)
                    {
                        if (action.name.StartsWith("Teleport Mode"))
                            action.Disable();
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // レイの到達距離（ワールド固定値なので拡大率に追従させる）
        // ------------------------------------------------------------------
        void CacheInteractorReach()
        {
            _casters.Clear();
            _visuals.Clear();
            if (rig == null) return;

            foreach (var caster in rig.GetComponentsInChildren<CurveInteractionCaster>(true))
                _casters.Add(new CasterReach { caster = caster, castDistance = caster.castDistance });

            foreach (var visual in rig.GetComponentsInChildren<CurveVisualController>(true))
            {
                var line = visual.GetComponent<LineRenderer>();
                _visuals.Add(new VisualReach
                {
                    visual = visual,
                    line = line,
                    maxCurveDistance = visual.maxVisualCurveDistance,
                    restingLength = visual.restingVisualLineLength,
                    lineWidth = line != null ? line.widthMultiplier : 0f,
                });
            }
        }

        /// <summary>
        /// パネルは XR Origin の子なので拡大率ぶん遠ざかるが、レイの到達距離はワールド固定値。
        /// 揃えないと俯瞰視点（30倍）でパネルの手前でレイが途切れ、ボタンを押せない。
        /// </summary>
        void ApplyInteractorReach(float worldScale)
        {
            if (!scaleInteractorReach) return;

            foreach (var entry in _casters)
            {
                if (entry.caster == null) continue;
                entry.caster.castDistance = entry.castDistance * worldScale;
            }

            foreach (var entry in _visuals)
            {
                if (entry.visual == null) continue;
                entry.visual.maxVisualCurveDistance = entry.maxCurveDistance * worldScale;
                entry.visual.restingVisualLineLength = entry.restingLength * worldScale;
                if (entry.line != null) entry.line.widthMultiplier = entry.lineWidth * worldScale;
            }
        }

        // ------------------------------------------------------------------
        /// <summary>Orionの現在位置に、保持しているヨーのままリグを置く。</summary>
        private void ApplyOrionPose()
        {
            if (rig == null || orion == null) return;
            var rot = Quaternion.Euler(0f, _orionYaw, 0f);
            rig.SetPositionAndRotation(orion.position + rot * orionFollowOffset, rot);
        }

        private void SetWorldScale(float scale)
        {
            scale = Mathf.Max(0.001f, scale);
            rig.localScale = Vector3.one * scale;
            ApplyInteractorReach(scale);
        }

        // ---- IViewpointSwitcher ------------------------------------------
        public void SetOverview()
        {
            Current = Viewpoint.Overview;
            if (rig == null) return;
            rig.SetPositionAndRotation(overviewPosition, Quaternion.Euler(0f, overviewYaw, 0f));
            SetWorldScale(overviewWorldScale);
        }

        public void SetOrionView()
        {
            Current = Viewpoint.OrionView;
            if (rig == null) return;
            SetWorldScale(orionViewWorldScale);
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
