using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class UILineRenderer : Graphic
{
    public Vector2[] Points = new Vector2[0];

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (Points == null || Points.Length < 2)
            return;

        for (int i = 0; i < Points.Length - 1; i++)
        {
            Vector2 start = Points[i];
            Vector2 end = Points[i + 1];

            DrawLine(vh, start, end);
        }
    }

    private void DrawLine(VertexHelper vh, Vector2 start, Vector2 end)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        Vector2 direction = (end - start).normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x) * 2f; // Line thickness

        // Create vertices for the line
        vertex.position = start - perpendicular;
        vh.AddVert(vertex);

        vertex.position = start + perpendicular;
        vh.AddVert(vertex);

        vertex.position = end - perpendicular;
        vh.AddVert(vertex);

        vertex.position = end + perpendicular;
        vh.AddVert(vertex);

        // Create triangles
        int index = vh.currentVertCount - 4;
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index + 2, index + 1, index + 3);
    }
}