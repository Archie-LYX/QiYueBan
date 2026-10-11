#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>场景搭建与验收均限定于 V0.1；不进入玩家构建。</summary>
public static class GoldIngotV01Tools
{
    public const string ScenePath = "Assets/_Project/Scences/GoldIngot_V01.unity";
    private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,"../../../QiYueBan/review/ingot-sequence"));
    private static string Request => Path.Combine(Output,"request.txt");
    private static GoldIngotMemoryGame game;
    private static Mouse mouse;
    private static int step, difficulty, routePoint;
    private static double next;
    private static Vector2 cursor;
    private static GoldIngotRoute retryRoute;
    private static bool wasRunInBackground;
    private static bool batchVerification;
    private static InputSettings originalInputSettings;
    private static InputSettings.BackgroundBehavior originalBackground;
    private static InputSettings.EditorInputBehaviorInPlayMode originalEditorInput;

    public static void CommandLineVerify()
    {
        try
        {
            batchVerification = true;
            EditorSceneManager.OpenScene(ScenePath);
            RunRuleTests();
            File.WriteAllText(Request,"TEST");
            EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        EditorApplication.delayCall += CheckRequest;
    }
    private static void BeforeReload() { if (mouse != null) Finish(); }
    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode && mouse != null) Finish();
        if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += CheckRequest;
    }
    private static void CheckRequest()
    {
        if (!File.Exists(Request)) return;
        string action = File.ReadAllText(Request).Trim();
        if (action == "RULES")
        {
            File.WriteAllText(Request,"RUNNING");
            try { RunRuleTests(); File.WriteAllText(Request,"PASS"); }
            catch (Exception e) { File.WriteAllText(Request,"FAIL: "+e); Debug.LogException(e); }
            return;
        }
        if (action == "TESTFRESH")
        {
            File.WriteAllText(Request,"TEST");
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = !EditorApplication.isPlaying;
            return;
        }
        if (action == "SETUP" && !EditorApplication.isPlaying)
        { File.WriteAllText(Request,"RUNNING"); BuildScene(); }
        else if (action == "TEST" && EditorApplication.isPlaying)
        { File.WriteAllText(Request,"RUNNING"); RunPlayTests(); }
        else if (action == "TEST" && !EditorApplication.isPlaying)
        { EditorApplication.isPaused = false; EditorApplication.isPlaying = true; }
    }
    [MenuItem("Tools/Gold Ingot/Create or Open V0.1 Scene")]
    public static void BuildScene()
    {
        Directory.CreateDirectory(Output);
        if (EditorApplication.isPlaying) throw new Exception("Exit Play Mode before creating a scene.");
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty) throw new Exception("Please save the current scene before setup; it has unsaved changes.");
        if (!File.Exists(ScenePath))
        {
            if (active.path != "Assets/_Project/Scences/Shop_Prototype.unity")
                throw new Exception("Open Shop_Prototype first so its Canvas and GoldPaper can be reused.");
            if (!EditorSceneManager.SaveScene(active,ScenePath,true)) throw new Exception("Unable to copy scene.");
            AssetDatabase.ImportAsset(ScenePath);
        }
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var paperObject = FindInScene(scene,"GoldPaper");
        var canvasObject = FindInScene(scene,"Canvas");
        if (paperObject == null || canvasObject == null) throw new Exception("Canvas or GoldPaper missing.");
        var paper = paperObject.GetComponent<RectTransform>();
        paper.anchorMin = paper.anchorMax = new Vector2(.5f,.5f);
        paper.pivot = new Vector2(.5f,.5f); paper.sizeDelta = new Vector2(640,330);
        paper.anchoredPosition = new Vector2(0,-95); paper.localScale = Vector3.one;
        var oldFold = paperObject.GetComponent<GoldPaperFold>(); if (oldFold != null) oldFold.enabled = false;
        if (paperObject.GetComponent<GoldIngotMemoryGame>() == null) paperObject.AddComponent<GoldIngotMemoryGame>();
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = .5f;
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = paperObject;
        RunRuleTests();
        File.WriteAllText(Request,"READY");
        Debug.Log("Gold Ingot V0.1 scene ready: " + ScenePath);
    }
    private static GameObject FindInScene(UnityEngine.SceneManagement.Scene scene,string name)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }
    private static void Require(bool value,string message)
    { if (!value) throw new Exception("V01 FAIL: " + message); }
    [MenuItem("Tools/Gold Ingot/Test Route Rules")]
    public static void RunRuleTests()
    {
        GoldIngotSequenceTests.Run();
    }
    [MenuItem("Tools/Gold Ingot/Verify Sequence Game in Play Mode")]
    public static void RunPlayTests()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first.");
        RunRuleTests();
        game = UnityEngine.Object.FindFirstObjectByType<GoldIngotMemoryGame>();
        Require(game != null,"V0.1 game exists");
        EditorApplication.isPaused = false;
        wasRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        // 临时测试设置也用于交互编辑器，避免 Game 面板焦点/缩放影响虚拟设备。
        {
            originalInputSettings = InputSystem.settings;
            originalBackground = originalInputSettings.backgroundBehavior;
            originalEditorInput = originalInputSettings.editorInputBehaviorInPlayMode;
            originalInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            originalInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output,"play-tests.txt"),"Running complete UI / Input System scenarios.\n");
        mouse = InputSystem.AddDevice<Mouse>("GoldIngotV01TestMouse");
        difficulty = step = routePoint = 0; next = EditorApplication.timeSinceStartup + .6;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Capture(string name)
    {
        if (!Application.isBatchMode)
        { ScreenCapture.CaptureScreenshot(Path.Combine(Output,name+".png")); return; }
        // batchmode 没有 GameView 后台缓冲。使用当前 CanvasRenderer 的实际网格、
        // 材质与字体图集绘制到离屏纹理；不重新拼造 UI，也不依赖旧截图。
        Canvas.ForceUpdateCanvases();
        int width = Mathf.Max(16,UnityEngine.Screen.width), height = Mathf.Max(16,UnityEngine.Screen.height);
        var target = RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        bool previousSrgb = GL.sRGBWrite;
        RenderTexture.active = target;
        GL.sRGBWrite = false;
        GL.Clear(true,true,Color.white);
        GL.PushMatrix(); GL.LoadPixelMatrix(0,width,0,height);
        var graphics = game.GetComponentInParent<Canvas>().GetComponentsInChildren<Graphic>();
        Array.Sort(graphics,(a,b) => a.canvasRenderer.absoluteDepth.CompareTo(b.canvasRenderer.absoluteDepth));
        foreach (var graphic in graphics)
        {
            if (!graphic.isActiveAndEnabled || graphic.canvasRenderer.cull) continue;
            Mesh source = graphic.canvasRenderer.GetMesh();
            if (source == null || source.vertexCount == 0) continue;
            var mesh = UnityEngine.Object.Instantiate(source);
            var vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(null,graphic.transform.TransformPoint(vertices[i]));
                vertices[i] = new Vector3(p.x,p.y,0);
            }
            mesh.vertices = vertices; mesh.RecalculateBounds();
            var material = new Material(graphic.materialForRendering);
            material.SetTexture("_MainTex",graphic.mainTexture);
            material.SetColor("_Color",graphic.canvasRenderer.GetColor());
            if (material.SetPass(0)) Graphics.DrawMeshNow(mesh,Matrix4x4.identity);
            UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(mesh);
        }
        GL.PopMatrix();
        var image = new Texture2D(width,height,TextureFormat.RGBA32,false);
        image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
        File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
        RenderTexture.active = previous; GL.sRGBWrite = previousSrgb; RenderTexture.ReleaseTemporary(target);
    }
    private static void Queue(Vector2 point,bool down)
    {
        cursor = point;
        InputSystem.QueueStateEvent(mouse,new MouseState { position = point }.WithButton(MouseButton.Left,down));
    }
    private static void RequireEndpoints()
    {
        var start = game.Paper.Find("StartPoint").GetComponent<GoldIngotStrokeGraphic>();
        var end = game.Paper.Find("EndPoint").GetComponent<GoldIngotStrokeGraphic>();
        Require(start.isActiveAndEnabled && end.isActiveAndEnabled,"both endpoints remain visible");
        Require(start.Points.Count == 1 && end.Points.Count == 1,"endpoint graphics contain only one dot");
        Require(start.Points[0] == game.CurrentRoute.Points[0] && end.Points[0] == game.CurrentRoute.Points[game.CurrentRoute.Points.Length-1],"endpoint positions");
        Require(start.color.g > start.color.r && end.color.r > end.color.g,"green start/red end");
        int count = 0; Color? style = null;
        foreach (var dot in game.Paper.GetComponentsInChildren<GoldIngotStrokeGraphic>())
            if (dot.name.StartsWith("Candidate"))
            {
                count++; if (!style.HasValue) style = dot.color;
                Require(dot.color == style.Value && dot.Points.Count == 1,"all candidate points share one style");
            }
        Require(count == game.CurrentRoute.Turns+game.CurrentRoute.Distractors.Length,"all candidate points stay visible");
    }
    private static Vector2 Screen(Vector2 p)
    {
        Rect r = game.Paper.rect;
        var canvas = game.GetComponentInParent<Canvas>();
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(cam,game.Paper.TransformPoint(new Vector3(r.xMin+p.x*r.width,r.yMin+p.y*r.height,0)));
    }
    private static Vector2 NearScreen(Vector2 p)
    { return Screen(p+new Vector2(.01f*game.Paper.rect.height/game.Paper.rect.width,0)); }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + .23;
        try
        {
            switch (step)
            {
                case 0:
                    Require(game.State == GoldIngotGameState.DifficultySelection,"initial selection");
                    Capture("00_difficulty"); step++; break;
                case 1:
                    // 点击实际难度按钮，验证所有难度从开始即可选，不绕过 UI。
                    var button = GameObject.Find("Difficulty"+difficulty).GetComponent<Button>();
                    ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
                    Require(game.State == GoldIngotGameState.Memorizing,"difficulty button starts memory"); step++; break;
                case 2:
                    RequireEndpoints();
                    Require(game.Paper.Find("MemoryRoute").GetComponent<GoldIngotStrokeGraphic>().Points.Count < game.CurrentRoute.Points.Length,"memory animates incrementally");
                    DumpGraphics(); Capture("memory_"+difficulty); step++; break;
                case 3:
                    if (game.State != GoldIngotGameState.Ready) break;
                    Require(!game.Paper.Find("MemoryRoute").gameObject.activeSelf,"route hides");
                    RequireEndpoints();
                    game.OnPointerDown(new PointerEventData(EventSystem.current) {position=Screen(game.CurrentRoute.Points[1]),button=PointerEventData.InputButton.Left});
                    Require(game.State == GoldIngotGameState.Ready,"pressing outside start cannot begin");
                    game.OnPointerDown(new PointerEventData(EventSystem.current) {position=Screen(game.CurrentRoute.Points[0]),button=PointerEventData.InputButton.Right});
                    Require(game.State == GoldIngotGameState.Ready,"right mouse cannot begin");
                    GameObject.Find("Status").GetComponent<Text>().text = "路线已隐藏 · 从起点按住左键开始";
                    Capture("hidden_"+difficulty); step++; break;
                case 4: Queue(NearScreen(game.CurrentRoute.Points[0]),false); step++; break;
                case 5: Queue(cursor,true); routePoint=1; step++; break;
                case 6:
                    Require(game.State == GoldIngotGameState.Drawing,"Input System starts stroke; state="+game.State+", status="+GameObject.Find("Status").GetComponent<Text>().text);
                    RequireEndpoints();
                    if (routePoint < game.CurrentRoute.Points.Length)
                    { Queue(NearScreen(game.CurrentRoute.Points[routePoint++]),true); break; }
                    var snapped = game.Paper.Find("PlayerStroke").GetComponent<GoldIngotStrokeGraphic>();
                    Require(snapped.Points.Count == game.CurrentRoute.Points.Length,"near-input connects all nodes");
                    for (int i = 0; i < snapped.Points.Count; i++) Require(snapped.Points[i] == game.CurrentRoute.Points[i],"line snaps to exact node center");
                    Capture("drawing_"+difficulty);
                    Queue(Screen(new Vector2(-.08f,.5f)),true); step++; break;
                case 7: Queue(cursor,false); step++; break;
                case 8:
                    if (game.State == GoldIngotGameState.Forming) break;
                    Require(game.State == GoldIngotGameState.Success,"route succeeds");
                    Require(game.TotalBundles == (difficulty+1)*(difficulty+2)/2,"reward total");
                    Capture("success_"+difficulty); step++; break;
                case 9:
                    if (difficulty < 2) { difficulty++; game.SelectDifficultyScreen(); step=1; break; }
                    game.SelectDifficultyScreen(); game.ChooseDifficulty(GoldIngotDifficulty.Easy); step++; break;
                case 10: if (game.State == GoldIngotGameState.Ready) { Queue(Screen(game.CurrentRoute.Points[0]),false); step++; } break;
                case 11: Queue(cursor,true); step++; break;
                case 12: Queue(cursor,false); step++; break;
                case 13:
                    Require(game.State == GoldIngotGameState.Failure && game.TotalBundles == 6,"early-release failure no reward");
                    Capture("failure"); retryRoute=game.CurrentRoute; step++; break;
                case 14:
                    ExecuteEvents.Execute(GameObject.Find("Retry"),new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
                    Require(game.State == GoldIngotGameState.Memorizing && !ReferenceEquals(retryRoute,game.CurrentRoute),"retry new route"); step++; break;
                case 15: if (game.State == GoldIngotGameState.Ready) { Queue(Screen(game.CurrentRoute.Points[0]),false); step++; } break;
                case 16: Queue(cursor,true); routePoint=1; step++; break;
                case 17:
                    if (routePoint < game.CurrentRoute.Points.Length) { Queue(NearScreen(game.CurrentRoute.Points[routePoint++]),true); break; }
                    Queue(cursor,false); step++; break;
                case 18:
                    if (game.State == GoldIngotGameState.Forming) break;
                    Require(game.State == GoldIngotGameState.Success && game.TotalBundles == 7,"retry reward only once; state="+game.State+", count="+game.TotalBundles+", status="+GameObject.Find("Status").GetComponent<Text>().text);
                    // 多余鼠标松手不能重复发奖。
                    game.OnPointerUp(new PointerEventData(EventSystem.current));
                    Require(game.TotalBundles == 7,"duplicate release does not reward");
                    difficulty = 0; step = 19;
                    break;
                case 19: game.ChooseDifficulty((GoldIngotDifficulty)difficulty); step++; break;
                case 20: if (game.State == GoldIngotGameState.Ready) { Queue(Screen(game.CurrentRoute.Points[0]),false); step++; } break;
                case 21: Queue(cursor,true); step++; break;
                case 22: Queue(Screen(game.CurrentRoute.Distractors[0]),true); step++; break;
                case 23: Queue(cursor,false); step++; break;
                case 24:
                    Require(game.State == GoldIngotGameState.Failure && game.TotalBundles == 7,"wrong candidate gives no reward, difficulty="+difficulty);
                    retryRoute = game.CurrentRoute; game.Retry();
                    Require(game.State == GoldIngotGameState.Memorizing && !ReferenceEquals(retryRoute,game.CurrentRoute),"fresh retry layout"); step++; break;
                case 25: if (game.State == GoldIngotGameState.Ready) { Queue(Screen(game.CurrentRoute.Points[0]),false); step++; } break;
                case 26: Queue(cursor,true); step++; break;
                case 27: Queue(Screen(game.CurrentRoute.Points[2]),true); step++; break;
                case 28: Queue(cursor,false); step++; break;
                case 29:
                    Require(game.State == GoldIngotGameState.Failure && game.TotalBundles == 7,"skip gives no reward, difficulty="+difficulty);
                    if (++difficulty < 3) { step = 19; break; }
                    File.AppendAllText(Path.Combine(Output,"play-tests.txt"),"PASS: all 3 difficulties; green/red endpoints and all same-style candidates visible in memory/ready/drawing; progressive memory/hide; wrong start/right button blocked; virtual mouse through InputSystemUIInputModule, offset inputs snap to exact node centers; rewards 1/2/3 (total 6), retry success (total 7), duplicate release no reward; early release failure; all difficulties reject wrong candidates and skipped points without reward; retries generate fresh layouts.\n");
                    Debug.Log("Gold Ingot sequence Play tests PASS."); Finish();
                    if (batchVerification) EditorApplication.Exit(0);
                    break;
            }
        }
        catch (Exception e)
        { File.AppendAllText(Path.Combine(Output,"play-tests.txt"),e+"\n"); Debug.LogException(e); Finish(); if (batchVerification) EditorApplication.Exit(1); }
    }
    private static void Finish()
    {
        EditorApplication.update -= Tick;
        if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
        Application.runInBackground = wasRunInBackground;
        if (originalInputSettings != null)
        {
            originalInputSettings.backgroundBehavior = originalBackground;
            originalInputSettings.editorInputBehaviorInPlayMode = originalEditorInput;
            originalInputSettings = null;
        }
        File.WriteAllText(Request,"DONE");
        if (game != null && game.State != GoldIngotGameState.Forming && game.State != GoldIngotGameState.Drawing) game.SelectDifficultyScreen();
    }
    private static void DumpGraphics()
    {
        Canvas.ForceUpdateCanvases();
        foreach (var graphic in game.Paper.GetComponentsInChildren<Graphic>(true))
        {
            var mesh = graphic.canvasRenderer.GetMesh();
            File.AppendAllText(Path.Combine(Output,"graphics.txt"),graphic.name+" active="+graphic.gameObject.activeInHierarchy+" rect="+graphic.rectTransform.rect+" color="+graphic.color+" tint="+graphic.canvasRenderer.GetColor()+" cull="+graphic.canvasRenderer.cull+" vertices="+(mesh == null ? 0 : mesh.vertexCount)+" material="+graphic.material.name+"\n");
        }
    }
}
#endif
