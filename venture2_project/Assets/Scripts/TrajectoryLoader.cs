using System.Collections.Generic;
using UnityEngine;

public class TrajectoryLoader : MonoBehaviour
{
    [Header("CSV")]
    public TextAsset csvFile;

    [Header("Scale")]
    public float scaleFactor = 1000000f;

    [Header("Playback")]
    public float speed = 300f;

    [Header("Orbit")]
    public bool showOrbitLine = true;

    private List<Vector3> trajectoryPoints = new List<Vector3>();

    private int currentIndex = 0;

    private float timer = 0f;

    public float orbitAmplifier = 20f;

    private LineRenderer lineRenderer;

    void Start()
    {
        if(csvFile == null)
        {
            Debug.LogError("CSVファイルが未設定");
            return;
        }

        LoadCSV();

        if(showOrbitLine)
        {
            SetupLineRenderer();
        }

        Debug.Log("TrajectoryLoader 起動完了");
    }

    void Update()
    {
        timer += Time.deltaTime;

        if(timer >= 1f / speed)
        {
            MoveOrion();
            timer = 0f;
        }
    }

    void LoadCSV()
    {
        string[] lines = csvFile.text.Split('\n');

        for(int i = 1; i < lines.Length; i++)
        {
            if(string.IsNullOrWhiteSpace(lines[i]))
                continue;

            string[] values = lines[i].Split(',');

            if(values.Length < 4)
                continue;

            float x = float.Parse(values[1]) / scaleFactor;
            float y = float.Parse(values[2]) / scaleFactor;
            float z = float.Parse(values[3]) / scaleFactor;

            Vector3 pos = new Vector3(x, y, z) * orbitAmplifier;
            trajectoryPoints.Add(pos);
        }

        Debug.Log("CSV読み込み完了");
        Debug.Log("軌道点数: " + trajectoryPoints.Count);
    }

    void MoveOrion()
    {
        if(currentIndex >= trajectoryPoints.Count)
            return;

        transform.position = trajectoryPoints[currentIndex];

        currentIndex++;
    }

    void SetupLineRenderer()
    {
        lineRenderer = GetComponent<LineRenderer>();

        if(lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.positionCount = trajectoryPoints.Count;

        lineRenderer.SetPositions(trajectoryPoints.ToArray());

        lineRenderer.startWidth = 0.3f;
        lineRenderer.endWidth = 0.3f;

        lineRenderer.useWorldSpace = true;

        lineRenderer.material =
            new Material(Shader.Find("Sprites/Default"));

        lineRenderer.startColor = Color.cyan;
        lineRenderer.endColor = Color.cyan;
    }
}