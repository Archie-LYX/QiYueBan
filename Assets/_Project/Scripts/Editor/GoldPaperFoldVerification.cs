#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// 仅用于当前第一次折叠验收；不进入玩家构建，不保存或修改场景。
public static class GoldPaperFoldVerification
{
    private static GoldPaperFold fold;
    private static PointerEventData pointer;
    private static Vector2 press;
    private static Vector2 size;
    private static Vector3 position, scale;
    private static int step, completed;
    private static double next;
    private static string folder;
    private static Mouse testMouse;

    [InitializeOnLoadMethod]
    private static void RequestedVerification()
    {
        EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.playModeStateChanged += OnPlayMode;
        string request = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../../QiYueBan/review/first-fold/verify-request.txt"));
        if (File.Exists(request) && File.ReadAllText(request).Trim() == "RUN")
            EditorApplication.delayCall += () => {
                if (!EditorApplication.isPlaying) return;
                File.WriteAllText(request, "RUNNING");
                Run();
            };
    }
    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode) RequestedVerification();
    }

    [MenuItem("Tools/Gold Paper/Verify First Fold")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first.");
        fold = UnityEngine.Object.FindFirstObjectByType<GoldPaperFold>();
        if (fold == null) throw new Exception("GoldPaperFold not found.");
        folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../QiYueBan/review/first-fold"));
        Directory.CreateDirectory(folder);
        fold.ResetFold();
        Canvas.ForceUpdateCanvases();
        completed = 0;
        fold.onFirstFoldCompleted.AddListener(Completed);
        var rt = (RectTransform)fold.transform;
        size = rt.rect.size; position = rt.anchoredPosition3D; scale = rt.localScale;
        var canvas = fold.GetComponentInParent<Canvas>();
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Rect r = rt.rect;
        press = RectTransformUtility.WorldToScreenPoint(camera,
            rt.TransformPoint(new Vector3(r.center.x, r.yMin + r.height * 0.05f, 0)));
        pointer = new PointerEventData(EventSystem.current) { pointerId = 123,
            button = PointerEventData.InputButton.Left, position = press, pressPosition = press };
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        bool hit = hits.Exists(h => h.gameObject.GetComponent<GoldPaperFoldLayer>() != null);
        Assert(hit, "Lower edge reaches generated UI raycast layer");
        pointer.pointerPressRaycast = hits.Find(h => h.gameObject.GetComponent<GoldPaperFoldLayer>() != null);
        step = 0; next = EditorApplication.timeSinceStartup + 0.4;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        File.WriteAllText(Path.Combine(folder, "verification.txt"), "Started; lower-edge raycast PASS\n");
    }
    private static void Completed() { completed++; }
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
    }
    private static void StartDrag()
    {
        pointer.position = press; pointer.pressPosition = press;
        fold.OnBeginDrag(pointer);
        Assert(fold.IsDragging, "Lower-edge drag starts");
    }
    private static void Drag(float angle)
    {
        var rt = (RectTransform)fold.transform;
        Rect r = rt.rect;
        var canvas = fold.GetComponentInParent<Canvas>();
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 localPress;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, press, cam, out localPress);
        float travel = (1f - Mathf.Cos(angle * Mathf.Deg2Rad)) * 0.5f;
        pointer.position = RectTransformUtility.WorldToScreenPoint(cam,
            rt.TransformPoint(localPress + Vector2.up * (r.height * travel)));
        fold.OnDrag(pointer);
        Assert(Mathf.Abs(fold.FoldAngle - angle) < 0.1f, "Angle follows pointer");
    }
    private static void Capture(string name)
    {
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 0.6;
        try
        {
            switch (step++)
            {
                case 0: Capture("00_flat"); break;
                case 1: StartDrag(); Drag(60); break;
                case 2: Capture("01_front_60"); break;
                case 3: Drag(120); break;
                case 4: Capture("02_back_120"); break;
                case 5: fold.OnEndDrag(pointer); break;
                case 6:
                    Assert(fold.FoldAngle == 0 && !fold.IsFolded, "Insufficient drag returns flat");
                    StartDrag(); Drag(155); fold.OnEndDrag(pointer); break;
                case 7:
                    Assert(fold.FoldAngle == 180 && fold.IsFolded && completed == 1, "Complete once");
                    Capture("03_complete_180");
                    break;
                case 8:
                    fold.OnBeginDrag(pointer); Assert(!fold.IsDragging, "Completed fold locks");
                    var rt = (RectTransform)fold.transform;
                    Assert(rt.rect.size == size && rt.anchoredPosition3D == position && rt.localScale == scale,
                        "RectTransform size, position and scale unchanged");
                    fold.ResetFold(); StartDrag(); Drag(75); Drag(0); fold.OnEndDrag(pointer); break;
                case 9:
                    Assert(fold.FoldAngle == 0 && !fold.IsFolded, "Reverse drag recovers flat");
                    pointer.pressPosition = press + Vector2.up * (size.y * 0.6f);
                    fold.OnBeginDrag(pointer); Assert(!fold.IsDragging, "Wrong grab location rejected");
                    StartDrag(); pointer.position = press + Vector2.right * 40;
                    fold.OnDrag(pointer); Assert(fold.FoldAngle == 0, "Horizontal drag does not fold");
                    pointer.pointerId = 456; fold.OnDrag(pointer); fold.OnEndDrag(pointer);
                    Assert(fold.IsDragging, "Other pointer cannot end gesture");
                    pointer.pointerId = 123; fold.ResetFold();
                    fold.enabled = false;
                    var image = fold.GetComponent<Image>();
                    Assert(image == null || image.enabled, "Original image restored when disabled");
                    fold.enabled = true;
                    Assert(fold.transform.childCount == 4, "Re-enable does not duplicate render layers");
                    File.AppendAllText(Path.Combine(folder, "verification.txt"),
                        "PASS: drag 60/120/155, front/back frames, rollback, completion event once, completion lock, reverse drag, invalid grab, horizontal drag, pointer isolation, disable/enable, unchanged RectTransform.\n");
                    testMouse = InputSystem.AddDevice<Mouse>("GoldPaperVerificationMouse");
                    break;
                case 10:
                    InputSystem.QueueStateEvent(testMouse, new MouseState { position = press }); break;
                case 11:
                    InputSystem.QueueStateEvent(testMouse,
                        new MouseState { position = press }.WithButton(MouseButton.Left)); break;
                case 12:
                    var canvas = fold.GetComponentInParent<Canvas>();
                    var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    var rect = (RectTransform)fold.transform;
                    Vector2 local;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, press, cam, out local);
                    Vector2 destination = RectTransformUtility.WorldToScreenPoint(cam,
                        rect.TransformPoint(local + Vector2.up * rect.rect.height));
                    InputSystem.QueueStateEvent(testMouse,
                        new MouseState { position = destination }.WithButton(MouseButton.Left)); break;
                case 13:
                    Assert(fold.IsDragging && fold.FoldAngle > 135f, "Input System dispatches actual UI drag");
                    InputSystem.QueueStateEvent(testMouse,
                        new MouseState { position = testMouse.position.ReadValue() }); break;
                case 14:
                    Assert(fold.IsFolded && completed == 2, "Input System mouse release completes fold");
                    File.AppendAllText(Path.Combine(folder, "verification.txt"),
                        "PASS: virtual mouse input -> InputSystemUIInputModule -> generated Graphic raycast -> drag handlers -> completion.\n");
                    Debug.Log("GoldPaperFold verification PASS: " + folder);
                    Finish(); break;
            }
        }
        catch (Exception e)
        {
            File.AppendAllText(Path.Combine(folder, "verification.txt"), e + "\n");
            Debug.LogException(e); Finish();
        }
    }
    private static void Finish()
    {
        EditorApplication.update -= Tick;
        if (testMouse != null) { InputSystem.RemoveDevice(testMouse); testMouse = null; }
        if (fold != null) { fold.onFirstFoldCompleted.RemoveListener(Completed); fold.ResetFold(); }
    }
}
#endif

