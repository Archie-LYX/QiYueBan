using UnityEngine;
using UnityEngine.UI;

// 由 GoldPaperFold 自动生成，无需手动挂载。MaskableGraphic 支持 Canvas / Mask。
public class GoldPaperFoldLayer : MaskableGraphic
{
    [SerializeField, HideInInspector] private GoldPaperFold owner;
    [SerializeField, HideInInspector] private int part;
    public GoldPaperFold Owner { get => owner; set => owner = value; }
    public int Part { get => part; set => part = value; }
    public override Texture mainTexture => Owner != null ? Owner.TextureForPart(Part) : Texture2D.whiteTexture;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        if (Owner == null) vh.Clear();
        else Owner.PopulateLayer(vh, Part, rectTransform.rect);
    }
}

