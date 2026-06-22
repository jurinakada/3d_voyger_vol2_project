using UnityEngine;

namespace Artemis
{
    /// <summary>
    /// §9.3 VR要件。観察用途のカメラ操作と VR酔い対策を実装。
    /// - カメラの急加速を避け、移動は緩やかに（加速度制限・スムージング）
    /// - 固定参照枠（コクピット枠・グリッド）を視界に常設
    /// - 視点移動はスナップ回転（連続回転より酔いにくい）を既定とする
    /// Unity XR Interaction Toolkit / OpenXR を前提（XR Originにアタッチ）。
    /// </summary>
    public class VRComfortRig : MonoBehaviour
    {
        [Header("移動（§9.3 緩やかに）")]
        [Tooltip("最大移動速度 [unit/s]。1unit=1000kmなので俯瞰移動は控えめに。")]
        public float maxMoveSpeed = 40f;
        [Tooltip("加速のなめらかさ。小さいほど緩慢で酔いにくい。")]
        public float moveSmoothing = 3f;

        [Header("回転（スナップ推奨）")]
        public bool useSnapTurn = true;
        public float snapAngle = 30f;
        public float snapCooldown = 0.25f;

        [Header("固定参照枠（§9.3）")]
        [Tooltip("コクピット枠/グリッドのルート。HMDに追従させ視界に常設。")]
        public Transform cockpitFrame;
        public Transform referenceGrid;
        [Tooltip("ビネット（周辺視野を狭める酔い軽減）の有効化。")]
        public bool useVignetteOnMove = true;

        private Vector3 _vel;
        private float _snapTimer;

        /// <summary>入力(-1..1)を受けて緩やかに俯瞰移動する。急加速を抑制。</summary>
        public void Locomote(Vector2 stick, float deltaTime)
        {
            Vector3 target = (transform.right * stick.x + transform.forward * stick.y) * maxMoveSpeed;
            _vel = Vector3.Lerp(_vel, target, moveSmoothing * deltaTime); // なめらか加速
            transform.position += _vel * deltaTime;

            if (useVignetteOnMove)
                SetVignette(Mathf.Clamp01(_vel.magnitude / Mathf.Max(1f, maxMoveSpeed)));
        }

        /// <summary>スナップ回転（連続回転を避け酔いを軽減）。</summary>
        public void Turn(float dir, float deltaTime)
        {
            if (useSnapTurn)
            {
                _snapTimer -= deltaTime;
                if (Mathf.Abs(dir) > 0.5f && _snapTimer <= 0f)
                {
                    transform.Rotate(Vector3.up, Mathf.Sign(dir) * snapAngle);
                    _snapTimer = snapCooldown;
                }
            }
            else
            {
                transform.Rotate(Vector3.up, dir * 60f * deltaTime); // 連続(非推奨)
            }
        }

        void SetVignette(float amount)
        {
            // 実装はポストプロセス(URP Vignette)やシェーダに委譲。
            // ここでは強度のみ公開し、移動中だけ周辺を暗くして酔いを軽減する。
            Shader.SetGlobalFloat("_ComfortVignette", amount);
        }

        void LateUpdate()
        {
            // コクピット枠・グリッドは常に視界基準に固定（HMD追従）。
            if (cockpitFrame) cockpitFrame.localPosition = Vector3.zero;
        }
    }
}
