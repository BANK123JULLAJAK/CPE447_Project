using UnityEngine;
using UnityEditor;

public static class InspectUndergroundPlatform
{
    [MenuItem("Tools/Inspect Underground Platform")]
    public static void Run()
    {
        var sb = new System.Text.StringBuilder();
        if (SceneView.lastActiveSceneView != null)
        {
            var cam = SceneView.lastActiveSceneView.camera;
            sb.AppendLine($"[SCENEVIEW CAM] Pos: {cam.transform.position}, Rot: {cam.transform.rotation.eulerAngles}");
        }

        if (Selection.activeGameObject != null)
        {
            sb.AppendLine($"[SELECTED] {Selection.activeGameObject.name}, Pos: {Selection.activeGameObject.transform.position}");
        }

        string[] names = {
            "TRAIN_STATION",
            "TRAIN_STATION_RAILWAY",
            "TRAIN_EVIDENCE_BLOODSPLATTER",
            "TRAIN_EVIDENCE_BLOODSPLATTER.001",
            "TRAIN_EVIDENCE_BLOODSPLATTER.002",
            "TRAIN_EVIDENCE_SPENTCARTRIDGECASE",
            "TRAIN_EVIDENCE_POCKETWATCH",
            "TRAIN_EVIDENCE_RAILWAYTICKET",
            "TRAIN_EVIDENCE_STATIONNOTE"
        };

        foreach (var n in names)
        {
            var go = GameObject.Find(n);
            if (go != null)
            {
                sb.AppendLine($"[OBJ] {n} -> Pos: {go.transform.position}");
            }
        }

        System.IO.File.WriteAllText("Assets/Editor/underground_inspect.txt", sb.ToString());
        Debug.Log("[SAVED] Assets/Editor/underground_inspect.txt");
    }
}
