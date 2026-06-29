using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(TrajectoryCsvLoader))]
public class OrbitLineRenderer : MonoBehaviour
{
    [SerializeField] private float kmPerUnityUnit = 10000f;
    [SerializeField] private int maxPoints = 4000;

    private void Start()
    {
        Draw();
    }

    public void Draw()
    {
        TrajectoryCsvLoader loader = GetComponent<TrajectoryCsvLoader>();
        IReadOnlyList<TrajectorySample> samples = loader.Samples;
        LineRenderer line = GetComponent<LineRenderer>();

        if (samples.Count == 0)
        {
            line.positionCount = 0;
            return;
        }

        int step = Mathf.Max(1, Mathf.CeilToInt(samples.Count / (float)maxPoints));
        int count = Mathf.CeilToInt(samples.Count / (float)step);
        line.positionCount = count;

        int lineIndex = 0;
        for (int sampleIndex = 0; sampleIndex < samples.Count; sampleIndex += step)
        {
            line.SetPosition(lineIndex, samples[sampleIndex].OrionPositionKm / kmPerUnityUnit);
            lineIndex++;
        }
    }
}

