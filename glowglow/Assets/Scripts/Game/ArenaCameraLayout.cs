using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public sealed class ArenaCameraLayout : MonoBehaviour
{
    public bool TrainingLayout { get; set; }
    private void OnEnable() => Apply();
    private void LateUpdate() => Apply();

    public void Apply()
    {
        var camera = GetComponent<Camera>();
        // Clear the entire frame, including the HUD bands, on every scene transition.
        camera.rect = new Rect(0, 0, 1, 1);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.025f, .008f, .055f, 1);
        camera.orthographic = true;
        if (TrainingLayout)
        {
            float size = Mathf.Max(4.7f / .73f, 8.9f / Mathf.Max(.1f, camera.aspect * .65f));
            camera.orthographicSize = size;
            camera.transform.position = new Vector3(.31f * size * camera.aspect, 0, -10);
            return;
        }
        // Fit the arena between the compact bottom dock and the life/timer header.
        const float bottom = .195f;
        const float top = .925f;
        float halfHeight = Mathf.Max(4.7f / (top - bottom), 8.9f / Mathf.Max(.1f, camera.aspect * .95f));
        camera.orthographicSize = halfHeight;
        camera.transform.position = new Vector3(0, -(bottom + top - 1) * halfHeight, -10);
    }
}
