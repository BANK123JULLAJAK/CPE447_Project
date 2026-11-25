using UnityEngine;
using UnityEditor;

public static class SetupPauseMenuInScene
{
    [MenuItem("Tools/Setup Pause Menu In Scene")]
    public static void Setup()
    {
        var existing = Object.FindAnyObjectByType<PauseMenuManager>();
        if (existing == null)
        {
            var go = new GameObject("PauseMenu_System");
            existing = go.AddComponent<PauseMenuManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create PauseMenu_System");
            Debug.Log("[Pause Menu] Created PauseMenu_System in active scene.");
        }
        else
        {
            Debug.Log("[Pause Menu] PauseMenu_System already exists in scene.");
        }

        existing.EnsureUIBuilt();

        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Pause Menu] Scene saved with updated 3D Slanted PauseMenu_System.");
    }
}
