using UnityEngine;

[System.Serializable]
public class TrajectorySample
{
    public float TimeSec;
    public string Utc;
    public Vector3 OrionPositionKm;
    public Vector3 OrionVelocityKmS;
    public Vector3? MoonPositionKm;
    public string Phase;
    public string EventName;
    public string Source;
}

