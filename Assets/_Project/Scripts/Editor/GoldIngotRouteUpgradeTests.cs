#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>仅编辑器验收；不进入构建、不修改场景或游戏数据。</summary>
public static class GoldIngotRouteUpgradeTests
{
    private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,"../../../QiYueBan/review/ingot-route-upgrade"));
    [InitializeOnLoadMethod]
    private static void Initialize() { EditorApplication.update -= Check; EditorApplication.update += Check; }
    private static void Check()
    {
        string request = Path.Combine(Output,"request.txt");
        if (EditorApplication.isCompiling || !File.Exists(request) || File.ReadAllText(request).Trim() != "RULES") return;
        File.WriteAllText(request,"RUNNING");
        try { GoldIngotV01Tools.RunRuleTests(); Run(); File.WriteAllText(request,"PASS"); }
        catch (Exception e) { File.WriteAllText(request,"FAIL: "+e); Debug.LogException(e); }
    }
    [MenuItem("Tools/Gold Ingot/Test Endpoint and Turn Rules")]
    public static void Run()
    {
        var results = new List<string>();
        for (int d = 0; d < 3; d++)
        for (int seed = 0; seed < 64; seed++)
        foreach (float aspect in new[] {1.5f,640f/330,2.2f})
        {
            var route = GoldIngotRoutes.Create((GoldIngotDifficulty)d,new System.Random(seed));
            var dense = new List<Vector2> {route.Points[0]};
            for (int i = 1; i < route.Points.Length; i++)
                for (int j = 1; j <= 40; j++)
                    dense.Add(Vector2.Lerp(route.Points[i-1],route.Points[i],j/40f)+
                        (j == 40 ? Vector2.zero : new Vector2(Mathf.Sin(j*2.3f)*.002f/aspect,Mathf.Cos(j*1.7f)*.002f)));
            Expect(route,aspect,dense,true,null,"dense jitter",results,false);
            var rounded = new List<Vector2> {route.Points[0]};
            for (int i = 1; i < route.Points.Length-1; i++)
            {
                Vector2 corner = route.Points[i];
                Vector2 a = corner+(route.Points[i-1]-corner).normalized*.025f;
                Vector2 b = corner+(route.Points[i+1]-corner).normalized*.025f;
                rounded.Add(a);
                for (int j = 1; j <= 8; j++)
                { float t = j/8f; rounded.Add((1-t)*(1-t)*a+2*(1-t)*t*corner+t*t*b); }
            }
            rounded.Add(route.Points[route.Points.Length-1]);
            Expect(route,aspect,rounded,true,null,"rounded turns",results,false);
        }
        results.Add("PASS: 576 generated routes with dense jitter + rounded turns (1152 cases).");
        var r = new GoldIngotRoute(new[] {P(.2f,.2f),P(.2f,.8f),P(.8f,.8f),P(.8f,.2f)},3.5f,.16f,1);
        Expect(r,1,new[] {P(.5f,.2f),r.Points[1],r.Points[2],r.Points[3]},false,null,"wrong start",results);
        Expect(r,1,new[] {r.Points[0],r.Points[1]},false,null,"early release",results);
        Expect(r,1,new[] {r.Points[0],r.Points[1],r.Points[2],P(.8f,.4f)},false,null,"missing endpoint",results);
        Expect(r,1,new[] {r.Points[0],r.Points[3]},false,null,"straight shortcut",results);
        Expect(r,1,new[] {r.Points[0],r.Points[2],r.Points[1],r.Points[3]},false,null,"wrong order",results);
        var shifted = new[] {r.Points[0],P(.3f,.7f),r.Points[2],r.Points[3]};
        Expect(r,1,shifted,false,new GoldIngotJudgeSettings {cornerPositionMultiplier=.5f},"corner position",results);
        Expect(r,1,shifted,false,new GoldIngotJudgeSettings {angleTolerance=10},"corner angle",results);
        Expect(r,1,new[] {r.Points[0],P(.2f,.4f),P(.26f,.46f),P(.14f,.52f),P(.2f,.6f),r.Points[1],r.Points[2],r.Points[3]},false,null,"extra turns",results);
        Expect(r,1,new[] {r.Points[0],P(.2f,.5f),P(.28f,.5f),P(.28f,.58f),P(.2f,.58f),P(.2f,.5f),r.Points[1],r.Points[2],r.Points[3]},false,null,"loop",results);
        Expect(r,1,new[] {r.Points[0],P(.24f,.76f),P(.76f,.76f),r.Points[3]},false,
            new GoldIngotJudgeSettings {shapeToleranceMultiplier=.1f},"shape score gate",results);
        var exact = Expect(r,1,r.Points,true,null,"70/30 score",results);
        if (Mathf.Abs(exact.CombinedScore-(exact.CornerScore*.7f+exact.ShapeScore*.3f)) > .00001f) throw new Exception("Score weights");
        // 单独验证几何匹配层：在线走廊会更早拒绝这些轨迹，因此直接提供分析输入。
        var geometryRoute = new GoldIngotRoute(r.Points,3.5f,1,1);
        GeometryReject(geometryRoute,new[] {P(.2f,.2f),P(.2f,.8f),P(.05f,.8f),P(.05f,.2f)},"方向",results);
        GeometryReject(geometryRoute,new[] {r.Points[0],r.Points[3]},"转折数量",results);
        Directory.CreateDirectory(Output); File.WriteAllLines(Path.Combine(Output,"rule-tests.txt"),results);
        Debug.Log("Gold Ingot endpoint and turn rules PASS.");
    }
    private static Vector2 P(float x,float y) => new Vector2(x,y);
    private static void GeometryReject(GoldIngotRoute route,Vector2[] points,string reason,List<string> results)
    {
        var judge = new GoldIngotRouteJudge(route,1,points[0],new GoldIngotJudgeSettings {cornerMergeMultiplier=.01f});
        var trace = (List<Vector2>)typeof(GoldIngotRouteJudge).GetField("trace",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(judge);
        trace.Clear(); trace.AddRange(points);
        bool passed = (bool)typeof(GoldIngotRouteJudge).GetMethod("MatchShape",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(judge,null);
        if (passed || !judge.Failure.Contains(reason)) throw new Exception("Geometry gate: "+reason+"; "+judge.Failure);
        results.Add("PASS: isolated geometry gate "+reason+"; "+judge.Failure);
    }
    private static GoldIngotRouteJudge Expect(GoldIngotRoute route,float aspect,IEnumerable<Vector2> points,bool expected,
        GoldIngotJudgeSettings settings,string name,List<string> results,bool log = true)
    {
        var list = new List<Vector2>(points); var judge = new GoldIngotRouteJudge(route,aspect,list[0],settings);
        for (int i = 1; i < list.Count && judge.Valid; i++) judge.Append(list[i]);
        bool actual = judge.Finish();
        if (actual != expected) throw new Exception(name+" expected="+expected+" reason="+judge.Failure+" score="+judge.CombinedScore);
        if (log) results.Add("PASS: "+name+"; "+(actual ? "accepted" : judge.Failure));
        return judge;
    }
}
#endif
