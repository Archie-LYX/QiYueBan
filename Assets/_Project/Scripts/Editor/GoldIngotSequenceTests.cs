#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GoldIngotSequenceTests
{
    [MenuItem("Tools/Gold Ingot/Test Sequence Rules")]
    public static void Run()
    {
        var settings = new GoldIngotSequenceSettings(); int cases = 0;
        var unique = new HashSet<string>();
        for (int d = 0; d < 3; d++)
        for (int seed = 0; seed < 32; seed++)
        foreach (float aspect in new[] {1.5f,640f/330,2.2f})
        {
            var route = GoldIngotSequenceRoutes.Create((GoldIngotDifficulty)d,new System.Random(seed),settings,aspect);
            Require(route.Turns == 6+2*d && route.Distractors.Length == 2+d,"point counts");
            Require(route.MemorySeconds == 7 && route.Tolerance == settings.Snap && route.Reward == d+1,"shared settings/reward");
            var all = new List<Vector2>(route.Points); all.AddRange(route.Distractors);
            for (int i = 0; i < all.Count; i++) for (int j = i+1; j < all.Count; j++)
                Require(GoldIngotSequenceRoutes.Distance(all[i],all[j],aspect) >= settings.Spacing-.0001f,"spacing/no attraction overlap");
            for (int i = 1; i < route.Points.Length; i++)
            {
                Vector2 a = GoldIngotSequenceRoutes.Metric(route.Points[i-1],aspect), b = GoldIngotSequenceRoutes.Metric(route.Points[i],aspect);
                foreach (Vector2 point in all)
                    if (point != route.Points[i-1] && point != route.Points[i])
                        Require(GoldIngotSequenceRoutes.SegmentDistance(GoldIngotSequenceRoutes.Metric(point,aspect),a,b) >= settings.Snap+.0149f,"safe correct edges");
            }
            var exact = new GoldIngotSequenceJudge(route,aspect,route.Points[0]);
            for (int i = 1; i < route.Points.Length; i++) Require(exact.Append(route.Points[i]),"correct order: "+exact.Failure);
            Require(exact.Finish(),"complete route");
            var near = new GoldIngotSequenceJudge(route,aspect,route.Points[0]+new Vector2(.015f/aspect,0));
            for (int i = 1; i < route.Points.Length; i++) Require(near.Append(route.Points[i]+new Vector2(.006f/aspect,.004f)),"near-point snap");
            Require(near.Finish(),"snap tolerance");
            var wrong = new GoldIngotSequenceJudge(route,aspect,route.Points[0]);
            Require(!wrong.Append(route.Distractors[0]),"distractor rejects");
            var skip = new GoldIngotSequenceJudge(route,aspect,route.Points[0]);
            Require(!skip.Append(route.Points[2]),"skip/order rejects");
            var early = new GoldIngotSequenceJudge(route,aspect,route.Points[0]);
            early.Append(route.Points[1]); Require(!early.Finish(),"early release rejects");
            var end = new GoldIngotSequenceJudge(route,aspect,route.Points[0]);
            Require(!end.Append(route.Points[route.Points.Length-1]),"premature endpoint rejects");
            var start = new GoldIngotSequenceJudge(route,aspect,route.Points[1]); Require(!start.Valid,"wrong start rejects");
            string signature = "";
            foreach (Vector2 point in route.Points) signature += point.ToString("F5");
            unique.Add(signature); cases++;
        }
        Require(unique.Count >= 96,"fresh randomized layouts");
        // 低帧率同时跨过正确点和干扰点，也必须按实际接触时间处理。
        var synthetic = new GoldIngotRoute(new[] {P(.1f,.2f),P(.3f,.2f),P(.5f,.2f),P(.9f,.2f)},7,.06f,1,new[] {P(.7f,.2f)});
        var swept = new GoldIngotSequenceJudge(synthetic,1,P(.1f,.2f));
        Require(!swept.Append(P(.9f,.2f)) && swept.ConnectedCount == 3,"swept collision order");
        var clean = new GoldIngotRoute(synthetic.Points,7,.06f,1);
        var multi = new GoldIngotSequenceJudge(clean,1,P(.1f,.2f));
        Require(multi.Append(P(.9f,.2f)) && multi.Finish(),"one long segment can connect in-order collinear points");
        var away = new GoldIngotSequenceJudge(clean,1,P(.1f,.2f)); away.Append(P(.9f,.2f)); away.Append(P(.9f,.5f));
        Require(away.Finish(),"connected endpoint remains complete after moving away");
        Require(away.Append(P(-.2f,1.2f)) && away.Finish(),"completed route can release outside paper");
        Require(away.Append(clean.Points[0]) && away.Finish(),"completed route ignores later node contacts");
        var overshootRoute = new GoldIngotRoute(clean.Points,7,.02f,1,new[] {P(.97f,.2f)});
        var overshoot = new GoldIngotSequenceJudge(overshootRoute,1,P(.1f,.2f));
        Require(overshoot.Append(P(1,.2f)) && overshoot.Finish(),"endpoint crossed before later distractor stays complete");
        var curved = new GoldIngotSequenceJudge(clean,1,P(.1f,.2f));
        foreach (Vector2 point in new[] {P(.1f,.6f),P(.3f,.2f),P(.5f,.6f),P(.5f,.2f),P(.9f,.6f),P(.9f,.2f)})
            Require(curved.Append(point),"free trajectory between ordered nodes");
        Require(curved.Finish(),"no angle or similarity scoring");
        string output = Path.GetFullPath(Path.Combine(Application.dataPath,"../../../QiYueBan/review/ingot-sequence"));
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"rule-tests.txt"),"PASS: "+cases+" layouts (3 difficulties x 32 seeds x 3 aspect ratios): 6/8/10 points, 2/3/4 distractors, spacing, safe edges, randomized layouts, shared memory/snap, exact and near-point success, wrong/start/skip/order/end/early-release failure; swept input and endpoint release checks.\n");
        Debug.Log("Gold Ingot sequence rules PASS: "+cases);
    }
    private static Vector2 P(float x,float y) => new Vector2(x,y);
    private static void Require(bool value,string message) { if (!value) throw new Exception("Sequence FAIL: "+message); }
}
#endif
