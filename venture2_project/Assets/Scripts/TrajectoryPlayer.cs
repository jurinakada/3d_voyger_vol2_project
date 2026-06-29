using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(TrajectoryCsvLoader))]
public class TrajectoryPlayer : MonoBehaviour
{
    [SerializeField] private Transform orionTarget;
    [SerializeField] private float kmPerUnityUnit = 10000f;
    [SerializeField] private float playbackSpeed = 3600f;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loop;

    public float MissionTimeSec { get; private set; }
    public bool IsPlaying { get; private set; }

    private TrajectoryCsvLoader loader;
    private int sampleIndex;

    private void Awake()
    {
        loader = GetComponent<TrajectoryCsvLoader>();
    }

    private void Start()
    {
        IsPlaying = playOnStart;
        ApplyCurrentPosition();
    }

    private void Update()
    {
        if (!IsPlaying)
        {
            return;
        }

        IReadOnlyList<TrajectorySample> samples = loader.Samples;
        if (samples.Count < 2 || orionTarget == null)
        {
            return;
        }

        MissionTimeSec += Time.deltaTime * playbackSpeed;
        float endTime = samples[samples.Count - 1].TimeSec;
        if (MissionTimeSec > endTime)
        {
            MissionTimeSec = loop ? 0f : endTime;
            IsPlaying = loop;
            sampleIndex = 0;
        }

        ApplyCurrentPosition();
    }

    public void Play()
    {
        IsPlaying = true;
    }

    public void Pause()
    {
        IsPlaying = false;
    }

    public void Seek(float missionTimeSec)
    {
        MissionTimeSec = Mathf.Max(0f, missionTimeSec);
        sampleIndex = 0;
        ApplyCurrentPosition();
    }

    private void ApplyCurrentPosition()
    {
        if (orionTarget == null)
        {
            return;
        }

        if (TryGetInterpolatedOrionPosition(MissionTimeSec, out Vector3 positionKm))
        {
            orionTarget.position = positionKm / kmPerUnityUnit;
        }
    }

    private bool TryGetInterpolatedOrionPosition(float timeSec, out Vector3 positionKm)
    {
        IReadOnlyList<TrajectorySample> samples = loader.Samples;
        positionKm = Vector3.zero;

        if (samples.Count == 0)
        {
            return false;
        }

        if (samples.Count == 1 || timeSec <= samples[0].TimeSec)
        {
            positionKm = samples[0].OrionPositionKm;
            return true;
        }

        while (sampleIndex < samples.Count - 2 && samples[sampleIndex + 1].TimeSec < timeSec)
        {
            sampleIndex++;
        }

        TrajectorySample a = samples[sampleIndex];
        TrajectorySample b = samples[Mathf.Min(sampleIndex + 1, samples.Count - 1)];
        float duration = Mathf.Max(0.0001f, b.TimeSec - a.TimeSec);
        float t = Mathf.Clamp01((timeSec - a.TimeSec) / duration);
        positionKm = Vector3.Lerp(a.OrionPositionKm, b.OrionPositionKm, t);
        return true;
    }
}

