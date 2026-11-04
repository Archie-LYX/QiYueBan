#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Generate a separate playable test scene without touching the existing shop or gold ingot scenes.</summary>
public static class WanGuanV04Tools
{
    public const string ScenePath = "Assets/_Project/Scences/WanGuan_V04.unity";

    [MenuItem("Tools/WanGuan/Create or Open V0.4 Test Scene")]
    public static void CreateScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("Exit Play Mode first.");
            return;
        }

        Scene active = EditorSceneManager.GetActiveScene();
        if (active.isDirty)
        {
            Debug.LogWarning("Save the current scene before generating the WanGuan test scene.");
            return;
        }

        if (System.IO.File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath);
            Selection.activeGameObject = GameObject.Find("WanGuanRoot");
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        var eventSystem = new GameObject("EventSystem", typeof(EventSystem),
            typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        var root = new GameObject("WanGuanRoot", typeof(RectTransform), typeof(Image),
            typeof(WanGuanEightDirectionGame));
        var rt = root.GetComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(.5f, .5f);

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new Exception("Could not save WanGuan scene.");
        AssetDatabase.Refresh();
        Selection.activeGameObject = root;
        Debug.Log("WanGuan V0.4 scene created: " + ScenePath + " — press Play to test.");
    }

    [MenuItem("Tools/WanGuan/Verify V0.4 Rules")]
    public static void VerifyRules()
    {
        for (int layer = 1; layer <= 4; layer++)
        {
            Vector2 d = WanGuanEightDirectionGame.DirectionForLayer(layer,
                new System.Random(123));
            if (Mathf.Abs(d.x * d.y) > 0.001f)
                throw new Exception("Diagonal movement before layer 5: " + layer);
        }

        Vector2 fifth = WanGuanEightDirectionGame.DirectionForLayer(5,
            new System.Random(123));
        if (Mathf.Abs(fifth.x) < .1f || Mathf.Abs(fifth.y) < .1f)
            throw new Exception("Layer 5 should introduce diagonal movement.");

        Rect a = new Rect(-50, -50, 100, 100);
        Rect b = new Rect(-30, -10, 100, 100);
        Rect r = WanGuanEightDirectionGame.CalculateOverlap(a, b);
        if (Mathf.Abs(r.width - 80) > .001f ||
            Mathf.Abs(r.height - 60) > .001f ||
            Mathf.Abs(r.xMin + 30) > .001f ||
            Mathf.Abs(r.yMin + 10) > .001f)
            throw new Exception("2D rectangle intersection is incorrect.");

        Rect none = WanGuanEightDirectionGame.CalculateOverlap(a,
            new Rect(200, 200, 100, 100));
        if (none.width != 0 || none.height != 0)
            throw new Exception("Non-overlap did not return an empty rect.");

        Debug.Log("WanGuan V0.4 basic rule checks PASS (not a Play Mode test).");
    }
}
#endif
