using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 万贯 V0.4 独立试玩：八方向移动、单击落纸、矩形重叠裁切。
/// 只使用 Unity UGUI；不改动剧情或其他小游戏。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class WanGuanEightDirectionGame : MonoBehaviour, IPointerClickHandler
{
    [Header("V0.4 核心规则")]
    [Range(1, 16)] public int targetLayers = 8;
    [Range(0.1f, 1f)] public float minimumAreaRatio = 0.30f;
    [Range(0.01f, 0.2f)] public float perfectToleranceRatio = 0.06f;

    [Header("移动速度（Canvas 参考分辨率单位/秒）")]
    [Min(10)] public float initialSpeed = 220f;
    [Min(0)] public float speedPerLayer = 14f;
    [Range(0.3f, 1f)] public float diagonalSpeedFactor = 0.75f;
    [Min(100)] public float moveRange = 300f;

    [Header("纸张与界面")]
    public Vector2 initialPaperSize = new Vector2(300f, 190f);
    public Font uiFont;
    public UnityEvent onBundleCompleted = new UnityEvent();

    public int TotalBundles { get; private set; }
    public int PlacedLayers { get; private set; }
    public int PerfectCount { get; private set; }
    public float RemainingAreaRatio => topRect.width * topRect.height / Mathf.Max(1f, initialArea);
    public bool IsPlaying { get; private set; }
    public Vector2 IncomingDirection => direction;

    private RectTransform board;
    private RectTransform movingPaper;
    private Text status;
    private Text feedback;
    private Text counter;
    private Button retry;
    private readonly List<RectTransform> placed = new List<RectTransform>();
    private readonly System.Random random = new System.Random();
    private Rect topRect;
    private float initialArea;
    private float movementOffset;
    private int movementSign;
    private Vector2 direction;
    private float speed;
    private bool initialized;

    private static readonly Color Background = new Color(.95f, .94f, .91f);
    private static readonly Color Paper = new Color(.85f, .69f, .38f);
    private static readonly Color PaperLight = new Color(.93f, .80f, .52f);
    private static readonly Color Ink = new Color(.27f, .32f, .32f);
    private static readonly Color Accent = new Color(.70f, .39f, .35f);

    private void Start()
    {
        Initialize();
        Restart();
    }

    private void Initialize()
    {
        if (initialized) return;
        if (GetComponentInParent<Canvas>() == null)
        {
            Debug.LogError("WanGuanEightDirectionGame needs a Canvas parent.", this);
            enabled = false;
            return;
        }

        var image = GetComponent<Image>();
        image.color = Background;
        image.raycastTarget = true;

        if (uiFont == null)
        {
            uiFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
            if (uiFont == null)
                uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        Label("Title", transform, new Vector2(-365, 300), new Vector2(490, 55),
            "七月半 · 叠万贯", 30, TextAnchor.MiddleLeft);
        counter = Label("Counter", transform, new Vector2(340, 300),
            new Vector2(400, 45), "", 20, TextAnchor.MiddleRight);
        Label("Hint", transform, new Vector2(0, 251), new Vector2(900, 35),
            "观察来纸方向，点击桌面叠放。只保留重叠部分。", 17, TextAnchor.MiddleCenter);

        board = CreateRect("WanGuanBoard", transform, new Vector2(0, -6),
            new Vector2(900, 460));
        var boardImage = board.gameObject.AddComponent<Image>();
        boardImage.color = new Color(.89f, .87f, .82f);
        boardImage.raycastTarget = false;

        // 上层 MovingPaper 永远在已落下的纸层之上。
        movingPaper = CreatePaper("MovingPaper", board, PaperLight);
        status = Label("Status", transform, new Vector2(-75, -280),
            new Vector2(790, 40), "", 20, TextAnchor.MiddleCenter);
        feedback = Label("Feedback", transform, new Vector2(-80, 210),
            new Vector2(550, 42), "", 22, TextAnchor.MiddleCenter);
        retry = CreateButton("RestartButton", transform, new Vector2(455, -293),
            new Vector2(180, 50), "重新开始");
        retry.onClick.AddListener(Restart);
        initialized = true;
    }

    public void Restart()
    {
        if (!initialized) return;
        foreach (RectTransform item in placed)
            if (item != null) Destroy(item.gameObject);
        placed.Clear();

        Vector2 size = new Vector2(Mathf.Max(50, initialPaperSize.x),
            Mathf.Max(50, initialPaperSize.y));
        initialArea = size.x * size.y;
        topRect = new Rect(-size.x * .5f, -size.y * .5f, size.x, size.y);
        PlacedLayers = 0;
        PerfectCount = 0;
        IsPlaying = true;
        feedback.text = "";
        CreatePlacedPaper(topRect);
        SpawnIncomingPaper();
    }

    private void Update()
    {
        if (!IsPlaying || movingPaper == null) return;
        movementOffset += movementSign * speed * Time.deltaTime;
        if (movementOffset >= moveRange)
        {
            movementOffset = moveRange;
            movementSign = -1;
        }
        else if (movementOffset <= -moveRange)
        {
            movementOffset = -moveRange;
            movementSign = 1;
        }
        UpdateMovingPaper();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsPlaying || eventData.button != PointerEventData.InputButton.Left)
            return;
        DropPaper();
    }

    private void SpawnIncomingPaper()
    {
        int nextLayer = PlacedLayers + 1;
        direction = DirectionForLayer(nextLayer, random);
        movementOffset = -moveRange;
        movementSign = 1;
        speed = (initialSpeed + (nextLayer - 1) * speedPerLayer)
            * (Mathf.Abs(direction.x) > 0.001f && Mathf.Abs(direction.y) > 0.001f
                ? diagonalSpeedFactor : 1f);
        movingPaper.gameObject.SetActive(true);
        movingPaper.SetAsLastSibling();
        UpdateMovingPaper();
        UpdateStatus("第 " + nextLayer + " 层 / " + targetLayers + "  ·  来纸 " +
            DirectionName(direction));
    }

    private void UpdateMovingPaper()
    {
        Vector2 center = topRect.center + direction * movementOffset;
        SetPaperRect(movingPaper, new Rect(center - topRect.size * .5f, topRect.size));
    }

    private void DropPaper()
    {
        // 对角线并不是两次独立操作：整张纸在一条固定斜线上运动。
        Vector2 center = topRect.center + direction * movementOffset;
        Rect incoming = new Rect(center - topRect.size * .5f, topRect.size);
        Vector2 offset = incoming.center - topRect.center;

        float thresholdX = Mathf.Max(6f, topRect.width * perfectToleranceRatio);
        float thresholdY = Mathf.Max(6f, topRect.height * perfectToleranceRatio);
        bool perfect = Mathf.Abs(offset.x) <= thresholdX
            && Mathf.Abs(offset.y) <= thresholdY;

        Rect kept = perfect ? topRect : CalculateOverlap(topRect, incoming);
        if (kept.width <= 0f || kept.height <= 0f)
        {
            EndRun(false, "完全没有重叠，纸张掉落了。");
            return;
        }

        float ratio = kept.width * kept.height / initialArea;
        if (ratio < minimumAreaRatio)
        {
            EndRun(false, "剩余面积不足 " +
                Mathf.RoundToInt(minimumAreaRatio * 100) + "%，本次失败。");
            return;
        }

        topRect = kept;
        PlacedLayers++;
        if (perfect) PerfectCount++;
        CreatePlacedPaper(topRect);
        feedback.text = perfect ? "PERFECT · 完美对齐！" : "已落纸 · 剩余面积 " +
            Mathf.RoundToInt(ratio * 100f) + "%";

        if (PlacedLayers >= targetLayers)
        {
            EndRun(true, "完成一份万贯！");
            return;
        }
        SpawnIncomingPaper();
    }

    private void EndRun(bool won, string message)
    {
        if (!IsPlaying) return; // 防止重复结算
        IsPlaying = false;
        movingPaper.gameObject.SetActive(false);

        if (won)
        {
            TotalBundles++;
            onBundleCompleted.Invoke();
        }

        UpdateStatus(message + "  ·  Perfect " + PerfectCount +
            " 次  ·  面积 " + Mathf.RoundToInt(RemainingAreaRatio * 100f) + "%");
        feedback.text = won ? "万贯 +1 扎  ·  点击「重新开始」再做一份" :
            "挑战失败  ·  点击「重新开始」立即重试";
    }

    private void UpdateStatus(string message)
    {
        status.text = message;
        counter.text = "已叠 " + PlacedLayers + "/" + targetLayers +
            "  ·  成品 " + TotalBundles + " 扎";
    }

    /// <summary>与本次移动方向无关，永远计算两个轴对齐矩形的真实交集。</summary>
    public static Rect CalculateOverlap(Rect lower, Rect upper)
    {
        float left = Mathf.Max(lower.xMin, upper.xMin);
        float right = Mathf.Min(lower.xMax, upper.xMax);
        float bottom = Mathf.Max(lower.yMin, upper.yMin);
        float top = Mathf.Min(lower.yMax, upper.yMax);
        if (right <= left || top <= bottom) return new Rect(0, 0, 0, 0);
        return Rect.MinMaxRect(left, bottom, right, top);
    }

    public static Vector2 DirectionForLayer(int layer, System.Random random)
    {
        // 1-4 层确保先学会两个正交轴；第 5 层才引入对角线。
        switch (layer)
        {
            case 1: return Vector2.right;
            case 2: return Vector2.down;
            case 3: return Vector2.left;
            case 4: return Vector2.up;
            case 5: return new Vector2(1, -1).normalized;
            case 6: return Vector2.left;
            case 7: return new Vector2(-1, 1).normalized;
            default:
                Vector2[] all = {
                    Vector2.right, Vector2.down, Vector2.left, Vector2.up,
                    new Vector2(1,-1).normalized, new Vector2(-1,1).normalized,
                    new Vector2(-1,-1).normalized, new Vector2(1,1).normalized
                };
                return all[random.Next(all.Length)];
        }
    }

    private static string DirectionName(Vector2 d)
    {
        int x = d.x > .1f ? 1 : d.x < -.1f ? -1 : 0;
        int y = d.y > .1f ? 1 : d.y < -.1f ? -1 : 0;
        if (x == 1 && y == 0) return "→";
        if (x == -1 && y == 0) return "←";
        if (x == 0 && y == 1) return "↑";
        if (x == 0 && y == -1) return "↓";
        if (x == 1 && y == 1) return "↗";
        if (x == 1 && y == -1) return "↘";
        if (x == -1 && y == 1) return "↖";
        return "↙";
    }

    private void CreatePlacedPaper(Rect rect)
    {
        RectTransform piece = CreatePaper("StackLayer" + placed.Count, board,
            placed.Count % 2 == 0 ? Paper : PaperLight);
        SetPaperRect(piece, rect);
        placed.Add(piece);
        movingPaper.SetAsLastSibling();
    }

    private static void SetPaperRect(RectTransform paper, Rect rect)
    {
        paper.anchoredPosition = rect.center;
        paper.sizeDelta = rect.size;
    }

    private static RectTransform CreateRect(string name, Transform parent,
        Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    private static RectTransform CreatePaper(string name, Transform parent, Color color)
    {
        RectTransform rt = CreateRect(name, parent, Vector2.zero, Vector2.zero);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rt;
    }

    private Text Label(string name, Transform parent, Vector2 position, Vector2 size,
        string message, int fontSize, TextAnchor alignment)
    {
        RectTransform rt = CreateRect(name, parent, position, size);
        Text label = rt.gameObject.AddComponent<Text>();
        label.font = uiFont;
        label.fontSize = fontSize;
        label.color = Ink;
        label.alignment = alignment;
        label.text = message;
        label.raycastTarget = false;
        return label;
    }

    private Button CreateButton(string name, Transform parent, Vector2 position,
        Vector2 size, string caption)
    {
        RectTransform rt = CreateRect(name, parent, position, size);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = Accent;
        Button button = rt.gameObject.AddComponent<Button>();
        Text label = Label("Caption", rt, Vector2.zero, size, caption, 19,
            TextAnchor.MiddleCenter);
        label.color = Color.white;
        return button;
    }
}
