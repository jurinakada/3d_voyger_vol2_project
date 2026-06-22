using System.Collections.Generic;
using UnityEngine;

namespace Artemis
{
    public enum ReferenceFrame { Earth, Moon }

    /// <summary>
    /// 軌道再生の中核。§9.2 時間スケーリング（実時間→数分へ圧縮・可変速）、
    /// §9.3 参照枠切替（地球基準/月基準）・現在時刻表示。
    /// 位置はdoubleで計算し、表示時のみScaleConfigでUnity座標へ変換（§9.1）。
    /// </summary>
    public class OrbitPlayer : MonoBehaviour
    {
        [Header("依存")]
        public ScaleConfig scale;
        public TextAsset csvFile;                 // §8 CSV を割当
        public Transform earth, moon, orion;      // 天体・宇宙船オブジェクト
        public TrajectoryRenderer trailRenderer;  // 軌跡描画（任意）

        [Header("§9.2 時間スケーリング")]
        [Tooltip("実ミッション全体(約9〜10日)を何秒で再生するか。既定180秒=3分。")]
        public float playbackSeconds = 180f;
        [Tooltip("可変速の倍率（一時停止=0, 早送り>1）。")]
        public float speed = 1f;
        public bool playing = true;

        [Header("§9.3 参照枠")]
        public ReferenceFrame frame = ReferenceFrame.Earth;

        // 状態
        private List<TrajectorySample> data;
        private double missionStart, missionEnd, missionDur;
        public double CurrentMissionTimeSec { get; private set; }
        public string CurrentPhase { get; private set; } = "";

        void Awake()
        {
            if (csvFile != null)
                data = TrajectoryLoader.Parse(csvFile.text);
            if (data != null && data.Count > 0)
            {
                missionStart = data[0].tSec;
                missionEnd = data[data.Count - 1].tSec;
                missionDur = missionEnd - missionStart;
                CurrentMissionTimeSec = missionStart;
                trailRenderer?.Init(data, scale);
            }
        }

        void Update()
        {
            if (data == null || data.Count == 0) return;
            if (playing)
            {
                // 再生秒 → ミッション秒（§9.2）
                double missionPerRealSec = missionDur / Mathf.Max(0.001f, playbackSeconds);
                CurrentMissionTimeSec += missionPerRealSec * speed * Time.deltaTime;
                if (CurrentMissionTimeSec > missionEnd) CurrentMissionTimeSec = missionStart; // ループ
                if (CurrentMissionTimeSec < missionStart) CurrentMissionTimeSec = missionEnd;
            }
            ApplyState(CurrentMissionTimeSec);
        }

        void ApplyState(double tSec)
        {
            var s = TrajectoryLoader.Interpolate(data, tSec);
            CurrentPhase = s.phase;

            // 参照枠原点（§9.3）。Moon基準なら月を原点に置き相対座標で表示。
            Vector3 earthPos = scale.KmToUnityPos(0, 0, 0);
            Vector3 moonPos = scale.KmToUnityPos(s.mx, s.my, s.mz);
            Vector3 orionPos = scale.KmToUnityPos(s.ox, s.oy, s.oz);

            Vector3 originOffset = (frame == ReferenceFrame.Moon) ? moonPos : earthPos;

            if (earth) earth.localPosition = earthPos - originOffset;
            if (moon) moon.localPosition = moonPos - originOffset;
            if (orion) orion.localPosition = orionPos - originOffset;

            trailRenderer?.UpdateTrail(tSec, originOffset);
        }

        // ---- UI フック（§9.3 インタラクション）-------------------------
        public void TogglePlay() => playing = !playing;
        public void SetSpeed(float s) => speed = s;
        public void StepSeek(float frac01) =>
            CurrentMissionTimeSec = missionStart + missionDur * Mathf.Clamp01(frac01);
        public void SwitchFrame(ReferenceFrame f) => frame = f;

        /// <summary>現在時刻表示用（日, 時, 分）。</summary>
        public string MissionClock()
        {
            double t = CurrentMissionTimeSec;
            int d = (int)(t / 86400); t -= d * 86400;
            int h = (int)(t / 3600); t -= h * 3600;
            int m = (int)(t / 60);
            return $"T+{d}d {h:00}:{m:00}  [{CurrentPhase}]";
        }
    }
}
