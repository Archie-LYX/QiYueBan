using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 草纸 V0.1：格子桌面排版解谜。点击选择、拖动滑动、方向键移动、R 旋转、Z 撤销。
/// 单独测试场景运行；不依赖剧情或经营系统。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class GrassPaperPuzzleGame : MonoBehaviour
{
    [Header("画面")]
    [Range(48, 72)] public float cellSize = 61f;
    public Font uiFont;
    [Header("未来接入经营库存的事件")]
    public UnityEvent onBatchCompleted = new UnityEvent();

    public GrassPaperPuzzleRules Rules { get; private set; }
    public int TotalBatches { get; private set; }
    public int CurrentLevelIndex { get; private set; }
    public bool IsComplete => Rules != null && Rules.IsSolved;
    public RectTransform Board => board;
    public float CellSize => cellSize;

    private RectTransform board;
    private Text titleText;
    private Text instructionText;
    private Text statusText;
    private Text counterText;
    private Text selectedText;
    private Text resultText;
    private Button undoButton;
    private Button nextButton;
    private Button rotateButton;
    private readonly List<GameObject> boardItems = new List<GameObject>();
    private readonly List<Outline> pieceOutlines = new List<Outline>();
    private readonly List<Button> levelButtons = new List<Button>();
    private int selectedIndex = -1;
    private bool awardedThisAttempt;
    private bool viewReady;

    private static readonly Color Background = new Color(.955f, .945f, .915f);
    private static readonly Color BoardColor = new Color(.86f, .82f, .75f);
    private static readonly Color StoneColor = new Color(.43f, .47f, .46f);
    private static readonly Color Ink = new Color(.24f, .30f, .32f);
    private static readonly Color Accent = new Color(.71f, .42f, .36f);
    private static readonly Color[] PaperColors =
    {
        new Color(.90f,.74f,.46f), new Color(.83f,.69f,.48f),
        new Color(.92f,.80f,.55f), new Color(.77f,.66f,.49f),
        new Color(.87f,.73f,.51f)
    };

    private void Start()
    {
        BuildView();
        LoadLevel(0);
    }

    private void Update()
    {
        if (!viewReady || Rules == null) return;
        Keyboard k = Keyboard.current;
        if (k == null) return;

        if (k.zKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame)
        {
            Undo();
            return;
        }

        if (k.enterKey.wasPressedThisFrame && IsComplete)
        {
            NextLevel();
            return;
        }

        if (k.rKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame)
        {
            RotateSelected();
            return;
        }

        if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) MoveSelected(-1,0);
        else if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) MoveSelected(1,0);
        else if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) MoveSelected(0,-1);
        else if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) MoveSelected(0,1);
    }

    public void LoadLevel(int index)
    {
        if (!viewReady || index < 0 || index >= GrassPaperPuzzleRules.Levels.Length) return;
        CurrentLevelIndex = index;
        Rules = new GrassPaperPuzzleRules(GrassPaperPuzzleRules.Levels[index]);
        selectedIndex = -1;
        awardedThisAttempt = false;
        statusText.text = "把每张草纸移到同字母的虚线位置，方向与尺寸也要吻合。";
        RenderBoard();
        RefreshText();
    }

    public void SelectPiece(int index)
    {
        if (Rules == null || index < 0 || index >= Rules.PieceCount) return;
        selectedIndex = index;
        statusText.text = "已选择纸张 " + Rules.Level.pieces[index].name +
            " · 方向键滑动 / R 旋转 / Z 撤销";
        RefreshSelectionOutlines();
        RefreshText();
    }

    public void MoveSelected(int dx, int dy)
    {
        if (Rules == null || selectedIndex < 0 || IsComplete) return;
        bool ok = Rules.TrySlide(selectedIndex, dx, dy);
        statusText.text = ok ? "移动成功。再看看下一步。" : "被草纸或镇石挡住了，不能穿过去。";
        AfterAction(ok);
    }

    public void ApplyDrag(int index, Vector2 localDelta)
    {
        if (Rules == null || IsComplete) { RenderBoard(); return; }
        selectedIndex = index;
        bool horizontal = Mathf.Abs(localDelta.x) >= Mathf.Abs(localDelta.y);
        float signedDistance = horizontal ? localDelta.x : -localDelta.y;
        int steps = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(signedDistance) / cellSize),
            0, Mathf.Max(Rules.Level.width, Rules.Level.height));
        if (steps == 0)
        {
            statusText.text = "拖动至少半格，或点击方向按钮移动。";
            RenderBoard(); RefreshText(); return;
        }

        int sign = signedDistance >= 0f ? 1 : -1;
        int dx = horizontal ? sign : 0;
        int dy = horizontal ? 0 : sign;
        int moved = 0;
        for (int i = 0; i < steps; i++)
        {
            if (!Rules.TrySlide(index, dx, dy)) break;
            moved++;
        }

        statusText.text = moved > 0
            ? "滑动 " + moved + " 格 · 已自动停在障碍物前"
            : "前方没有空位，换一张纸试试。";
        AfterAction(moved > 0);
    }

    public void RotateSelected()
    {
        if (Rules == null || selectedIndex < 0 || IsComplete) return;
        bool ok = Rules.TryRotate(selectedIndex);
        statusText.text = ok ? "旋转 90° · 看看新方向是否有路。" :
            "旋转后的格子被占据或越界，先让出空间。";
        AfterAction(ok);
    }

    public void Undo()
    {
        if (Rules == null) return;
        if (Rules.Undo())
        {
            statusText.text = "已撤销上一步。";
            RenderBoard();
            RefreshText();
        }
        else statusText.text = "已经没有可撤销的步骤。";
    }

    public void Restart()
    {
        if (Rules == null) return;
        Rules.Reset();
        awardedThisAttempt = false;
        selectedIndex = -1;
        statusText.text = "关卡已重新开始。";
        RenderBoard();
        RefreshText();
    }

    public void NextLevel()
    {
        if (!IsComplete) return;
        if (CurrentLevelIndex + 1 < GrassPaperPuzzleRules.Levels.Length)
        {
            LoadLevel(CurrentLevelIndex + 1);
        }
        else
        {
            statusText.text = "五道关卡全部完成！可以选择关卡继续挑战更少步数。";
            RefreshText();
        }
    }

    private void AfterAction(bool changed)
    {
        if (!changed) { RenderBoard(); RefreshText(); return; }
        if (Rules.IsSolved && !awardedThisAttempt)
        {
            awardedThisAttempt = true;
            TotalBatches++;
            onBatchCompleted.Invoke();
        }
        RenderBoard();
        RefreshText();
    }

    private void RefreshText()
    {
        if (Rules == null) return;
        GrassPaperLevel level = Rules.Level;
        titleText.text = "草纸 · " + level.title;
        instructionText.text = level.subtitle;
        counterText.text = "第 " + (CurrentLevelIndex + 1) + "/5 关  ·  已走 " +
            Rules.MoveCount + " 步";
        selectedText.text = selectedIndex < 0 ? "先点击一张纸" :
            "当前纸张：" + level.pieces[selectedIndex].name;

        if (IsComplete)
        {
            string grade = Rules.MoveCount <= level.knownMinimumSteps ? "★★★" :
                Rules.MoveCount <= level.knownMinimumSteps + 5 ? "★★" : "★";
            resultText.text = grade + "  完成排版！\n最少 " +
                level.knownMinimumSteps + " 步 · 本次 " + Rules.MoveCount +
                " 步\n草纸成品：" + TotalBatches + " 扎";
            nextButton.GetComponentInChildren<Text>().text =
                CurrentLevelIndex + 1 == GrassPaperPuzzleRules.Levels.Length
                ? "全部完成" : "下一关 →";
            nextButton.interactable = CurrentLevelIndex + 1 <
                GrassPaperPuzzleRules.Levels.Length;
        }
        else
        {
            resultText.text = "目标：把所有字母纸张放到对应虚线框。\n" +
                "单击选择 / 拖动或方向键移动 / R 旋转 / Z 撤销";
            nextButton.interactable = false;
        }
        undoButton.interactable = Rules.CanUndo;
        rotateButton.interactable = selectedIndex >= 0 && !IsComplete;
        foreach (Button button in levelButtons)
        {
            int idx = levelButtons.IndexOf(button);
            button.GetComponent<Image>().color = idx == CurrentLevelIndex ?
                Accent : new Color(.80f,.81f,.77f);
        }
    }

    private void RenderBoard()
    {
        foreach (GameObject go in boardItems)
            if (go != null) { go.SetActive(false); Destroy(go); }
        boardItems.Clear();
        pieceOutlines.Clear();

        GrassPaperLevel level = Rules.Level;
        board.sizeDelta = new Vector2(level.width * cellSize, level.height * cellSize);

        for (int y = 0; y < level.height; y++)
        for (int x = 0; x < level.width; x++)
        {
            RectTransform rt = BoardRect("Cell_" + x + "_" + y, x,y,1,1, 2f);
            Image im = rt.gameObject.AddComponent<Image>();
            im.color = (x + y) % 2 == 0 ? new Color(.91f,.88f,.81f) :
                new Color(.88f,.85f,.78f);
            im.raycastTarget = false;
        }

        foreach (Vector2Int stone in level.stones)
        {
            RectTransform rt = BoardRect("Stone", stone.x, stone.y, 1, 1, 5f);
            Image im = rt.gameObject.AddComponent<Image>();
            im.color = StoneColor;
            im.raycastTarget = false;
            Text t = AddText("StoneLabel",rt,Vector2.zero,rt.sizeDelta,
                "镇",20,TextAnchor.MiddleCenter,Color.white);
            t.raycastTarget = false;
        }

        for (int i = 0; i < Rules.PieceCount; i++)
        {
            GrassPaperPieceSpec piece = level.pieces[i];
            GrassPaperPose target = piece.target;
            Vector2Int size = Rules.GetSize(i,target);
            RectTransform rt = BoardRect("Target_" + piece.name,
                target.x,target.y,size.x,size.y, 6f);
            Image im = rt.gameObject.AddComponent<Image>();
            im.color = new Color(PaperColors[i%PaperColors.Length].r,
                PaperColors[i%PaperColors.Length].g,
                PaperColors[i%PaperColors.Length].b,.25f);
            im.raycastTarget = false;
            Outline outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(Accent.r,Accent.g,Accent.b,.75f);
            outline.effectDistance = new Vector2(2f,-2f);
            Text t = AddText("TargetLabel",rt,Vector2.zero,rt.sizeDelta,
                "目标 " + piece.name,17,TextAnchor.MiddleCenter,
                new Color(.39f,.34f,.28f,.7f));
            t.raycastTarget = false;
        }

        for (int i = 0; i < Rules.PieceCount; i++)
        {
            GrassPaperPose pose = Rules.GetPose(i);
            Vector2Int size = Rules.GetSize(i,pose);
            RectTransform rt = BoardRect("Paper_" + (i+1),pose.x,pose.y,
                size.x,size.y, 6f);
            Image im = rt.gameObject.AddComponent<Image>();
            im.color = PaperColors[i % PaperColors.Length];
            im.raycastTarget = true;
            Outline outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = i == selectedIndex ? Accent :
                new Color(.54f,.42f,.27f);
            outline.effectDistance = i == selectedIndex ?
                new Vector2(3f,-3f) : new Vector2(1f,-1f);

            pieceOutlines.Add(outline);
            GrassPaperPieceDrag drag = rt.gameObject.AddComponent<GrassPaperPieceDrag>();
            drag.Initialize(this,i);
            AddText("PaperLabel",rt,Vector2.zero,rt.sizeDelta,
                Rules.Level.pieces[i].name,23,TextAnchor.MiddleCenter,Ink);
        }
    }

    private void RefreshSelectionOutlines()
    {
        for (int i = 0; i < pieceOutlines.Count; i++)
        {
            Outline o = pieceOutlines[i];
            if (o == null) continue;
            o.effectColor = i == selectedIndex ? Accent : new Color(.54f,.42f,.27f);
            o.effectDistance = i == selectedIndex ?
                new Vector2(3f,-3f) : new Vector2(1f,-1f);
        }
    }

    private RectTransform BoardRect(string name, float x, float y,
        float width, float height, float padding)
    {
        RectTransform rt = MakeRect(name, board, Vector2.zero,
            new Vector2(width*cellSize-padding,height*cellSize-padding));
        rt.anchoredPosition = new Vector2(
            (x + width*.5f - Rules.Level.width*.5f)*cellSize,
            (Rules.Level.height*.5f - y - height*.5f)*cellSize);
        boardItems.Add(rt.gameObject);
        return rt;
    }

    private void BuildView()
    {
        if (viewReady) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("GrassPaperPuzzleGame requires a Canvas parent.", this);
            enabled = false; return;
        }

        Image rootImage = GetComponent<Image>();
        rootImage.color = Background;
        rootImage.raycastTarget = false;

        if (uiFont == null)
        {
            uiFont = Font.CreateDynamicFontFromOSFont(
                new[] {"Microsoft YaHei","SimHei","Arial"},24);
            if (uiFont == null)
                uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        titleText = AddText("Title",transform,new Vector2(-240,305),
            new Vector2(740,50),"草纸 · 排版谜题",30,TextAnchor.MiddleLeft,Ink);
        instructionText = AddText("Subtitle",transform,new Vector2(-240,263),
            new Vector2(740,35),"将每张纸放入对应位置",18,TextAnchor.MiddleLeft,Ink);
        counterText = AddText("Counter",transform,new Vector2(-270,-268),
            new Vector2(630,36),"",19,TextAnchor.MiddleCenter,Ink);
        statusText = AddText("Status",transform,new Vector2(0,-322),
            new Vector2(1160,35),"",18,TextAnchor.MiddleCenter,Ink);

        board = MakeRect("GrassPaperBoard",transform,new Vector2(-235,-8),
            new Vector2(380,380));
        var bg = board.gameObject.AddComponent<Image>();
        bg.color = BoardColor;
        bg.raycastTarget = false;

        selectedText = AddText("Selected",transform,new Vector2(343,196),
            new Vector2(380,42),"",22,TextAnchor.MiddleCenter,Ink);
        resultText = AddText("Result",transform,new Vector2(335,-187),
            new Vector2(450,90),"",17,TextAnchor.MiddleCenter,Ink);

        MakeButton("Up",new Vector2(342,107),"↑",()=>MoveSelected(0,-1));
        MakeButton("Left",new Vector2(274,42),"←",()=>MoveSelected(-1,0));
        MakeButton("Right",new Vector2(410,42),"→",()=>MoveSelected(1,0));
        MakeButton("Down",new Vector2(342,-23),"↓",()=>MoveSelected(0,1));
        rotateButton = MakeButton("Rotate",new Vector2(342,-95),"旋转 R",
            RotateSelected,new Vector2(180,46));
        undoButton = MakeButton("Undo",new Vector2(245,-270),"撤销 Z",
            Undo,new Vector2(148,42));
        MakeButton("Restart",new Vector2(410,-270),"重来",
            Restart,new Vector2(128,42));
        nextButton = MakeButton("Next",new Vector2(550,-270),"下一关 →",
            NextLevel,new Vector2(140,42));

        for (int i = 0; i < GrassPaperPuzzleRules.Levels.Length; i++)
        {
            int copy = i;
            Button button = MakeButton("Level"+(i+1),
                new Vector2(185+i*80,246),(i+1).ToString(),()=>LoadLevel(copy),
                new Vector2(60,38));
            levelButtons.Add(button);
        }

        viewReady = true;
    }

    private RectTransform MakeRect(string name, Transform parent, Vector2 pos,
        Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent,false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f,.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private Text AddText(string name, Transform parent, Vector2 pos, Vector2 size,
        string message, int fontSize, TextAnchor align, Color color)
    {
        RectTransform rt = MakeRect(name,parent,pos,size);
        Text text = rt.gameObject.AddComponent<Text>();
        text.font = uiFont;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = color;
        text.text = message;
        text.raycastTarget = false;
        return text;
    }

    private Button MakeButton(string name, Vector2 pos, string caption,
        UnityAction click, Vector2? size = null)
    {
        RectTransform rt = MakeRect(name,transform,pos,size ?? new Vector2(58,48));
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = new Color(.80f,.81f,.77f);
        Button button = rt.gameObject.AddComponent<Button>();
        button.onClick.AddListener(click);
        AddText("Text",rt,Vector2.zero,rt.sizeDelta,caption,
            20,TextAnchor.MiddleCenter,Ink);
        return button;
    }
}

/// <summary>纸张的鼠标/触摸拖动适配器：只提交通过格子规则校验的单轴滑动。</summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class GrassPaperPieceDrag : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private GrassPaperPuzzleGame game;
    private int index;
    private RectTransform rt;
    private Vector2 dragStart;
    private Vector2 homePosition;
    private bool dragging;

    public void Initialize(GrassPaperPuzzleGame owner, int pieceIndex)
    {
        game = owner;
        index = pieceIndex;
        rt = (RectTransform)transform;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left)
            game.SelectPiece(index);
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || game.IsComplete) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            game.Board,e.position,e.pressEventCamera,out dragStart)) return;
        homePosition = rt.anchoredPosition;
        dragging = true;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!dragging) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            game.Board,e.position,e.pressEventCamera,out Vector2 now)) return;
        rt.anchoredPosition = homePosition + (now - dragStart);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!dragging) return;
        dragging = false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            game.Board,e.position,e.pressEventCamera,out Vector2 now))
        {
            game.SelectPiece(index); return;
        }
        game.ApplyDrag(index,now-dragStart);
    }
}
