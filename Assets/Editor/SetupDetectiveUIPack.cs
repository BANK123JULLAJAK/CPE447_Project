using System.IO;
using UnityEngine;
using UnityEditor;

public static class SetupDetectiveUIPack
{
    [MenuItem("Tools/Setup Detective UI Pack Assets")]
    public static void Run()
    {
        string sourceDir = @"C:\Users\jullb\.gemini\antigravity-ide\brain\8604cfbd-843a-4b11-8b45-11d39f8bd72a";
        string targetDir = @"Assets/Sprites/DetectiveUI";

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string[] sourceFiles = Directory.GetFiles(sourceDir, "detective_*.*");
        foreach (var src in sourceFiles)
        {
            string filename = Path.GetFileName(src);
            string destName = "";
            if (filename.StartsWith("detective_corkboard_bg")) destName = "DetectiveCorkboardBg.jpg";
            else if (filename.StartsWith("detective_case_folder")) destName = "DetectiveCaseFolder.jpg";
            else if (filename.StartsWith("detective_note_card")) destName = "DetectiveNoteCard.jpg";
            else if (filename.StartsWith("detective_red_pin")) destName = "DetectiveRedPin.jpg";

            if (!string.IsNullOrEmpty(destName))
            {
                string targetPath = Path.Combine(targetDir, destName);
                File.Copy(src, targetPath, true);
                Debug.Log($"[COPIED] {destName} -> {targetPath}");
            }
        }

        AssetDatabase.Refresh();

        // Configure texture importers as Sprite (2D and UI)
        string[] importedSprites = {
            "Assets/Sprites/DetectiveUI/DetectiveCorkboardBg.jpg",
            "Assets/Sprites/DetectiveUI/DetectiveCaseFolder.jpg",
            "Assets/Sprites/DetectiveUI/DetectiveNoteCard.jpg",
            "Assets/Sprites/DetectiveUI/DetectiveRedPin.jpg"
        };

        foreach (var path in importedSprites)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                Debug.Log($"[CONFIGURED SPRITE] {path}");
            }
        }

        Debug.Log("<color=green>[SUCCESS] Detective Evidence Board UI Kit assets installed and configured!</color>");
    }
}
