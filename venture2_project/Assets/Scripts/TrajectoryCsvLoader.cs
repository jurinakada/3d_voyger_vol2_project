using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class TrajectoryCsvLoader : MonoBehaviour
{
    [SerializeField] private TextAsset csvFile;
    [SerializeField] private string resourcesPath = "CSV/artemis2_trajectory";
    [SerializeField] private bool loadOnAwake = true;

    public IReadOnlyList<TrajectorySample> Samples => samples;

    private readonly List<TrajectorySample> samples = new List<TrajectorySample>();

    private void Awake()
    {
        if (loadOnAwake)
        {
            Load();
        }
    }

    public void Load()
    {
        samples.Clear();

        TextAsset source = csvFile;
        if (source == null && !string.IsNullOrWhiteSpace(resourcesPath))
        {
            source = Resources.Load<TextAsset>(resourcesPath);
        }

        if (source == null)
        {
            Debug.LogError("TrajectoryCsvLoader: CSV file is not assigned and Resources load failed.");
            return;
        }

        string[] lines = source.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            string[] values = lines[i].Trim().Split(',');
            if (values.Length < 14)
            {
                Debug.LogWarning($"TrajectoryCsvLoader: skipped malformed row {i + 1}");
                continue;
            }

            samples.Add(new TrajectorySample
            {
                TimeSec = ParseFloat(values[0]),
                Utc = values[1],
                OrionPositionKm = new Vector3(ParseFloat(values[2]), ParseFloat(values[3]), ParseFloat(values[4])),
                OrionVelocityKmS = new Vector3(ParseFloat(values[5]), ParseFloat(values[6]), ParseFloat(values[7])),
                MoonPositionKm = ParseOptionalVector(values[8], values[9], values[10]),
                Phase = values[11],
                EventName = values[12],
                Source = values[13],
            });
        }

        Debug.Log($"TrajectoryCsvLoader: loaded {samples.Count} samples from {source.name}");
    }

    private static Vector3? ParseOptionalVector(string x, string y, string z)
    {
        if (string.IsNullOrWhiteSpace(x) || string.IsNullOrWhiteSpace(y) || string.IsNullOrWhiteSpace(z))
        {
            return null;
        }

        return new Vector3(ParseFloat(x), ParseFloat(y), ParseFloat(z));
    }

    private static float ParseFloat(string value)
    {
        return float.Parse(value, CultureInfo.InvariantCulture);
    }
}

