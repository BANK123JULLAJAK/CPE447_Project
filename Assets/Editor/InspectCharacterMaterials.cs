using UnityEngine;
using UnityEditor;
using System.Text;

public static class InspectCharacterMaterials
{
    [MenuItem("Tools/Inspect Character Materials")]
    public static void Inspect()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== INSPECT CHARACTER MATERIALS IN SCENE ===");

        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        foreach (var r in renderers)
        {
            var root = r.transform.root;
            if (root.name.Contains("Newman") || root.name.Contains("Jack") || root.name.Contains("Player") || root.name.Contains("Armature"))
            {
                sb.AppendLine($"Renderer: {r.gameObject.name} (Parent: {r.transform.parent?.name}, Root: {root.name})");
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m != null)
                    {
                        sb.AppendLine($"  [{i}] Mat: '{m.name}', Shader: '{m.shader.name}', AssetPath: '{AssetDatabase.GetAssetPath(m)}'");
                        // Check textures
                        var texProps = m.GetTexturePropertyNames();
                        foreach (var tp in texProps)
                        {
                            var t = m.GetTexture(tp);
                            if (t != null)
                            {
                                sb.AppendLine($"      Tex '{tp}': {t.name}");
                            }
                        }
                    }
                    else
                    {
                        sb.AppendLine($"  [{i}] Mat: NULL!");
                    }
                }
            }
        }

        sb.AppendLine("\n=== ALL SHADERS IN PROJECT (Matching MToon / Toon / UTS) ===");
        var shaderInfos = ShaderUtil.GetAllShaderInfo();
        foreach (var s in shaderInfos)
        {
            if (s.name.ToLower().Contains("toon") || s.name.ToLower().Contains("mtoon") || s.name.ToLower().Contains("vrm") || s.name.ToLower().Contains("uts"))
            {
                sb.AppendLine($"Shader: '{s.name}' (supported={s.supported}, hasErrors={s.hasErrors})");
            }
        }

        System.IO.File.WriteAllText("Assets/Editor/character_materials_inspect.txt", sb.ToString());
        Debug.Log("[CHARACTER INSPECT COMPLETE] Output written to Assets/Editor/character_materials_inspect.txt");
    }
}
