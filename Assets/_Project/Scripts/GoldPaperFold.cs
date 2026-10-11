using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 第一步：下半张纸沿水平中线向上翻折。输入、折叠状态和网格渲染独立。
/// 使用虚拟三维旋转和透视投影；不改变 RectTransform 的宽高、位置或缩放。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class GoldPaperFold : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("纸张正反面（未设置贴图时使用纯色）")]
    public Texture frontTexture;
    public Texture backTexture;
    public Color frontColor = new Color(1f, 0.72f, 0.18f, 1f);
    public Color backColor = new Color(0.92f, 0.85f, 0.59f, 1f);
    [Header("第一次折叠")]
    [Tooltip("下沿抓取区域，占纸张高度的比例")]
    [Range(0.05f, 0.4f)] public float grabStrip = 0.22f;
    [Tooltip("达到此角度后松手，才自动完成折叠")]
    [Range(95f, 175f)] public float completeAngle = 135f;
    [Min(0.05f)] public float settleDuration = 0.3f;
    [Tooltip("虚拟观察距离 / 纸张高度，只影响纸张网格透视")]
    [Range(2f, 10f)] public float perspectiveDistance = 3f;
    [Range(8, 48)] public int meshStrips = 24;
    [Header("后续步骤接口")]
    public UnityEvent<float> onFoldProgress = new UnityEvent<float>();
    public UnityEvent onFirstFoldCompleted = new UnityEvent();

    public float FoldAngle { get; private set; }
    public bool IsFolded { get; private set; }
    public bool IsDragging => dragging;

    private RectTransform paper;
    [SerializeField, HideInInspector] private Graphic originalGraphic;
    [SerializeField, HideInInspector] private bool originalEnabled;
    [SerializeField, HideInInspector] private GoldPaperFoldLayer[] layers;
    private Coroutine settling;
    private bool dragging;
    private int pointerId;
    private Vector2 pressLocal;
    private Camera pressCamera;

    private void OnEnable() { InitializeSurface(); }

    public void InitializeSurface()
    {
        paper = (RectTransform)transform;
        if (layers == null || layers.Length != 4)
        {
            originalGraphic = GetComponent<Graphic>();
            if (originalGraphic != null) originalEnabled = originalGraphic.enabled;
            layers = new GoldPaperFoldLayer[4];
            string[] names = { "FixedPaper", "OverlapShadow", "TurningPaper", "CreaseAndEdge" };
            for (int i = 0; i < layers.Length; i++)
            {
                var go = new GameObject(names[i], typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(GoldPaperFoldLayer));
                go.layer = gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                rt.pivot = paper.pivot;
                layers[i] = go.GetComponent<GoldPaperFoldLayer>();
                layers[i].Owner = this;
                layers[i].Part = i;
                // 固定输入区域不会随折叠网格收缩；只有从下沿抓取才开始折叠。
                layers[i].raycastTarget = i == 0;
            }
        }
        if (originalGraphic != null) originalGraphic.enabled = false;
        foreach (var layer in layers) layer.gameObject.SetActive(true);
        RefreshSurface();
    }

    private void OnDisable()
    {
        if (settling != null) StopCoroutine(settling);
        settling = null;
        dragging = false;
        if (!IsFolded) FoldAngle = 0f;
        if (layers != null)
            foreach (var layer in layers)
                if (layer != null) layer.gameObject.SetActive(false);
        if (originalGraphic != null) originalGraphic.enabled = originalEnabled;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (!isActiveAndEnabled || IsFolded || dragging || settling != null ||
            e.button != PointerEventData.InputButton.Left) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(paper,
            e.pressPosition, e.pressEventCamera, out Vector2 p)) return;
        Rect r = paper.rect;
        if (!r.Contains(p) || p.y > r.yMin + r.height * grabStrip) return;
        pressLocal = p;
        pressCamera = e.pressEventCamera;
        pointerId = e.pointerId;
        dragging = true;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!dragging || e.pointerId != pointerId) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(paper,
            e.position, pressCamera, out Vector2 p)) return;
        // 下沿从 -h/2 移到 +h/2，用余弦反解旋转角度；横向拖动不推进。
        float travel = Mathf.Clamp01((p.y - pressLocal.y) / Mathf.Max(1f, paper.rect.height));
        SetAngle(Mathf.Acos(1f - 2f * travel) * Mathf.Rad2Deg);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!dragging || e.pointerId != pointerId) return;
        dragging = false;
        settling = StartCoroutine(Settle(FoldAngle >= completeAngle ? 180f : 0f));
    }

    private IEnumerator Settle(float target)
    {
        float from = FoldAngle;
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, settleDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAngle(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsed / duration))));
            yield return null;
        }
        SetAngle(target);
        settling = null;
        if (target == 180f)
        {
            IsFolded = true;
            onFirstFoldCompleted.Invoke();
        }
    }

    [ContextMenu("重置第一次折叠")]
    public void ResetFold()
    {
        if (settling != null) StopCoroutine(settling);
        settling = null;
        dragging = false;
        IsFolded = false;
        SetAngle(0f);
    }

    private void OnApplicationFocus(bool focus) { if (!focus && dragging) ResetFold(); }
    private void OnApplicationPause(bool paused) { if (paused && dragging) ResetFold(); }
    private void OnValidate() { RefreshSurface(); }
    private void SetAngle(float angle)
    {
        FoldAngle = Mathf.Clamp(angle, 0f, 180f);
        RefreshSurface();
        onFoldProgress.Invoke(FoldAngle / 180f);
    }

    public void RefreshSurface()
    {
        if (layers == null) return;
        foreach (var layer in layers)
        {
            if (layer == null) continue;
            layer.rectTransform.pivot = ((RectTransform)transform).pivot;
            layer.SetAllDirty(); // 翻面时也刷新材质，使背面贴图及时切换。
        }
    }

    /// <summary>下半张纸绕中线旋转：y=-d*cos(a), z=d*sin(a)，再透视投影。</summary>
    public Vector2 ProjectFoldPoint(Rect r, float x, float distanceFromCrease, float angle)
    {
        float rad = angle * Mathf.Deg2Rad;
        float z = distanceFromCrease * Mathf.Sin(rad);
        float viewDistance = Mathf.Max(2f, perspectiveDistance) * r.height;
        float scale = viewDistance / Mathf.Max(1f, viewDistance - z);
        return r.center + new Vector2(x * scale, -distanceFromCrease * Mathf.Cos(rad) * scale);
    }

    public Texture TextureForPart(int part)
    {
        Texture tex = part == 0 ? frontTexture :
            part == 2 ? (FoldAngle <= 90f ? frontTexture : backTexture) : null;
        return tex != null ? tex : Texture2D.whiteTexture;
    }

    public void PopulateLayer(VertexHelper vh, int part, Rect r)
    {
        vh.Clear();
        if (r.width <= 0 || r.height <= 0) return;
        if (part == 0)
        {
            // 固定上半张纸使用原贴图上半部分 UV。
            Quad(vh, new Vector2(r.xMin, r.center.y), new Vector2(r.xMax, r.center.y),
                new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax),
                frontColor, new Vector2(0, 0.5f), Vector2.one);
        }
        else if (part == 1 && FoldAngle > 90f)
        {
            float shadowHeight = ProjectFoldPoint(r, 0f, r.height * 0.5f, FoldAngle).y - r.center.y;
            float spreadMax = Mathf.Lerp(r.height * 0.035f, 1.5f,
                Mathf.InverseLerp(90f, 180f, FoldAngle));
            for (int i = 5; i >= 1; i--)
            {
                float s = spreadMax * i / 5f;
                // 限于固定纸面范围；阴影仅在上半张纸内，不能飘出纸张轮廓。
                float top = Mathf.Min(r.yMax, r.center.y + shadowHeight + s);
                Quad(vh, new Vector2(r.xMin, r.center.y), new Vector2(r.xMax, r.center.y),
                    new Vector2(r.xMax, top), new Vector2(r.xMin, top),
                    new Color(0.12f, 0.055f, 0.015f, 0.06f), Vector2.zero, Vector2.zero);
            }
        }
        else if (part == 2)
        {
            Color face = FoldAngle <= 90f ? frontColor : backColor;
            float light = Mathf.Lerp(1f, 0.58f, Mathf.Sin(FoldAngle * Mathf.Deg2Rad));
            int strips = Mathf.Clamp(meshStrips, 8, 48);
            for (int i = 0; i < strips; i++)
            {
                float t0 = (float)i / strips;
                float t1 = (float)(i + 1) / strips;
                Color c = face * (light * Mathf.Lerp(0.97f, 1f, t0));
                c.a = face.a;
                Quad(vh, ProjectFoldPoint(r, -r.width * 0.5f, t0 * r.height * 0.5f, FoldAngle),
                    ProjectFoldPoint(r, r.width * 0.5f, t0 * r.height * 0.5f, FoldAngle),
                    ProjectFoldPoint(r, r.width * 0.5f, t1 * r.height * 0.5f, FoldAngle),
                    ProjectFoldPoint(r, -r.width * 0.5f, t1 * r.height * 0.5f, FoldAngle), c,
                    new Vector2(0, 0.5f - t0 * 0.5f), new Vector2(1, 0.5f - t1 * 0.5f));
            }
        }
        else if (part == 3)
        {
            float thickness = Mathf.Max(0.7f, r.height * 0.004f);
            float opacity = Mathf.Lerp(0.1f, 0.5f, FoldAngle / 180f);
            Line(vh, new Vector2(r.xMin, r.center.y), new Vector2(r.xMax, r.center.y),
                thickness, new Color(0.26f, 0.13f, 0.02f, opacity));
            if (FoldAngle > 0.1f)
            {
                Vector2 left = ProjectFoldPoint(r, -r.width * 0.5f, r.height * 0.5f, FoldAngle);
                Vector2 right = ProjectFoldPoint(r, r.width * 0.5f, r.height * 0.5f, FoldAngle);
                Line(vh, left, right, Mathf.Max(0.65f, r.height * 0.003f),
                    new Color(1f, 0.92f, 0.65f, 1f));
                // 深色厚度线与亮色纸沿一起区分最终重叠的两层。
                if (FoldAngle > 150f)
                    Line(vh, left + Vector2.up * thickness, right + Vector2.up * thickness,
                        thickness * 0.65f, new Color(0.32f, 0.18f, 0.04f, 0.6f));
            }
        }
    }

    private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
        Quad(vh, a - n, b - n, b + n, a + n, color, Vector2.zero, Vector2.zero);
    }

    private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
        Color color, Vector2 uvMin, Vector2 uvMax)
    {
        int start = vh.currentVertCount;
        vh.AddVert(a, color, new Vector2(uvMin.x, uvMin.y));
        vh.AddVert(b, color, new Vector2(uvMax.x, uvMin.y));
        vh.AddVert(c, color, new Vector2(uvMax.x, uvMax.y));
        vh.AddVert(d, color, new Vector2(uvMin.x, uvMax.y));
        // UI/Default 的 Cull Off 支持翻面；正背贴图由当前角度决定。
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}


