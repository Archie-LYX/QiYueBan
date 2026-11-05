using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>纯规则：离散格子、滑动/旋转、碰撞、撤销、胜利判断。与 UI 完全分离。</summary>
public struct GrassPaperPose
{
    public int x;
    public int y;
    public bool turned;

    public GrassPaperPose(int x, int y, bool turned = false)
    {
        this.x = x;
        this.y = y;
        this.turned = turned;
    }

    public bool SameAs(GrassPaperPose other)
    {
        return x == other.x && y == other.y && turned == other.turned;
    }
}

public sealed class GrassPaperPieceSpec
{
    public readonly string name;
    public readonly int width;
    public readonly int height;
    public readonly GrassPaperPose initial;
    public readonly GrassPaperPose target;

    public GrassPaperPieceSpec(string name, int width, int height,
        GrassPaperPose initial, GrassPaperPose target)
    {
        this.name = name;
        this.width = width;
        this.height = height;
        this.initial = initial;
        this.target = target;
    }
}

public sealed class GrassPaperLevel
{
    public readonly string title;
    public readonly string subtitle;
    public readonly int width;
    public readonly int height;
    public readonly int knownMinimumSteps;
    public readonly Vector2Int[] stones;
    public readonly GrassPaperPieceSpec[] pieces;

    public GrassPaperLevel(string title, string subtitle, int width, int height,
        int knownMinimumSteps, Vector2Int[] stones, params GrassPaperPieceSpec[] pieces)
    {
        this.title = title;
        this.subtitle = subtitle;
        this.width = width;
        this.height = height;
        this.knownMinimumSteps = knownMinimumSteps;
        this.stones = stones;
        this.pieces = pieces;
    }
}

public sealed class GrassPaperPuzzleRules
{
    // 初始和目标布局均经过离线搜索验证；Editor 测试还会逐步重放解法。
    public static readonly GrassPaperLevel[] Levels =
    {
        new GrassPaperLevel("01 · 留出空位", "先观察：哪张草纸挡住了去路？",
            5, 5, 12,
            new[] { new Vector2Int(2,0), new Vector2Int(2,1), new Vector2Int(3,3) },
            P("A",2,1, 2,3,true,  0,0,false),
            P("B",1,2, 3,2,true,  4,0,false),
            P("C",2,1, 3,4,false, 1,4,false)
        ),
        new GrassPaperLevel("02 · 换个方向", "先腾出空间，才能旋转长纸。",
            6, 5, 14,
            new[] { new Vector2Int(2,1), new Vector2Int(2,2),
                new Vector2Int(4,2), new Vector2Int(0,3) },
            P("A",2,1, 4,3,true,  0,0,false),
            P("B",1,3, 2,0,true,  3,0,false),
            P("C",2,1, 5,3,true,  4,4,false),
            P("D",1,2, 0,0,false, 0,1,false)
        ),
        new GrassPaperLevel("03 · 错位让路", "并不是离目标最近的纸，要先动。",
            6, 6, 14,
            new[] { new Vector2Int(2,1), new Vector2Int(2,2),
                new Vector2Int(3,4), new Vector2Int(3,5),
                new Vector2Int(0,3), new Vector2Int(5,1) },
            P("A",2,1, 3,1,true,  0,0,false),
            P("B",1,3, 4,1,false, 3,0,false),
            P("C",2,1, 5,2,true,  4,4,false),
            P("D",1,2, 1,3,true,  0,4,false)
        ),
        new GrassPaperLevel("04 · 狭路回旋", "让开旋转位置，再把纸推回来。",
            6, 6, 17,
            new[] { new Vector2Int(2,1), new Vector2Int(2,2),
                new Vector2Int(3,4), new Vector2Int(3,5),
                new Vector2Int(0,3), new Vector2Int(5,1) },
            P("A",2,1, 1,1,true,  0,0,false),
            P("B",1,3, 1,0,true,  3,0,false),
            P("C",2,1, 3,2,true,  4,4,false),
            P("D",1,2, 3,1,true,  0,4,false)
        ),
        new GrassPaperLevel("05 · 纸柜归位", "五张纸、几块镇石。慢慢想，不限时间。",
            6, 6, 21,
            new[] { new Vector2Int(2,0), new Vector2Int(3,2),
                new Vector2Int(3,3), new Vector2Int(0,4),
                new Vector2Int(5,1), new Vector2Int(5,5) },
            P("A",2,1, 2,5,false, 0,0,false),
            P("B",1,3, 5,2,false, 4,2,false),
            P("C",2,1, 4,2,true,  3,5,false),
            P("D",1,2, 1,1,true,  0,2,false),
            P("E",2,1, 0,2,false, 1,4,false)
        )
    };

