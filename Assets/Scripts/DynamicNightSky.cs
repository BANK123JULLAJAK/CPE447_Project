using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class DynamicNightSky : MonoBehaviour
{
    [Header("Skybox Rotation (การหมุนของท้องฟ้า เมฆ และดวงดาว)")]
    public bool autoRotateSky = true;
    [Range(-5f, 5f)]
    public float rotationSpeed = 0.5f;

    private float currentRotation = 0f;

    private void Start()
    {
        if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Rotation"))
        {
            currentRotation = RenderSettings.skybox.GetFloat("_Rotation");
        }
    }

    private void Update()
    {
        float dt = Application.isPlaying ? Time.deltaTime : 0.02f;

        // Dynamic Skybox Rotation only
        if (autoRotateSky && RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Rotation"))
        {
            currentRotation = (currentRotation + rotationSpeed * dt) % 360f;
            RenderSettings.skybox.SetFloat("_Rotation", currentRotation);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
#endif
    }
}
