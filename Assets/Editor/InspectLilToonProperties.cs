using UnityEngine;
using UnityEditor;
using System.Text;

public static class InspectLilToonProperties
{
    [MenuItem("Tools/Inspect lilToon Properties")]
    public static void Inspect()
    {
        var s = Shader.Find("lilToon");
        if (s == null)
        {
            Debug.LogError("Shader lilToon not found!");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Shader: {s.name}, PropertyCount: {s.GetPropertyCount()}");
        for (int i = 0; i < s.GetPropertyCount(); i++)
        {
            var pName = s.GetPropertyName(i);
            var pType = s.GetPropertyType(i);
            var pDesc = s.GetPropertyDescription(i);
            sb.AppendLine($"  [{i}] {pName} ({pType}) : {pDesc}");
        }

        System.IO.File.WriteAllText("Assets/Editor/liltoon_properties.txt", sb.ToString());
        Debug.Log("[LILTOON PROPS COMPLETE] Output written to Assets/Editor/liltoon_properties.txt");
    }
}
