using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class GoldIngotSequenceSettings
{
    [Range(1,12)] public int easyPoints = 6;
    [Range(1,12)] public int normalPoints = 8;
    [Range(1,12)] public int hardPoints = 10;
    [Range(0,6)] public int easyDistractors = 2;
    [Range(0,6)] public int normalDistractors = 3;
    [Range(0,6)] public int hardDistractors = 4;
    [Tooltip("各难度共用。距离以纸张高度为单位，适配 Canvas 缩放。")]
    [Range(.035f,.09f)] public float snapRadius = .065f;
    [Range(.05f,.12f)] public float attractionRadius = .095f;
    [Range(.15f,.3f)] public float minimumSpacing = .20f;
    [Range(2,15)] public float memorySeconds = 7;
    [Range(.3f,3)] public float failureRevealSeconds = 1;
    public int PointCount(GoldIngotDifficulty d) => Mathf.Clamp(d == GoldIngotDifficulty.Easy ? easyPoints : d == GoldIngotDifficulty.Normal ? normalPoints : hardPoints,1,12);
    public int DistractorCount(GoldIngotDifficulty d) => Mathf.Clamp(d == GoldIngotDifficulty.Easy ? easyDistractors : d == GoldIngotDifficulty.Normal ? normalDistractors : hardDistractors,0,6);
    public float Snap => Mathf.Clamp(snapRadius,.035f,.09f);
    public float Attraction => Mathf.Max(Snap,Mathf.Clamp(attractionRadius,.05f,.12f));
    public float Spacing => Mathf.Max(minimumSpacing,Attraction*2+.01f);
}

/// <summary>随机分布候选点，再搜索转向明显且不会误触其他点的有序路线。</summary>
public static class GoldIngotSequenceRoutes
{
    public static GoldIngotRoute Create(GoldIngotDifficulty difficulty,System.Random random,GoldIngotSequenceSettings settings,float aspect)
    {
        int count = settings.PointCount(difficulty), distractors = settings.DistractorCount(difficulty);
        aspect = Mathf.Max(.5f,aspect);
        float gap = settings.Spacing;
        for (int attempt = 0; attempt < 80; attempt++)
        {
            var nodes = new List<Vector2>();
            for (int trial = 0; trial < 6000 && nodes.Count < count+distractors+2; trial++)
            {
                Vector2 p = new Vector2(.09f+(float)random.NextDouble()*.82f,.12f+(float)random.NextDouble()*.76f);
                bool clear = true;
                foreach (Vector2 other in nodes) if (Distance(p,other,aspect) < gap) { clear = false; break; }
                if (clear) nodes.Add(p);
            }
            if (nodes.Count != count+distractors+2) break;
            // 起点和终点固定为此次随机集合中的前两点；其余点在搜索后分配角色。
            var path = new List<int> {0}; var used = new bool[nodes.Count]; used[0] = true;
            int budget = 10000;
            if (!Search(nodes,path,used,count,aspect,settings.Snap+.015f,random,ref budget)) continue;
            var points = new Vector2[path.Count];
            for (int i = 0; i < points.Length; i++) points[i] = nodes[path[i]];
            var decoys = new List<Vector2>();
            for (int i = 2; i < nodes.Count; i++) if (!used[i]) decoys.Add(nodes[i]);
            return new GoldIngotRoute(points,Mathf.Max(2,settings.memorySeconds),settings.Snap,(int)difficulty+1,decoys.ToArray());
        }
        // 不静默减少点数或缩小容差；Inspector 中不可行的间距组合给出明确反馈。
        throw new InvalidOperationException("点数与间距无法放入金纸，请减小 Minimum Spacing / Attraction Radius，或减少候选点数。");
    }
    private static bool Search(List<Vector2> nodes,List<int> path,bool[] used,int count,float aspect,float clearance,System.Random random,ref int budget)
    {
        if (--budget <= 0) return false;
        bool end = path.Count == count+1;
        var choices = new List<int>();
        if (end) choices.Add(1);
        else for (int i = 2; i < nodes.Count; i++) if (!used[i]) choices.Add(i);
        for (int i = choices.Count-1; i > 0; i--) { int j = random.Next(i+1); int temp = choices[i]; choices[i] = choices[j]; choices[j] = temp; }
        int last = path[path.Count-1];
        foreach (int candidate in choices)
        {
            Vector2 a = Metric(nodes[last],aspect), b = Metric(nodes[candidate],aspect);
            if (path.Count > 1)
            {
                Vector2 incoming = a-Metric(nodes[path[path.Count-2]],aspect);
                float angle = Mathf.Abs(Vector2.SignedAngle(incoming,b-a));
                if (angle < 35 || angle > 155) continue;
            }
            bool clear = true;
            for (int i = 0; i < nodes.Count; i++)
                if (i != last && i != candidate && SegmentDistance(Metric(nodes[i],aspect),a,b) < clearance) { clear = false; break; }
            if (!clear) continue;
            path.Add(candidate); used[candidate] = true;
            if (end || Search(nodes,path,used,count,aspect,clearance,random,ref budget)) return true;
            path.RemoveAt(path.Count-1); used[candidate] = false;
        }
        return false;
    }
    public static Vector2 Metric(Vector2 p,float aspect) => new Vector2(p.x*aspect,p.y);
    public static float Distance(Vector2 a,Vector2 b,float aspect) => Metric(a-b,aspect).magnitude;
    public static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
    { Vector2 v = b-a; return Vector2.Distance(p,a+v*(v.sqrMagnitude < .0000001f ? 0 : Mathf.Clamp01(Vector2.Dot(p-a,v)/v.sqrMagnitude))); }
}

