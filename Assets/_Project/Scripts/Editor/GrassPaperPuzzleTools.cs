#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>草纸谜题：一键生成独立测试场景，不改动已有场景。</summary>
public static class GrassPaperPuzzleTools
{
    public const string ScenePath = "Assets/_Project/Scences/GrassPaper_V01.unity";

    [MenuItem("Tools/Grass Paper/Create or Open V0.1 Test Scene")]
    public static void CreateOrOpen()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("先退出 Play 模式。");
            return;
        }
        Scene active = EditorSceneManager.GetActiveScene();
        if (active.isDirty)
        {
            Debug.LogWarning("请先保存当前场景，避免丢失未保存的修改。");
            return;
        }
        if (System.IO.File.Exists(ScenePath))
        {
            EditorSceneManager.OpenScene(ScenePath);
            return;
        }

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            NewSceneMode.Single);
        GameObject canvasObject = new GameObject("Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280,720);
        scaler.matchWidthOrHeight = .5f;

        GameObject eventSystem = new GameObject("EventSystem",
            typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

        GameObject root = new GameObject("GrassPaperRoot",typeof(RectTransform),
            typeof(Image),typeof(GrassPaperPuzzleGame));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(canvas.transform,false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(.5f,.5f);

        if (!EditorSceneManager.SaveScene(scene,ScenePath))
            throw new Exception("无法保存草纸小游戏场景：" + ScenePath);
        AssetDatabase.Refresh();
        Selection.activeGameObject = root;
        Debug.Log("草纸 V0.1 独立场景已创建。打开 Game 视图，按 Play 试玩。");
    }

    [MenuItem("Tools/Grass Paper/Verify All 5 Puzzle Solutions")]
    public static void VerifyAllSolutions()
    {
        // 离线求解器求得的最短路径：逐步重放，确保目标可达。
        string[][] solutions =
        {
            new[] { "0L","0L","0U","0U","0U","0T",
                "1U","1U","1T","1R","2L","2L" },
            new[] { "0L","0L","0L","0U","0U","0U",
                "1R","0T","1T","2L","2T","2D","3D","0L" },
            new[] { "0U","0T","0L","0L","0L","1L","1U",
                "2L","2D","2D","2T","3D","3L","3T" },
            new[] { "0L","0U","1R","0T","1R","2R","2D","2D",
                "2T","3D","3D","1T","3L","3L","3D","3L","3T" },
            new[] { "0U","2D","2D","3L","4D","3D","0T","0U","0U",
                "0U","0T","0L","0L","0U","2L","2T","2D",
                "1L","4R","3T","4D" }
        };

        if (solutions.Length != GrassPaperPuzzleRules.Levels.Length)
            throw new Exception("关卡数量与测试路径数量不匹配。");

        for (int i = 0; i < solutions.Length; i++)
        {
            GrassPaperPuzzleRules game =
                new GrassPaperPuzzleRules(GrassPaperPuzzleRules.Levels[i]);

            string[] actions = solutions[i];
            if (actions.Length != game.Level.knownMinimumSteps)
                throw new Exception("关卡 " + (i+1) + " 参考步数不匹配。");

            for (int step = 0; step < actions.Length; step++)
            {
                string cmd = actions[step];
                int piece = cmd[0] - '0';
                char action = cmd[1];
                bool ok = action == 'T' ? game.TryRotate(piece)
                    : action == 'L' ? game.TrySlide(piece,-1,0)
                    : action == 'R' ? game.TrySlide(piece,1,0)
                    : action == 'U' ? game.TrySlide(piece,0,-1)
                    : action == 'D' && game.TrySlide(piece,0,1);
                if (!ok)
                    throw new Exception("关卡 " + (i+1) +
                        " 第 " + (step+1) + " 步不合法：" + cmd);
            }

            if (!game.IsSolved || game.MoveCount != actions.Length)
                throw new Exception("关卡 " + (i+1) + " 无法完成。");
            while (game.Undo()) { }
            if (game.MoveCount != 0 || game.IsSolved)
                throw new Exception("关卡 " + (i+1) + " 撤销未正确还原。");
            for (int p = 0; p < game.PieceCount; p++)
                if (!game.GetPose(p).SameAs(game.Level.pieces[p].initial))
                    throw new Exception("关卡 " + (i+1) + " 未还原初始布局。");
        }

        Debug.Log("草纸 V0.1：5/5 关通过路线验证与撤销测试。注意：尚需 Play 模式下手工测试 UI。");
    }
}
#endif
