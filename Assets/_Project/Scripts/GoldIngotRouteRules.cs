using System;
using System.Collections.Generic;
using UnityEngine;

public enum GoldIngotDifficulty { Easy, Normal, Hard }

public sealed class GoldIngotRoute
{
    public readonly Vector2[] Points;
    public readonly Vector2[] Distractors;
    public readonly float MemorySeconds, Tolerance;
    public readonly int Reward;
    public int Turns => Points.Length - 2;

    public GoldIngotRoute(Vector2[] points, float seconds, float tolerance, int reward, Vector2[] distractors = null)
    { Points = points; MemorySeconds = seconds; Tolerance = tolerance; Reward = reward; Distractors = distractors ?? Array.Empty<Vector2>(); }
}

/// <summary>归一化纸面坐标，路线数据不依赖 UI 像素或输入系统。</summary>
public static class GoldIngotRoutes
{
    public static GoldIngotRoute Create(GoldIngotDifficulty difficulty, System.Random random)
    {
        bool longer = random.Next(2) == 1;
        Vector2[] points;
        switch (difficulty)
        {
            case GoldIngotDifficulty.Easy:
                points = longer ? new[] { P(.16f,.2f), P(.16f,.78f), P(.52f,.78f), P(.52f,.3f), P(.84f,.3f) }
                    : new[] { P(.16f,.2f), P(.16f,.78f), P(.82f,.78f), P(.82f,.24f) };
                break;
            case GoldIngotDifficulty.Normal:
                points = longer ? new[] { P(.12f,.2f), P(.12f,.78f), P(.38f,.78f), P(.38f,.28f), P(.66f,.28f), P(.66f,.78f), P(.88f,.78f) }
                    : new[] { P(.12f,.2f), P(.12f,.78f), P(.38f,.78f), P(.38f,.28f), P(.7f,.28f), P(.88f,.76f) };
                break;
            default:
                points = longer ? new[] { P(.1f,.18f), P(.1f,.8f), P(.3f,.8f), P(.3f,.28f), P(.52f,.28f), P(.52f,.8f), P(.74f,.8f), P(.74f,.28f), P(.9f,.28f) }
                    : new[] { P(.1f,.18f), P(.1f,.8f), P(.3f,.8f), P(.3f,.28f), P(.52f,.28f), P(.52f,.8f), P(.8f,.8f), P(.9f,.22f) };
                break;
        }
        bool mirrorX = random.Next(2) == 1, mirrorY = random.Next(2) == 1;
        for (int i = 0; i < points.Length; i++)
            points[i] = P(mirrorX ? 1 - points[i].x : points[i].x, mirrorY ? 1 - points[i].y : points[i].y);
        return new GoldIngotRoute(points, difficulty == GoldIngotDifficulty.Easy ? 3.5f :
            difficulty == GoldIngotDifficulty.Normal ? 4.5f : 5.5f,
            difficulty == GoldIngotDifficulty.Easy ? .105f : difficulty == GoldIngotDifficulty.Normal ? .095f : .085f,
            (int)difficulty + 1);
    }
    private static Vector2 P(float x, float y) => new Vector2(x, y);
}

/// <summary>
/// 在线检查当前线段走廊和有序关键点。长输入段细分后检测，防止低帧率跳点/抄近路。
/// 距离单位为纸张高度，x 按纸张宽高比换算；同一难度不因分辨率而改变。
/// </summary>
[Serializable]
public sealed class GoldIngotJudgeSettings
{
    [Range(.5f,1.5f)] public float endpointToleranceMultiplier = 1;
    [Range(.5f,1.5f)] public float cornerPositionMultiplier = 1;
    [Range(10,60)] public float angleTolerance = 30;
    [Range(10,40)] public float minimumTurnAngle = 22;
    [Range(.004f,.03f)] public float sampleSpacing = .012f;
    [Range(0,4)] public int smoothingRadius = 1;
    [Range(.008f,.06f)] public float simplifyDistance = .025f;
    [Range(.2f,1.2f)] public float cornerMergeMultiplier = .9f;
    [Range(1.1f,2)] public float maximumLengthMultiplier = 1.65f;
    [Range(.5f,2)] public float backtrackMultiplier = 1.8f;
    [Range(.5f,1.5f)] public float shapeToleranceMultiplier = 1;
    [Range(0,1)] public float cornerWeight = .7f;
    [Range(.5f,.95f)] public float passScore = .72f;
    [Range(0,1)] public float minimumShapeScore = .45f;
}