/// <summary>检测鼠标移动段首先接触哪个候选圆；只判顺序，不判轨迹形状或转角。</summary>
public sealed class GoldIngotSequenceJudge
{
    private readonly GoldIngotRoute route;
    private readonly float aspect;
    private Vector2 previous;
    public bool Valid { get; private set; } = true;
    public string Failure { get; private set; }
    public int ConnectedCount { get; private set; } = 1;
    public bool Complete => ConnectedCount == route.Points.Length;
    public GoldIngotSequenceJudge(GoldIngotRoute route,float aspect,Vector2 start)
    {
        this.route = route; this.aspect = aspect; previous = start;
        if (!Inside(start) || Distance(start,route.Points[0]) > route.Tolerance) Fail("请从绿色起点附近按下左键。");
    }
    public float Distance(Vector2 a,Vector2 b) => GoldIngotSequenceRoutes.Distance(a,b,aspect);
    private static bool Inside(Vector2 p) => p.x >= 0 && p.x <= 1 && p.y >= 0 && p.y <= 1;
    private void Fail(string reason) { Valid = false; Failure = reason; }
    public bool Append(Vector2 point)
    {
        if (!Valid) return false;
        // 完成后锁定连接结果，后续移动不再取消已连好的路线。
        if (Complete) return true;
        if (!Inside(point)) { Fail("画笔离开了金纸，请重试。"); return false; }
        Vector2 a = GoldIngotSequenceRoutes.Metric(previous,aspect), b = GoldIngotSequenceRoutes.Metric(point,aspect);
        float from = 0;
        // 长移动段按接触时间排序，低帧率也不会跳过错误点；每个节点最多处理一次。
        for (int scan = 0; scan < route.Points.Length+route.Distractors.Length; scan++)
        {
            float earliest = float.MaxValue; int hit = -1;
            for (int i = 0; i < route.Points.Length+route.Distractors.Length; i++)
            {
                if (i == ConnectedCount-1) continue;
                Vector2 node = i < route.Points.Length ? route.Points[i] : route.Distractors[i-route.Points.Length];
                float t = Entry(a,b,GoldIngotSequenceRoutes.Metric(node,aspect),route.Tolerance,from);
                if (t < earliest) { earliest = t; hit = i; }
            }
            if (hit < 0) break;
            if (hit != ConnectedCount)
            { Fail(hit >= route.Points.Length ? "连接了干扰点，请重试。" : "折点顺序不对，不能跳点或折返。"); return false; }
            ConnectedCount++; from = earliest+.00001f;
            if (Complete) break;
            if (from > 1) break;
        }
        previous = point; return Valid;
    }
    private static float Entry(Vector2 a,Vector2 b,Vector2 center,float radius,float from)
    {
        Vector2 v = b-a, q = a-center;
        if ((Vector2.LerpUnclamped(a,b,from)-center).sqrMagnitude <= radius*radius) return from;
        float aa = v.sqrMagnitude;
        if (aa < .00000001f) return float.MaxValue;
        float bb = Vector2.Dot(q,v), cc = q.sqrMagnitude-radius*radius, discriminant = bb*bb-aa*cc;
        if (discriminant < 0) return float.MaxValue;
        float t = (-bb-Mathf.Sqrt(discriminant))/aa;
        return t >= from && t <= 1 ? t : float.MaxValue;
    }
    public bool Finish()
    {
        if (!Valid) return false;
        if (!Complete)
        { Fail("还没按顺序连到红色终点就松手了。"); return false; }
        return true;
    }
}
