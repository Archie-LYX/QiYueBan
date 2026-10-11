using UnityEngine;
using UnityEngine.UI;

/// <summary>三个大色块构成的平面元宝占位图。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class GoldIngotIconGraphic : MaskableGraphic
{
    public override Texture mainTexture => Texture2D.whiteTexture;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Polygon(vh, new Color(.73f,.58f,.3f), new[] { V(-.48f,.12f),V(-.28f,-.34f),V(.28f,-.34f),V(.48f,.12f),V(0,-.02f) });
        Polygon(vh, new Color(.86f,.73f,.45f), new[] { V(-.48f,.12f),V(-.16f,-.04f),V(.16f,-.04f),V(.48f,.12f),V(.26f,.2f),V(-.26f,.2f) });
        Polygon(vh, new Color(.94f,.83f,.58f), new[] { V(-.24f,.14f),V(0,.38f),V(.24f,.14f),V(0,.02f) });
    }
    private Vector2 V(float x, float y)
    { Rect r = rectTransform.rect; return r.center + new Vector2(x*r.width,y*r.height); }
    private void Polygon(VertexHelper vh, Color c, Vector2[] vertices)
    {
        c.a *= color.a;
        int start = vh.currentVertCount;
        foreach (var p in vertices) vh.AddVert(p, c, Vector2.zero);
        for (int i = 1; i < vertices.Length-1; i++) vh.AddTriangle(start,start+i,start+i+1);
    }
}
