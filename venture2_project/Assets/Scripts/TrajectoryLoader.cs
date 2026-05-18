using System.Collections.Generic;
using UnityEngine;

public class TrajectoryLoader : MonoBehaviour
{
   public TextAsset csvFile;

   private List<Vector3> trajectoryPoints = new List<Vector3>();

   private int currentIndex = 0;

   public float scaleFactor = 1000000f;

   void Start()
   {
       LoadCSV();
   }

   void Update()
   {
       MoveOrion();
   }

   void LoadCSV()
   {
       string[] lines = csvFile.text.Split('\n');

       for(int i = 1; i < lines.Length; i++)
       {
           if(string.IsNullOrWhiteSpace(lines[i]))
               continue;

           string[] values = lines[i].Split(',');

           float x = float.Parse(values[1]) / scaleFactor;
           float y = float.Parse(values[2]) / scaleFactor;
           float z = float.Parse(values[3]) / scaleFactor;

           trajectoryPoints.Add(new Vector3(x, y, z));
       }

       Debug.Log("読み込み完了: " + trajectoryPoints.Count);
   }

   void MoveOrion()
   {
       if(currentIndex >= trajectoryPoints.Count)
           return;

       transform.position = trajectoryPoints[currentIndex];

       currentIndex++;
   }
}