public sealed class GoldIngotRouteJudge
{
    private readonly GoldIngotRoute route;
    private readonly float aspect;
    private readonly GoldIngotJudgeSettings settings;
    private readonly List<Vector2> trace = new List<Vector2>();
    public float CornerScore { get; private set; }
    public float ShapeScore { get; private set; }
    public float CombinedScore { get; private set; }
    private Vector2 previous;
    private int nextPoint;
    private float length, expectedLength, furthestAlong;
    public bool Valid { get; private set; } = true;
    public string Failure { get; private set; }
    public int VisitedPoints => nextPoint;

    public GoldIngotRouteJudge(GoldIngotRoute route, float aspect, Vector2 start, GoldIngotJudgeSettings settings = null)
    {
        this.route = route; this.aspect = Mathf.Max(.1f, aspect);
        this.settings = settings ?? new GoldIngotJudgeSettings();
        trace.Add(Metric(start));
        previous = start; nextPoint = 1;
        for (int i = 1; i < route.Points.Length; i++) expectedLength += Distance(route.Points[i-1], route.Points[i]);
        if (start.x < 0 || start.x > 1 || start.y < 0 || start.y > 1 ||
            Distance(start, route.Points[0]) > route.Tolerance * this.settings.endpointToleranceMultiplier) Fail("请从起点开始。");
    }
    public float Distance(Vector2 a, Vector2 b) => Metric(a - b).magnitude;
    private Vector2 Metric(Vector2 p) => new Vector2(p.x * aspect, p.y);
    private void Fail(string reason) { Valid = false; Failure = reason; }

    public bool Append(Vector2 point)
    {
        if (!Valid) return false;
        if (point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) { Fail("画笔离开了金纸。"); return false; }
        float distance = Distance(previous, point);
        length += distance;
        if (length > expectedLength * settings.maximumLengthMultiplier + route.Tolerance * 2) { Fail("路线绕得太远了。"); return false; }
        int samples = Mathf.Max(1, Mathf.CeilToInt(distance / (route.Tolerance * .3f)));
        for (int i = 1; i <= samples; i++)
        {
            Vector2 p = Vector2.Lerp(previous, point, (float)i / samples);
            if (nextPoint == route.Points.Length)
            {
                if (Distance(p, route.Points[nextPoint-1]) > route.Tolerance) { Fail("到达终点后松开即可。"); break; }
                continue;
            }
            Vector2 a = Metric(route.Points[nextPoint-1]), b = Metric(route.Points[nextPoint]);
            Vector2 v = b - a, q = Metric(p);
            float along = Vector2.Dot(q - a, v) / v.sqrMagnitude;
            if ((q - (a + v * Mathf.Clamp01(along))).magnitude > route.Tolerance)
            { Fail("路线偏离了记忆中的折痕。"); break; }
            if ((furthestAlong - along) * v.magnitude > route.Tolerance * settings.backtrackMultiplier)
            { Fail("需要按顺序一笔画完，不能折返。"); break; }
            furthestAlong = Mathf.Max(furthestAlong, along);
            if (Distance(p, route.Points[nextPoint]) <= route.Tolerance)
            { nextPoint++; furthestAlong = 0; }
        }
        previous = point;
        if (distance > .0001f) trace.Add(Metric(point));
        return Valid;
    }

    public bool Finish()
    {
        if (!Valid) return false;
        if (nextPoint < route.Points.Length || Distance(previous, route.Points[route.Points.Length-1]) > route.Tolerance * settings.endpointToleranceMultiplier)
        { Fail("还没画完就松手了。"); return false; }
        return MatchShape();
    }

