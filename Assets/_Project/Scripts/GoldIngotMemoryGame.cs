using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum GoldIngotGameState { DifficultySelection, Memorizing, Ready, Drawing, Forming, Success, Failure }

/// <summary>挂在现有 GoldPaper 上。运行时创建占位 UI；只维护本次小游戏会话的“扎”数。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class GoldIngotMemoryGame : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("可指定包含中文的字体。Windows 默认使用 Microsoft YaHei。")]
    public Font uiFont;
    [Header("记忆折点顺序（距离以纸张高度为单位）")]
    public GoldIngotSequenceSettings sequence = new GoldIngotSequenceSettings();
    [Range(12,32)] public float endpointDiameter = 22;
    public int TotalBundles { get; private set; }
    public GoldIngotGameState State { get; private set; }
    public GoldIngotDifficulty Difficulty { get; private set; }
    public GoldIngotRoute CurrentRoute { get; private set; }
    public RectTransform Paper => paper;

    private RectTransform paper;
    private Image paperImage;
    private GoldIngotStrokeGraphic routeGraphic, strokeGraphic, startGraphic, endGraphic;
    private GoldIngotIconGraphic ingot;
    private Text title, status, instruction, count, gain;
    private Button retry, changeDifficulty;
    [SerializeField, HideInInspector] private Button[] difficultyButtons = new Button[3];
    private GoldIngotSequenceJudge judge;
    private readonly List<GoldIngotStrokeGraphic> candidates = new List<GoldIngotStrokeGraphic>();
    private readonly List<Vector2> linePoints = new List<Vector2>();
    private Vector2 livePointer;
    private readonly System.Random random = new System.Random();
    private float remaining;
    private int activePointer;
    private Camera pointerCamera;
    private GameObject view;
    private Coroutine formAnimation;
    private bool initialized;
    private static readonly Color Ink = new Color(.22f,.28f,.27f);
    private static readonly Color Gold = new Color(.83f,.73f,.49f);
    private static readonly Color Accent = new Color(.36f,.48f,.44f);

    private void Start() { Initialize(); }
    private void Initialize()
    {
        if (initialized) return;
        paper = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) { Debug.LogError("GoldIngotMemoryGame requires a Canvas parent.", this); enabled = false; return; }
        // 只停用同对象上的旧玩法组件；不删除脚本、素材或其他场景。
        var oldFold = GetComponent<GoldPaperFold>();
        if (oldFold != null) oldFold.enabled = false;
        paperImage = GetComponent<Image>();
        paperImage.enabled = true;
        paperImage.sprite = null;
        paperImage.color = Gold;
        if (uiFont == null)
        {
            uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 24);
            if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        BuildView(canvas.transform);
        paper.SetAsLastSibling();
        routeGraphic = CreateStroke("MemoryRoute", paper, Ink, 6f, false);
        strokeGraphic = CreateStroke("PlayerStroke", paper, Accent, 7f, false);
        startGraphic = CreateStroke("StartPoint", paper, new Color(.20f,.52f,.33f), endpointDiameter, false);
        endGraphic = CreateStroke("EndPoint", paper, new Color(.75f,.28f,.25f), endpointDiameter, false);
        ingot = CreateRect("Ingot", paper, Vector2.zero, new Vector2(270, 190)).gameObject.AddComponent<GoldIngotIconGraphic>();
        ingot.raycastTarget = false;
        initialized = true;
        SelectDifficultyScreen();
    }

    private void BuildView(Transform canvas)
    {
        RectTransform root = CreateRect("GoldIngot_V01_UI", canvas, Vector2.zero, Vector2.zero);
        Stretch(root); view = root.gameObject; root.SetAsFirstSibling();
        var background = root.gameObject.AddComponent<Image>();
        background.color = new Color(.94f,.95f,.93f); background.raycastTarget = false;
        title = Label("Title", root, new Vector2(-375, 272), new Vector2(470,50), 31, TextAnchor.MiddleLeft);
        AnchorHeader(title.rectTransform,false,35);
        title.text = "七月半 · 折金元宝";
        var subtitle = Label("Subtitle", root, Vector2.zero, new Vector2(470,32), 16, TextAnchor.MiddleLeft);
        AnchorHeader(subtitle.rectTransform,false,84);
        subtitle.text = "记住折点顺序，再按住左键一笔连起来。";
        count = Label("Count", root, new Vector2(460, 270), new Vector2(250,40), 23, TextAnchor.MiddleRight);
        AnchorHeader(count.rectTransform,true,35);
        string[] labels = new string[3];
        for (int i = 0; i < 3; i++) labels[i] = new[] {"简单","普通","困难"}[i]+"  ·  "+(i+1)+" 扎\n"+sequence.PointCount((GoldIngotDifficulty)i)+" 折点 · "+sequence.DistractorCount((GoldIngotDifficulty)i)+" 干扰";
        for (int i = 0; i < 3; i++)
        {
            int choice = i;
            difficultyButtons[i] = MakeButton("Difficulty"+i, root, new Vector2((i-1)*228,166), new Vector2(208,70), labels[i]);
            difficultyButtons[i].onClick.AddListener(() => ChooseDifficulty((GoldIngotDifficulty)choice));
        }
        status = Label("Status", root, new Vector2(0,99), new Vector2(1000,40), 22, TextAnchor.MiddleCenter);
        instruction = Label("Instruction", root, new Vector2(0,-282), new Vector2(1050,28), 16, TextAnchor.MiddleCenter);
        retry = MakeButton("Retry", root, new Vector2(126,-325), new Vector2(224,48), "再试一次");
        retry.onClick.AddListener(() => { if (State == GoldIngotGameState.Failure) Retry(); else if (State == GoldIngotGameState.Success) ChooseDifficulty(Difficulty); });
        changeDifficulty = MakeButton("ChangeDifficulty", root, new Vector2(-126,-325), new Vector2(224,48), "选择难度");
        changeDifficulty.onClick.AddListener(SelectDifficultyScreen);
        gain = Label("Gain", paper, new Vector2(0,-115), new Vector2(420,45), 25, TextAnchor.MiddleCenter);
        gain.transform.SetAsLastSibling();
    }

    public void ChooseDifficulty(GoldIngotDifficulty difficulty)
    {
        if (!initialized || State == GoldIngotGameState.Drawing || State == GoldIngotGameState.Forming) return;
        Difficulty = difficulty;
        try { CurrentRoute = GoldIngotSequenceRoutes.Create(difficulty,random,sequence,paper.rect.width/paper.rect.height); }
        catch (InvalidOperationException e) { status.text = e.Message; return; }
        BeginMemorizing();
    }
    public void Retry()
    {
        if (State != GoldIngotGameState.Failure || CurrentRoute == null) return;
        ChooseDifficulty(Difficulty);
    }
    public void SelectDifficultyScreen()
    {
        if (!initialized || State == GoldIngotGameState.Drawing || State == GoldIngotGameState.Forming) return;
        State = GoldIngotGameState.DifficultySelection;
        ResetVisuals();
        status.text = "选择难度，开始记忆";
        instruction.text = "所有难度均可直接选择。成功后获得金元宝，数量按“扎”累计。";
        SetButtons();
    }
    private void ResetVisuals()
    {
        paperImage.enabled = true; paperImage.color = Gold; paperImage.raycastTarget = false;
        paperImage.transform.localScale = Vector3.one;
        routeGraphic.gameObject.SetActive(false);
        strokeGraphic.SetPoints(Array.Empty<Vector2>());
        strokeGraphic.gameObject.SetActive(true); strokeGraphic.color = Accent;
        startGraphic.gameObject.SetActive(false);
        endGraphic.gameObject.SetActive(false);
        foreach (var dot in candidates) if (dot != null) dot.gameObject.SetActive(false);
        ingot.gameObject.SetActive(false); ingot.transform.localScale = Vector3.one;
        gain.text = ""; judge = null;
        count.text = "成品  " + TotalBundles + " 扎";
    }
    private void BeginMemorizing()
    {
        ResetVisuals();
        State = GoldIngotGameState.Memorizing;
        remaining = CurrentRoute.MemorySeconds;
        routeGraphic.color = Ink; routeGraphic.SetPoints(new[] {CurrentRoute.Points[0]});
        routeGraphic.gameObject.SetActive(true);
        ShowCandidates();
        startGraphic.SetPoints(new[] { CurrentRoute.Points[0] });
        startGraphic.gameObject.SetActive(true);
        endGraphic.SetPoints(new[] { CurrentRoute.Points[CurrentRoute.Points.Length-1] });
        endGraphic.gameObject.SetActive(true);
        instruction.text = "绿色起点，红色终点。记住连线顺序，所有小圆点外观相同。";
        SetButtons(); UpdateCountdown();
    }
    private void Update()
    {
        if (State == GoldIngotGameState.Failure)
        { remaining -= Time.unscaledDeltaTime; if (remaining <= 0) routeGraphic.gameObject.SetActive(false); return; }
        if (State != GoldIngotGameState.Memorizing) return;
        remaining -= Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01((CurrentRoute.MemorySeconds-remaining)/(CurrentRoute.MemorySeconds*.86f))*(CurrentRoute.Points.Length-1);
        int segment = Mathf.Min(Mathf.FloorToInt(progress),CurrentRoute.Points.Length-2);
        linePoints.Clear();
        for (int i = 0; i <= segment; i++) linePoints.Add(CurrentRoute.Points[i]);
        linePoints.Add(Vector2.Lerp(CurrentRoute.Points[segment],CurrentRoute.Points[segment+1],progress-segment));
        routeGraphic.SetPoints(linePoints);
        if (remaining <= 0)
        {
            State = GoldIngotGameState.Ready;
            routeGraphic.gameObject.SetActive(false);
            paperImage.raycastTarget = true;
            status.text = "路线已隐藏 · 从起点按住左键开始";
            instruction.text = "按住左键，依次靠近记住的折点。自动吸附，连到红色终点再松手。";
        }
        else UpdateCountdown();
    }
    private void UpdateCountdown()
    { status.text = "记住连接顺序  ·  " + Mathf.CeilToInt(remaining) + " 秒"; }

    public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = false; }
    public void OnBeginDrag(PointerEventData e) { }
    public void OnPointerDown(PointerEventData e)
    {
        if (State != GoldIngotGameState.Ready || e.button != PointerEventData.InputButton.Left) return;
        if (!ToNormalized(e.position,e.pressEventCamera,out Vector2 p)) return;
        var candidate = new GoldIngotSequenceJudge(CurrentRoute,paper.rect.width/paper.rect.height,p);
        if (!candidate.Valid) { status.text = "请从绿色起点附近按下左键。"; return; }
        judge = candidate; activePointer = e.pointerId; pointerCamera = e.pressEventCamera;
        State = GoldIngotGameState.Drawing;
        livePointer = p; DrawConnections(p);
        status.text = "保持按住，按记忆中的顺序连点"; SetButtons();
    }
    public void OnDrag(PointerEventData e)
    {
        if (State == GoldIngotGameState.Drawing && e.pointerId == activePointer) Sample(e.position);
    }
    private bool Sample(Vector2 screen)
    {
        if (judge.Complete) return true;
        if (!ToNormalized(screen,pointerCamera,out Vector2 p)) { Fail("无法取得画笔位置，请重试。"); return false; }
        int before = judge.ConnectedCount;
        if (!judge.Append(p)) { Fail(judge.Failure); return false; }
        for (int i = before; i < judge.ConnectedCount; i++)
            foreach (var dot in candidates)
                if (dot.gameObject.activeSelf && dot.Points[0] == CurrentRoute.Points[i]) StartCoroutine(Pulse(dot));
        livePointer = p; DrawConnections(p);
        if (judge.Complete) status.text = "已连到终点 · 松开左键即可完成";
        return true;
    }
    public void OnPointerUp(PointerEventData e)
    {
        if (State != GoldIngotGameState.Drawing || e.pointerId != activePointer || e.button != PointerEventData.InputButton.Left) return;
        if (!Sample(e.position)) return;
        if (!judge.Finish()) { Fail(judge.Failure); return; }
        State = GoldIngotGameState.Forming; paperImage.raycastTarget = false;
        status.text = "折痕完成"; startGraphic.gameObject.SetActive(false); SetButtons();
        endGraphic.gameObject.SetActive(false);
        foreach (var dot in candidates) dot.gameObject.SetActive(false);
        formAnimation = StartCoroutine(FormIngot());
    }
    public void OnEndDrag(PointerEventData e) { OnPointerUp(e); }
    private bool ToNormalized(Vector2 screen, Camera cam, out Vector2 normalized)
    {
        normalized = Vector2.zero;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(paper,screen,cam,out Vector2 local)) return false;
        Rect r = paper.rect;
        normalized = new Vector2((local.x-r.xMin)/r.width,(local.y-r.yMin)/r.height);
        return true;
    }
    private void Fail(string reason)
    {
        State = GoldIngotGameState.Failure; paperImage.raycastTarget = false;
        routeGraphic.color = new Color(.65f,.42f,.36f,.7f);
        routeGraphic.SetPoints(CurrentRoute.Points);
        routeGraphic.gameObject.SetActive(true);
        remaining = Mathf.Max(.3f,sequence.failureRevealSeconds);
        status.text = reason;
        instruction.text = "短暂显示正确路线。点击再试一次，立即开始新的挑战。"; SetButtons();
    }
    private void ShowCandidates()
    {
        var points = new List<Vector2>();
        for (int i = 1; i < CurrentRoute.Points.Length-1; i++) points.Add(CurrentRoute.Points[i]);
        points.AddRange(CurrentRoute.Distractors);
        // 随机渲染次序，不让层级名称或重叠顺序透露正确折点。
        for (int i = points.Count-1; i > 0; i--) { int j = random.Next(i+1); Vector2 temp = points[i]; points[i] = points[j]; points[j] = temp; }
        while (candidates.Count < points.Count) candidates.Add(CreateStroke("Candidate"+candidates.Count,paper,Ink,16,false));
        for (int i = 0; i < candidates.Count; i++)
        {
            var dot = candidates[i]; dot.Width = 16;
            dot.gameObject.SetActive(i < points.Count);
            if (i < points.Count) dot.SetPoints(new[] {points[i]});
        }
        startGraphic.transform.SetAsLastSibling(); endGraphic.transform.SetAsLastSibling();
        ingot.transform.SetAsLastSibling();
    }
    private void DrawConnections(Vector2 pointer)
    {
        // 吸附力度只按距离计算，正确点、干扰点一视同仁，不预告答案。
        Vector2 closest = pointer; float distance = float.MaxValue;
        foreach (var dot in candidates)
            if (dot.gameObject.activeSelf && judge.Distance(pointer,dot.Points[0]) < distance)
            { distance = judge.Distance(pointer,dot.Points[0]); closest = dot.Points[0]; }
        foreach (Vector2 endpoint in new[] {CurrentRoute.Points[0],CurrentRoute.Points[CurrentRoute.Points.Length-1]})
            if (judge.Distance(pointer,endpoint) < distance) { distance = judge.Distance(pointer,endpoint); closest = endpoint; }
        float strength = distance >= sequence.Attraction ? 0 : distance <= CurrentRoute.Tolerance ? 1 :
            1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(CurrentRoute.Tolerance,sequence.Attraction,distance));
        livePointer = Vector2.Lerp(pointer,closest,strength);
        linePoints.Clear();
        for (int i = 0; i < judge.ConnectedCount; i++) linePoints.Add(CurrentRoute.Points[i]);
        if (!judge.Complete) linePoints.Add(livePointer);
        strokeGraphic.SetPoints(linePoints);
    }
    private IEnumerator Pulse(GoldIngotStrokeGraphic dot)
    {
        for (float t = 0; t < .18f; t += Time.unscaledDeltaTime)
        {
            if (State != GoldIngotGameState.Drawing) break;
            dot.Width = 16*(1+.35f*Mathf.Sin(t/.18f*Mathf.PI)); dot.SetVerticesDirty();
            yield return null;
        }
        dot.Width = 16; dot.SetVerticesDirty();
    }
    private IEnumerator FormIngot()
    {
        ingot.gameObject.SetActive(true); ingot.color = new Color(1,1,1,0);
        for (float elapsed = 0; elapsed < .65f; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.Clamp01(elapsed/.65f);
            paperImage.color = new Color(Gold.r,Gold.g,Gold.b,1-Mathf.SmoothStep(0,1,t));
            strokeGraphic.color = new Color(Accent.r,Accent.g,Accent.b,1-t);
            ingot.color = new Color(1,1,1,Mathf.SmoothStep(0,1,t)); ingot.SetVerticesDirty();
            float pop = t < .75f ? Mathf.Lerp(.65f,1.08f,Mathf.SmoothStep(0,1,t/.75f)) : Mathf.Lerp(1.08f,1,(t-.75f)/.25f);
            ingot.transform.localScale = Vector3.one * pop;
            yield return null;
        }
        paperImage.color = new Color(Gold.r,Gold.g,Gold.b,0);
        strokeGraphic.gameObject.SetActive(false); ingot.color = Color.white; ingot.SetVerticesDirty();
        ingot.transform.localScale = Vector3.one;
        TotalBundles += CurrentRoute.Reward; formAnimation = null;
        State = GoldIngotGameState.Success;
        status.text = "记住了，也画对了。";
        gain.text = "+ " + CurrentRoute.Reward + " 扎金元宝";
        count.text = "成品  " + TotalBundles + " 扎";
        instruction.text = "再来一张，或选择其他难度。"; SetButtons();
    }
    private void SetButtons()
    {
        bool busy = State == GoldIngotGameState.Drawing || State == GoldIngotGameState.Forming;
        for (int i = 0; i < 3; i++)
        {
            difficultyButtons[i].interactable = !busy;
            bool selected = State != GoldIngotGameState.DifficultySelection && i == (int)Difficulty;
            difficultyButtons[i].GetComponent<Image>().color = selected ? Accent : new Color(.86f,.89f,.85f);
            difficultyButtons[i].GetComponentInChildren<Text>().color = selected ? new Color(.96f,.96f,.92f) : Ink;
        }
        changeDifficulty.interactable = !busy && State != GoldIngotGameState.DifficultySelection;
        retry.gameObject.SetActive(State == GoldIngotGameState.Failure || State == GoldIngotGameState.Success);
        retry.GetComponentInChildren<Text>().text = State == GoldIngotGameState.Success ? "再来一张" : "再试一次";
    }
    private void OnApplicationFocus(bool focus)
    { if (!focus && State == GoldIngotGameState.Drawing) Fail("绘制被中断，请重试。"); }
    private void OnApplicationPause(bool paused)
    { if (paused && State == GoldIngotGameState.Drawing) Fail("绘制被中断，请重试。"); }
    private void OnDisable()
    {
        if (!initialized) return;
        if (formAnimation != null) { StopCoroutine(formAnimation); formAnimation = null; State = GoldIngotGameState.Failure; }
        else if (State == GoldIngotGameState.Drawing) Fail("绘制被中断，请重试。");
        if (view != null) view.SetActive(false);
    }
    private void OnEnable() { if (initialized && view != null) { view.SetActive(true); SelectDifficultyScreen(); } }

    private GoldIngotStrokeGraphic CreateStroke(string name, Transform parent, Color color, float width, bool nodes)
    {
        RectTransform rt = CreateRect(name,parent,Vector2.zero,Vector2.zero); Stretch(rt);
        var graphic = rt.gameObject.AddComponent<GoldIngotStrokeGraphic>();
        graphic.color = color; graphic.Width = width; graphic.ShowPoints = nodes; graphic.raycastTarget = false;
        return graphic;
    }
    private RectTransform CreateRect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)new GameObject(name,typeof(RectTransform)).transform;
        rt.gameObject.layer = 5; rt.SetParent(parent,false); rt.anchorMin = rt.anchorMax = new Vector2(.5f,.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos; return rt;
    }
    private void Stretch(RectTransform rt)
    { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    private void AnchorHeader(RectTransform rt,bool right,float top)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(right ? 1 : 0,1);
        rt.anchoredPosition = new Vector2(right ? -40 : 40,-top);
    }
    private Text Label(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, TextAnchor alignment)
    {
        var label = CreateRect(name,parent,pos,size).gameObject.AddComponent<Text>();
        label.font = uiFont; label.fontSize = fontSize; label.color = Ink; label.alignment = alignment;
        label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow; return label;
    }
    private Button MakeButton(string name, Transform parent, Vector2 pos, Vector2 size, string text)
    {
        var rt = CreateRect(name,parent,pos,size); var image = rt.gameObject.AddComponent<Image>(); image.color = Accent;
        var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.highlightedColor = new Color(.91f,.95f,.92f); colors.pressedColor = new Color(.72f,.81f,.75f);
        colors.disabledColor = new Color(.8f,.8f,.8f,.55f); button.colors = colors;
        var label = Label("Label",rt,Vector2.zero,size-Vector2.one*10,19,TextAnchor.MiddleCenter);
        label.text = text; label.color = new Color(.96f,.96f,.92f); return button;
    }
}

