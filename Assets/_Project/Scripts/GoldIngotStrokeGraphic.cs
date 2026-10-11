using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>轻量 2D 圆头折线，仅绘制提示或玩家轨迹，没有折纸物理。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class GoldIngotStrokeGraphic : MaskableGraphic
{
    public override Texture mainTexture => Texture2D.whiteTexture;
    public readonly List<Vector2> Points = new List<Vector2>();
    public float Width = 6f;
    public bool ShowPoints;
    public void SetPoints(IEnumerable<Vector2> points)
    { Points.Clear(); Points.AddRange(points); SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        for (int i = 0; i < Points.Count; i++)
        {
            Vector2 p = new Vector2(r.xMin + Points[i].x * r.width, r.yMin + Points[i].y * r.height);
            if (i > 0)
            {
                Vector2 last = new Vector2(r.xMin + Points[i-1].x * r.width, r.yMin + Points[i-1].y * r.height);
                Vector2 n = new Vector2(-(p-last).y, (p-last).x).normalized * Width * .5f;
                int start = vh.currentVertCount;
                vh.AddVert(last-n, color, Vector2.zero); vh.AddVert(p-n, color, Vector2.zero);
                vh.AddVert(p+n, color, Vector2.zero); vh.AddVert(last+n, color, Vector2.zero);
                vh.AddTriangle(start, start+1, start+2); vh.AddTriangle(start+2, start+3, start);
            }
            Circle(vh, p, ShowPoints ? Width * 1.05f : Width * .5f, color);
        }
    }
    public static void Circle(VertexHelper vh, Vector2 center, float radius, Color c)
    {
        int start = vh.currentVertCount;
        vh.AddVert(center, c, Vector2.zero);
        const int segments = 12;
        for (int i = 0; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2 / segments;
            vh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, c, Vector2.zero);
            if (i > 0) vh.AddTriangle(start, start+i, start+i+1);
        }
    }
}
