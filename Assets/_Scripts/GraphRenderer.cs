using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class GraphRenderer : MonoBehaviour
{
    public bool showGraph = true; // Toggle to show/hide the graph
    public Color graphColor = Color.white; // Color of the graph
    public float graphScale = 1.0f; // Scale of the graph
    public float stepSize = 0.001f; // Step size between points
    public RectTransform graphContainer; // Container for the graph (e.g., a UI Panel)

    private CanvasRenderer canvasRenderer;
    private UILineRenderer lineRenderer;

    private void Awake()
    {
        canvasRenderer = GetComponent<CanvasRenderer>();
        lineRenderer = gameObject.AddComponent<UILineRenderer>();
        lineRenderer.color = graphColor;
    }

    public void PlotGraph<T>(T[] input, int startValue, int maxValue) where T : struct, IComparable
    {
        if (!showGraph || input == null || input.Length == 0 || graphContainer == null)
            return;

        Vector2[] points = new Vector2[maxValue - startValue];

        for (int i = startValue; i < maxValue; i++)
        {
            float yOffset = Convert.ToSingle(input[i]) * graphScale;
            float xPos = i * stepSize;
            float yPos = yOffset;

            // Normalize positions to the container's size
            points[i - startValue] = new Vector2(
                xPos / graphContainer.rect.width,
                yPos / graphContainer.rect.height
            );
        }

        lineRenderer.Points = points;
        lineRenderer.SetVerticesDirty(); // Force the line to update
    }
}