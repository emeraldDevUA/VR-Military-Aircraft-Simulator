using System;
using System.Collections.Generic;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Renders tessellated SVG shapes as one lightweight UGUI mesh.</summary>
[DisallowMultipleComponent]
public sealed class CockpitSvgGraphic : MaskableGraphic
{
    private const int MaximumVertices = 60000;
    private List<VectorUtils.Geometry> geometry;
    private Rect sourceViewport;

    public void SetGeometry(List<VectorUtils.Geometry> svgGeometry, Rect viewport)
    {
        if (svgGeometry == null)
            throw new ArgumentNullException(nameof(svgGeometry));
        if (viewport.width <= 0f || viewport.height <= 0f)
            throw new ArgumentOutOfRangeException(nameof(viewport));

        int vertexCount = 0;
        foreach (VectorUtils.Geometry shape in svgGeometry)
        {
            if (shape.Vertices == null || shape.Indices == null)
                continue;
            vertexCount += shape.Vertices.Length;
            if (vertexCount > MaximumVertices)
                throw new ArgumentException("SVG is too complex for a cockpit display (over 60,000 vertices).");
        }

        geometry = svgGeometry;
        sourceViewport = viewport;
        SetVerticesDirty();
    }

    public void ClearGeometry()
    {
        geometry = null;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (geometry == null || sourceViewport.width <= 0f || sourceViewport.height <= 0f)
            return;

        Rect target = rectTransform.rect;
        float scale = Mathf.Min(target.width / sourceViewport.width, target.height / sourceViewport.height);
        Vector2 renderedSize = new Vector2(sourceViewport.width * scale, sourceViewport.height * scale);
        Vector2 offset = new Vector2(-renderedSize.x * 0.5f, -renderedSize.y * 0.5f);

        foreach (VectorUtils.Geometry shape in geometry)
        {
            Vector2[] vertices = shape.Vertices;
            ushort[] indices = shape.Indices;
            if (vertices == null || indices == null || indices.Length < 3)
                continue;

            int start = vertexHelper.currentVertCount;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 source = vertices[i];
                // SVG uses a top-left origin; world-space UI uses a bottom-left
                // origin. Flip Y while fitting the source viewBox into the slot.
                Vector2 position = new Vector2(
                    (source.x - sourceViewport.xMin) * scale + offset.x,
                    (sourceViewport.yMax - source.y) * scale + offset.y);
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = position;
                vertex.color = shape.Color;
                vertexHelper.AddVert(vertex);
            }

            for (int i = 0; i + 2 < indices.Length; i += 3)
                vertexHelper.AddTriangle(start + indices[i], start + indices[i + 1], start + indices[i + 2]);
        }
    }
}
