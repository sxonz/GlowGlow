using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Effects/Horizontal Gradient")]
public sealed class UIHorizontalGradient : BaseMeshEffect
{
    [SerializeField] private Color leftColor = new Color(0.02f, 0.005f, 0.07f, 0.76f);
    [SerializeField] private Color rightColor = new Color(0.02f, 0.005f, 0.07f, 0f);

    public override void ModifyMesh(VertexHelper vertexHelper)
    {
        if (!IsActive() || vertexHelper.currentVertCount == 0) return;

        var rect = graphic.rectTransform.rect;
        float width = Mathf.Max(1f, rect.width);
        var vertex = new UIVertex();

        for (int i = 0; i < vertexHelper.currentVertCount; i++)
        {
            vertexHelper.PopulateUIVertex(ref vertex, i);
            float t = Mathf.Clamp01((vertex.position.x - rect.xMin) / width);
            vertex.color = Color.Lerp(leftColor, rightColor, Mathf.SmoothStep(0f, 1f, t));
            vertexHelper.SetUIVertex(vertex, i);
        }
    }
}