    private static GrassPaperPieceSpec P(string name, int w, int h,
        int ix, int iy, bool it, int tx, int ty, bool tt)
    {
        return new GrassPaperPieceSpec(name, w, h,
            new GrassPaperPose(ix, iy, it), new GrassPaperPose(tx, ty, tt));
    }

    private readonly Stack<GrassPaperPose[]> undo = new Stack<GrassPaperPose[]>();
    private GrassPaperPose[] poses;

    public GrassPaperLevel Level { get; private set; }
    public int MoveCount { get; private set; }
    public bool CanUndo => undo.Count > 0;
    public int PieceCount => poses.Length;
    public bool IsSolved
    {
        get
        {
            for (int i = 0; i < poses.Length; i++)
                if (!poses[i].SameAs(Level.pieces[i].target)) return false;
            return true;
        }
    }

    public GrassPaperPuzzleRules(GrassPaperLevel level)
    {
        Level = level ?? throw new ArgumentNullException(nameof(level));
        if (Level.pieces == null || Level.pieces.Length == 0)
            throw new ArgumentException("Puzzle needs pieces.");
        poses = new GrassPaperPose[Level.pieces.Length];
        for (int i = 0; i < poses.Length; i++) poses[i] = Level.pieces[i].initial;

        if (!ValidArrangement(poses)) throw new ArgumentException("Invalid starting layout.");
        var targets = new GrassPaperPose[poses.Length];
        for (int i = 0; i < targets.Length; i++) targets[i] = Level.pieces[i].target;
        if (!ValidArrangement(targets)) throw new ArgumentException("Invalid target layout.");
    }

    public GrassPaperPose GetPose(int index) => poses[index];

    public Vector2Int GetSize(int index, GrassPaperPose pose)
    {
        GrassPaperPieceSpec piece = Level.pieces[index];
        return pose.turned ? new Vector2Int(piece.height, piece.width)
            : new Vector2Int(piece.width, piece.height);
    }

    public bool TrySlide(int index, int dx, int dy)
    {
        if (!IndexOK(index) || Mathf.Abs(dx) + Mathf.Abs(dy) != 1)
            return false;
        GrassPaperPose next = poses[index];
        next.x += dx;
        next.y += dy;
        return TryCommit(index, next);
    }

    public bool TryRotate(int index)
    {
        if (!IndexOK(index)) return false;
        GrassPaperPieceSpec piece = Level.pieces[index];
        if (piece.width == piece.height) return false;

        // 围绕纸张左上角对应的格子进行 90° 旋转，不允许越界或穿越其他纸。
        GrassPaperPose next = poses[index];
        next.turned = !next.turned;
        return TryCommit(index, next);
    }

    public bool Undo()
    {
        if (!CanUndo) return false;
        poses = undo.Pop();
        MoveCount = Mathf.Max(0, MoveCount - 1);
        return true;
    }

    public void Reset()
    {
        undo.Clear();
        MoveCount = 0;
        for (int i = 0; i < poses.Length; i++) poses[i] = Level.pieces[i].initial;
    }

    private bool TryCommit(int index, GrassPaperPose next)
    {
        if (!CanPlace(index, next, poses)) return false;
        undo.Push((GrassPaperPose[])poses.Clone());
        poses[index] = next;
        MoveCount++;
        return true;
    }

    private bool ValidArrangement(GrassPaperPose[] arrangement)
    {
        for (int i = 0; i < arrangement.Length; i++)
            if (!CanPlace(i, arrangement[i], arrangement)) return false;
        return true;
    }

    private bool CanPlace(int index, GrassPaperPose next, GrassPaperPose[] all)
    {
        Vector2Int size = GetSize(index, next);
        if (next.x < 0 || next.y < 0 ||
            next.x + size.x > Level.width || next.y + size.y > Level.height)
            return false;

        foreach (Vector2Int stone in Level.stones)
            if (Contains(next, size, stone)) return false;

        for (int i = 0; i < all.Length; i++)
        {
            if (i == index) continue;
            GrassPaperPose other = all[i];
            Vector2Int otherSize = GetSize(i, other);
            if (next.x < other.x + otherSize.x &&
                next.x + size.x > other.x &&
                next.y < other.y + otherSize.y &&
                next.y + size.y > other.y)
                return false;
        }

        return true;
    }

    private static bool Contains(GrassPaperPose rect, Vector2Int size, Vector2Int cell)
    {
        return cell.x >= rect.x && cell.x < rect.x + size.x &&
            cell.y >= rect.y && cell.y < rect.y + size.y;
    }

    private bool IndexOK(int index) => index >= 0 && index < poses.Length;
}
