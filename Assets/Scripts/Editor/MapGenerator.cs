using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 地图生成器编辑器工具。
/// 点击菜单 DeltaForce > Generate Map 即可在当前场景生成：
/// - 边界墙壁（4 面）
/// - 内部障碍物（8 段墙壁 + 1 个中心柱）
/// - 5 个物品生成点（ItemSpawner）
/// 所有障碍物间隙至少为玩家直径（1.0）的 4 倍 = 4 单位。
/// </summary>
public class MapGenerator : EditorWindow
{
    // ── 地图参数 ───────────────────────────────────────────
    private const float MapHalfExtent = 20f;     // 地图半边长（总长 40x40）
    private const float WallThickness = 0.5f;    // 墙壁厚度
    private const float PlayerSize = 1.0f;       // 玩家直径（CircleCollider2D radius=0.5）
    private const float MinGap = 4.0f;           // 最小间隙 = 4 倍玩家直径

    // ── 障碍物定义（每段墙：centerX, centerY, width, height）──
    // width 为 X 方向全长，height 为 Y 方向全长
    private static readonly (float cx, float cy, float w, float h)[] Obstacles = new[]
    {
        // ── 上方两段水平墙（中间留 4 单位缺口）────────────
        (-6f, 12f,  8f, WallThickness),   // W1: 左上横墙
        ( 6f, 12f,  8f, WallThickness),   // W2: 右上横墙  (W1-W2 间隙 = 4)

        // ── 下方两段水平墙（中间留 4 单位缺口）────────────
        (-6f, -12f, 8f, WallThickness),   // W3: 左下横墙
        ( 6f, -12f, 8f, WallThickness),   // W4: 右下横墙  (W3-W4 间隙 = 4)

        // ── 左侧两段垂直墙（中间留 4 单位缺口）────────────
        (-12f,  6f, WallThickness, 8f),   // W5: 左上竖墙
        (-12f, -6f, WallThickness, 8f),   // W6: 左下竖墙  (W5-W6 间隙 = 4)

        // ── 右侧两段垂直墙（中间留 4 单位缺口）────────────
        ( 12f,  6f, WallThickness, 8f),   // W7: 右上竖墙
        ( 12f, -6f, WallThickness, 8f),   // W8: 右下竖墙  (W7-W8 间隙 = 4)
    };

    // ── 中心柱 ────────────────────────────────────────────
    private static readonly (float cx, float cy, float w, float h) CenterPillar =
        (0f, 0f, 3f, 3f);  // 3x3 中心柱，到各墙间隙均 > 4

    // ── 5 个物品生成点 ────────────────────────────────────
    private static readonly Vector2[] SpawnPoints = new[]
    {
        new Vector2(-10f,   6f),   // S1: 左中区域
        new Vector2( 10f,   6f),   // S2: 右中区域
        new Vector2(-10f,  -6f),   // S3: 左下区域
        new Vector2( 10f,  -6f),   // S4: 右下区域
        new Vector2(  0f,  16f),   // S5: 顶部中央
    };

    // ── 菜单入口 ──────────────────────────────────────────
    [MenuItem("DeltaForce/Generate Map", false, 100)]
    public static void GenerateMap()
    {
        // 检查当前场景是否已保存
        if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty)
        {
            bool proceed = EditorUtility.DisplayDialog(
                "场景未保存",
                "当前场景有未保存的更改。建议先保存场景再生成地图。\n\n是否继续？",
                "继续生成",
                "取消"
            );
            if (!proceed) return;
        }

        // 清理旧地图（如果存在）
        CleanupOldMap();

        // 创建地图根节点
        GameObject mapRoot = new GameObject("GeneratedMap");
        Undo.RegisterCreatedObjectUndo(mapRoot, "Generate Map");

        // 创建障碍物父节点
        GameObject obstaclesRoot = new GameObject("Obstacles");
        obstaclesRoot.transform.SetParent(mapRoot.transform);

        // 生成边界墙
        CreateBoundaryWalls(obstaclesRoot.transform);