    // 先按弧长统一采样再平滑，避免鼠标帧率改变角度计算。视觉轨迹仍使用原输入。
    private bool MatchShape()
    {
        var target = new List<Vector2>();
        foreach (Vector2 p in route.Points) target.Add(Metric(p));
        var smooth = Resample(trace, Mathf.Max(.004f, settings.sampleSpacing));
        var filtered = new List<Vector2>(smooth);
        int radius = Mathf.Clamp(settings.smoothingRadius,0,4);
        for (int i = 1; i < smooth.Count-1; i++)
        {
            Vector2 sum = Vector2.zero; float weight = 0;
            for (int j = Mathf.Max(0,i-radius); j <= Mathf.Min(smooth.Count-1,i+radius); j++)
            { float w = radius+1-Mathf.Abs(i-j); sum += smooth[j]*w; weight += w; }
            filtered[i] = sum / weight;
        }
        var simple = new List<Vector2> { filtered[0] };
        Simplify(filtered,0,filtered.Count-1,Mathf.Max(.004f,settings.simplifyDistance),simple);
        var corners = new List<Vector2>(); var angles = new List<float>();
        for (int i = 1; i < simple.Count-1; i++)
        {
            float angle = Vector2.SignedAngle(simple[i]-simple[i-1],simple[i+1]-simple[i]);
            if (Mathf.Abs(angle) < settings.minimumTurnAngle) continue;
            int end = i;
            // 圆滑转弯可能被简化成相邻的小转弯，将同方向的近邻合并。
            while (end+1 < simple.Count-1 && Vector2.Distance(simple[i],simple[end+1]) <= route.Tolerance*settings.cornerMergeMultiplier)
            {
                float next = Vector2.SignedAngle(simple[end+1]-simple[end],simple[end+2]-simple[end+1]);
                if (Mathf.Sign(next) != Mathf.Sign(angle) || Mathf.Abs(next) < 5) break;
                angle += next; end++;
            }
            if (Mathf.Abs(angle) >= 175) { Fail("路线出现绕圈或回头，请按折痕转向。"); return false; }
            corners.Add((simple[i]+simple[end])*.5f); angles.Add(angle); i = end;
        }
        if (corners.Count != route.Turns)
        { Fail("转折数量不对：请在每个折点转向，避免多余转弯。"); return false; }
        float quality = 0;
        for (int i = 0; i < corners.Count; i++)
        {
            float expected = Vector2.SignedAngle(target[i+1]-target[i],target[i+2]-target[i+1]);
            float positionError = Vector2.Distance(corners[i],target[i+1]);
            float positionTolerance = Mathf.Max(.001f,route.Tolerance*settings.cornerPositionMultiplier);
            if (positionError > positionTolerance) { Fail("转折位置不对，请在记忆中的折点附近转向。"); return false; }
            if (Mathf.Sign(angles[i]) != Mathf.Sign(expected)) { Fail("转弯方向不对，请留意向左还是向右。"); return false; }
            float angleError = Mathf.Abs(angles[i]-expected), tolerance = Mathf.Max(1,settings.angleTolerance);
            if (angleError > tolerance) { Fail("转折角度不对，请记住折痕的夹角。"); return false; }
            quality += 1-.5f*(Mathf.Pow(positionError/positionTolerance,2)+Mathf.Pow(angleError/tolerance,2));
        }
        CornerScore = quality / Mathf.Max(1,corners.Count);
        var targetSamples = Resample(target, Mathf.Max(.004f,settings.sampleSpacing));
        float squaredError = MeanSquaredDistance(filtered,target)+MeanSquaredDistance(targetSamples,filtered);
        float shapeTolerance = Mathf.Max(.001f,route.Tolerance*settings.shapeToleranceMultiplier);
        ShapeScore = Mathf.Clamp01(1-squaredError*.5f/(shapeTolerance*shapeTolerance));
        float cornerWeight = Mathf.Clamp01(settings.cornerWeight);
        CombinedScore = CornerScore*cornerWeight+ShapeScore*(1-cornerWeight);
        if (ShapeScore < settings.minimumShapeScore || CombinedScore < settings.passScore)
        { Fail("整体路线还不够接近，请记住折痕的形状。"); return false; }
        return true;
    }
    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 v = b-a;
        return Vector2.Distance(p,a+v*(v.sqrMagnitude < .0000001f ? 0 : Mathf.Clamp01(Vector2.Dot(p-a,v)/v.sqrMagnitude)));
    }
    private static float MeanSquaredDistance(List<Vector2> samples, List<Vector2> line)
    {
        float sum = 0;
        foreach (Vector2 p in samples)
        {
            float nearest = float.MaxValue;
            for (int j = 1; j < line.Count; j++) nearest = Mathf.Min(nearest,SegmentDistance(p,line[j-1],line[j]));
            sum += nearest*nearest;
        }
        return sum/Mathf.Max(1,samples.Count);
    }
    private static List<Vector2> Resample(List<Vector2> input, float spacing)
    {
        var output = new List<Vector2> { input[0] }; float carried = 0;
        for (int i = 1; i < input.Count; i++)
        {
            Vector2 a = input[i-1], b = input[i]; float distance = Vector2.Distance(a,b);
            while (carried+distance >= spacing)
            {
                float step = spacing-carried; a = Vector2.Lerp(a,b,step/distance);
                output.Add(a); distance = Vector2.Distance(a,b); carried = 0;
            }
            carried += distance;
        }
        if (Vector2.Distance(output[output.Count-1],input[input.Count-1]) > .00001f) output.Add(input[input.Count-1]);
        return output;
    }
    private static void Simplify(List<Vector2> input,int first,int last,float tolerance,List<Vector2> output)
    {
        float maximum = tolerance; int split = -1;
        for (int i = first+1; i < last; i++)
        { float d = SegmentDistance(input[i],input[first],input[last]); if (d > maximum) { maximum = d; split = i; } }
        if (split >= 0) { Simplify(input,first,split,tolerance,output); Simplify(input,split,last,tolerance,output); }
        else if (last > first) output.Add(input[last]);
    }
}