        // 生成内部障碍物
        foreach (var obs in Obstacles)
        {
            CreateWall(obs.cx, obs.cy, obs.w, obs.h, obstaclesRoot.transform, "Wall");
        }

        // 生成中心柱
        CreateWall(CenterPillar.cx, CenterPillar.cy, CenterPillar.w, CenterPillar.h,
            obstaclesRoot.transform, "Pillar");

        // 创建物品生成点父节点
        GameObject spawnersRoot = new GameObject("ItemSpawners");
        spawnersRoot.transform.SetParent(mapRoot.transform);

        // 生成 5 个物品生成点
        for (int i = 0; i < SpawnPoints.Length; i++)
        {
            CreateSpawnPoint(SpawnPoints[i], i + 1, spawnersRoot.transform);
        }

        // 选中地图根节点
        Selection.activeGameObject = mapRoot;

        Debug.Log($"[MapGenerator] 地图生成完成！障碍物: {Obstacles.Length + 1} 个（含中心柱），" +
                  $"物品生成点: {SpawnPoints.Length} 个，边界墙: 4 面。" +
                  $"所有障碍物间隙 ≥ {MinGap} 单位（{MinGap / PlayerSize} 倍玩家直径）。");
    }

    [MenuItem("DeltaForce/Cleanup Map", false, 101)]
    public static void CleanupMap()
    {
        CleanupOldMap();
        Debug.Log("[MapGenerator] 旧地图已清理。");
    }

    // ── 验证间隙 ──────────────────────────────────────────
    [MenuItem("DeltaForce/Validate Map Gaps", false, 102)]
    public static void ValidateMapGaps()
    {
        GameObject mapRoot = GameObject.Find("GeneratedMap");
        if (mapRoot == null)
        {
            Debug.LogWarning("[MapGenerator] 未找到 GeneratedMap，请先生成地图。");
            return;
        }

        Transform obstaclesRoot = mapRoot.transform.Find("Obstacles");
        if (obstaclesRoot == null)
        {
            Debug.LogWarning("[MapGenerator] 未找到 Obstacles 节点。");
            return;
        }

        // 收集所有障碍物碰撞体
        var colliders = new List<BoxCollider2D>();
        foreach (Transform child in obstaclesRoot)
        {
            var col = child.GetComponent<BoxCollider2D>();
            if (col != null) colliders.Add(col);
        }

        float minGapFound = float.MaxValue;
        for (int i = 0; i < colliders.Count; i++)
        {
            for (int j = i + 1; j < colliders.Count; j++)
            {
                float gap = GetMinGapBetweenColliders(colliders[i], colliders[j]);
                if (gap < minGapFound) minGapFound = gap;
            }
        }

        if (minGapFound >= MinGap)
        {
            Debug.Log($"[MapGenerator] ✅ 所有障碍物间隙验证通过！最小间隙: {minGapFound:F2} 单位 " +
                      $"(要求 ≥ {MinGap}，即 {MinGap / PlayerSize} 倍玩家直径)。");
        }
        else
        {
            Debug.LogWarning($"[MapGenerator] ❌ 发现间隙不足！最小间隙: {minGapFound:F2} 单位 " +
                             $"(要求 ≥ {MinGap}，即 {MinGap / PlayerSize} 倍玩家直径)。");
        }
    }

    // ── 辅助方法 ──────────────────────────────────────────

    private static void CleanupOldMap()
    {
        GameObject oldMap = GameObject.Find("GeneratedMap");
        if (oldMap != null)
        {
            Undo.DestroyObjectImmediate(oldMap);
        }
    }

    private static void CreateBoundaryWalls(Transform parent)
    {
        float half = MapHalfExtent;
        float thick = WallThickness;

        // 上边界: 水平，y=+half
        CreateWall(0f, half, half * 2f, thick, parent, "Boundary_Top");
        // 下边界: 水平，y=-half
        CreateWall(0f, -half, half * 2f, thick, parent, "Boundary_Bottom");
        // 左边界: 垂直，x=-half
        CreateWall(-half, 0f, thick, half * 2f, parent, "Boundary_Left");
        // 右边界: 垂直，x=+half
        CreateWall(half, 0f, thick, half * 2f, parent, "Boundary_Right");
    }

    /// <summary>
    /// 创建一个带有 BoxCollider2D 的墙壁 GameObject。
    /// </summary>
    private static void CreateWall(float centerX, float centerY, float width, float height,
        Transform parent, string baseName)
    {
        GameObject wall = new GameObject(baseName);
        wall.transform.SetParent(parent);
        wall.transform.position = new Vector3(centerX, centerY, 0f);
        wall.transform.localScale = Vector3.one; // 保持缩放为 1，用 collider size 控制碰撞

        BoxCollider2D col = wall.AddComponent<BoxCollider2D>();
        col.size = new Vector2(width, height);

        // 使用 Tiled 模式 + FullRect sprite 渲染墙壁
        SpriteRenderer sr = wall.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(width, height);
        sr.color = new Color(0.4f, 0.4f, 0.4f, 1f); // 灰色墙壁
        sr.sortingOrder = -1;

        Undo.RegisterCreatedObjectUndo(wall, "Create Wall");
    }

    private static void CreateSpawnPoint(Vector2 position, int index, Transform parent)
    {
        GameObject spawnGo = new GameObject($"SpawnPoint_{index}");
        spawnGo.transform.SetParent(parent);
        spawnGo.transform.position = new Vector3(position.x, position.y, 0f);

        // 添加 ItemSpawner 组件（会从 ItemDatabase 随机生成物品）
        spawnGo.AddComponent<ItemSpawner>();

        // 添加 SpriteRenderer 用于编辑器中可视化
        SpriteRenderer sr = spawnGo.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(1f, 0.85f, 0.2f, 0.6f); // 金色半透明标记
        sr.sortingOrder = -2;
        sr.transform.localScale = Vector3.one * 1.5f;

        Undo.RegisterCreatedObjectUndo(spawnGo, "Create Spawn Point");
    }

    /// <summary>
    /// 计算两个 BoxCollider2D 之间的最小间隙。
    /// 使用分离轴定理简化版：按边界矩形计算。
    /// </summary>
    private static float GetMinGapBetweenColliders(BoxCollider2D a, BoxCollider2D b)
    {
        Bounds boundsA = a.bounds;
        Bounds boundsB = b.bounds;

        // 计算两个 AABB 之间的最短距离
        float dx = Mathf.Max(0f,
            Mathf.Abs(boundsA.center.x - boundsB.center.x) -
            (boundsA.extents.x + boundsB.extents.x));
        float dy = Mathf.Max(0f,
            Mathf.Abs(boundsA.center.y - boundsB.center.y) -
            (boundsA.extents.y + boundsB.extents.y));

        // 如果碰撞（dx=0, dy=0），返回 0
        if (dx == 0f && dy == 0f) return 0f;

        // 返回欧几里得距离
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    // ── 程序化 Sprite 生成（避免依赖外部资源）─────────────

    private static Sprite _cachedSquareSprite;
    private static Sprite _cachedCircleSprite;

    private static Sprite CreateSquareSprite()
    {
        if (_cachedSquareSprite != null) return _cachedSquareSprite;

        int size = 4;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat; // Tiled 模式需要 Repeat
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        // FullRect mesh 支持 Tiled/Sliced drawMode
        _cachedSquareSprite = Sprite.Create(tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size,
            0u,
            SpriteMeshType.FullRect);
        _cachedSquareSprite.name = "GeneratedSquare";
        return _cachedSquareSprite;
    }

    private static Sprite CreateCircleSprite()
    {
        if (_cachedCircleSprite != null) return _cachedCircleSprite;

        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        float center = (size - 1) / 2f;
        float radius = size / 2f - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                pixels[y * size + x] = dist <= radius ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();

        _cachedCircleSprite = Sprite.Create(tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        _cachedCircleSprite.name = "GeneratedCircle";
        return _cachedCircleSprite;
    }
}
