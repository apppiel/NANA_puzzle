using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;

// Tools > NANA > Level Solver
// 각 레벨의 정답 경로(Hamiltonian path) 개수를 세서 난이도 등급을 매김.
// Generator 섹션: 격자/시작점/구멍 개수/목표 정답 범위를 지정하면,
//                랜덤 배치를 solver로 검증하며 조건 맞는 후보만 뽑아줌.
public enum ShapeTemplate { Rectangle, Diamond, Cross, Hexagon }

public class LevelSolverWindow : EditorWindow
{
    // ── Solver 필드 ──
    LevelData targetLevel;
    Vector2 scrollPos;
    string singleResult = "";
    string singlePathPreview = "";
    readonly List<string> batchResults = new List<string>();

    // ── Generator 필드 ──
    int genWidth = 9;
    int genHeight = 8;
    Vector2Int genStart = Vector2Int.zero;
    int genBlockedCount = 15;
    int genMinPaths = 2;
    int genMaxPaths = 10;
    int genMaxAttempts = 500;
    int genNumCandidates = 5;
    ShapeTemplate genShape = ShapeTemplate.Rectangle;
    LevelData genTarget; // 채택 시 덮어쓸 대상 LevelData
    readonly List<Candidate> candidates = new List<Candidate>();
    Vector2 genScrollPos;

    const int MAX_PATHS = 100;
    const int TIMEOUT_MS = 30000;

    class Candidate
    {
        public List<Vector2Int> blockedCells;
        public int pathCount;
        public List<Vector2Int> firstPath; // 시각화용 정답 예시 하나
    }

    [MenuItem("Tools/NANA/Level Solver")]
    public static void ShowWindow() => GetWindow<LevelSolverWindow>("Level Solver");

    void OnGUI()
    {
        EditorGUILayout.LabelField("정답 경로 개수 카운터", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "100+ 쉬움 / 6~99 중간 / 2~5 어려움 / 1 극악 / 0 못 품 / T 30초 타임아웃",
            MessageType.None);
        EditorGUILayout.Space();

        // ── 단일 레벨 ──
        EditorGUILayout.LabelField("단일 레벨 검사", EditorStyles.miniBoldLabel);
        targetLevel = (LevelData)EditorGUILayout.ObjectField(
            "Level Data", targetLevel, typeof(LevelData), false);

        using (new EditorGUI.DisabledScope(targetLevel == null))
        {
            if (GUILayout.Button("이 레벨 풀어보기"))
            {
                var (count, timedOut) = CountPaths(targetLevel);
                singleResult = FormatResult(targetLevel.name, count, timedOut);
                var path = FindOnePath(targetLevel);
                singlePathPreview = path != null
                    ? PreviewGrid(targetLevel.width, targetLevel.height,
                                  targetLevel.blockedCells, path)
                    : "(정답 없음)";
            }
        }

        if (!string.IsNullOrEmpty(singleResult))
            EditorGUILayout.HelpBox(singleResult, MessageType.Info);

        if (!string.IsNullOrEmpty(singlePathPreview))
            EditorGUILayout.TextArea(singlePathPreview, GUILayout.ExpandHeight(false));

        EditorGUILayout.Space();

        // ── 일괄 스캔 ──
        EditorGUILayout.LabelField("전체 레벨 일괄 검사", EditorStyles.miniBoldLabel);
        if (GUILayout.Button("Assets/Levels 전체 스캔"))
            RunBatchScan();

        using (new EditorGUI.DisabledScope(batchResults.Count == 0))
        {
            if (GUILayout.Button("결과 클립보드 복사"))
                EditorGUIUtility.systemCopyBuffer = string.Join("\n", batchResults);
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MaxHeight(200));
        foreach (var line in batchResults)
            EditorGUILayout.LabelField(line);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        DrawSeparator();

        // ── Generator ──
        DrawGeneratorSection();
    }

    void DrawSeparator()
    {
        var r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, Color.gray);
        EditorGUILayout.Space();
    }

    void DrawGeneratorSection()
    {
        EditorGUILayout.LabelField("Level Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "격자/시작점/구멍 개수/목표 정답 범위 설정 후 [생성]. " +
            "랜덤 배치 → solver 검증 반복해서 조건 맞는 후보만 뽑음.",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            genWidth = EditorGUILayout.IntField("Width", genWidth);
            genHeight = EditorGUILayout.IntField("Height", genHeight);
        }
        genStart = EditorGUILayout.Vector2IntField("Start Cell", genStart);

        genShape = (ShapeTemplate)EditorGUILayout.EnumPopup("모양 템플릿", genShape);
        if (genShape != ShapeTemplate.Rectangle)
        {
            int baseCount = GetShapeBaseBlocks(genShape, genWidth, genHeight).Count;
            EditorGUILayout.LabelField(
                $"  → 모양 기본 구멍: {baseCount}개 (구멍 개수는 이보다 크거나 같아야 추가 배치됨)",
                EditorStyles.miniLabel);
        }

        genBlockedCount = EditorGUILayout.IntField("구멍 개수 (모양 포함 총합)", genBlockedCount);

        using (new EditorGUILayout.HorizontalScope())
        {
            genMinPaths = EditorGUILayout.IntField("목표 정답 Min", genMinPaths);
            genMaxPaths = EditorGUILayout.IntField("Max", genMaxPaths);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            genMaxAttempts = EditorGUILayout.IntField("최대 시도", genMaxAttempts);
            genNumCandidates = EditorGUILayout.IntField("뽑을 후보 수", genNumCandidates);
        }

        genTarget = (LevelData)EditorGUILayout.ObjectField(
            "적용 대상 LevelData", genTarget, typeof(LevelData), false);

        if (GUILayout.Button("후보 생성"))
            GenerateCandidates();

        EditorGUILayout.LabelField($"후보 {candidates.Count}개 찾음", EditorStyles.miniLabel);

        genScrollPos = EditorGUILayout.BeginScrollView(genScrollPos, GUILayout.MaxHeight(300));
        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"후보 #{i + 1} — 정답 {c.pathCount}개",
                EditorStyles.boldLabel);
            EditorGUILayout.TextArea(PreviewGrid(c), GUILayout.ExpandHeight(false));
            EditorGUILayout.LabelField(BlockedCellsToText(c),
                EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUI.DisabledScope(genTarget == null))
            {
                if (GUILayout.Button($"→ {(genTarget != null ? genTarget.name : "?")}에 적용 (덮어쓰기)"))
                    ApplyCandidate(c);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
        EditorGUILayout.EndScrollView();
    }

    void GenerateCandidates()
    {
        candidates.Clear();

        if (genWidth <= 0 || genHeight <= 0)
        {
            UnityEngine.Debug.LogError("Width/Height는 1 이상이어야 함");
            return;
        }
        if (genStart.x < 0 || genStart.x >= genWidth || genStart.y < 0 || genStart.y >= genHeight)
        {
            UnityEngine.Debug.LogError("시작점이 격자 범위 밖");
            return;
        }
        int usable = genWidth * genHeight - genBlockedCount;
        if (usable <= 0)
        {
            UnityEngine.Debug.LogError("구멍이 너무 많음 (사용칸 0 이하)");
            return;
        }

        var rng = new System.Random();
        int totalPositions = genWidth * genHeight - 1; // 시작점 제외

        // 모양 템플릿의 기본 구멍 (모양 밖 셀)
        var baseBlocks = GetShapeBaseBlocks(genShape, genWidth, genHeight);
        baseBlocks.RemoveAll(b => b == genStart); // 시작점이 모양 밖이면 강제 제거

        // 시작점이 모양 안에 있는지 확인
        if (GetShapeBaseBlocks(genShape, genWidth, genHeight).Contains(genStart))
        {
            UnityEngine.Debug.LogError(
                "시작점이 모양 밖입니다. 시작점을 모양 안쪽으로 옮겨주세요.");
            return;
        }

        int targetCount = Mathf.Min(Mathf.Max(genBlockedCount, baseBlocks.Count), totalPositions);

        // 임시 LevelData로 CountPaths 재사용
        var tempLevel = ScriptableObject.CreateInstance<LevelData>();
        tempLevel.width = genWidth;
        tempLevel.height = genHeight;
        tempLevel.startCell = genStart;

        var blocks = new List<Vector2Int>(targetCount);

        try
        {
            for (int a = 0; a < genMaxAttempts && candidates.Count < genNumCandidates; a++)
            {
                bool canceled = EditorUtility.DisplayCancelableProgressBar("Generator",
                    $"시도 {a + 1}/{genMaxAttempts}, 후보 {candidates.Count}/{genNumCandidates}",
                    (float)a / genMaxAttempts);
                if (canceled) break;

                // 구조적 배치: 모양 기본 구멍 + 클러스터로 추가 구멍
                PlaceBlocksClustered(blocks, targetCount, rng, baseBlocks);
                tempLevel.blockedCells = new List<Vector2Int>(blocks);

                // Generator는 목표 max+1개 도달 시 조기 종료 → 너무 쉬운 후보 즉시 폐기
                var (count, timedOut) = CountPaths(tempLevel, genMaxPaths + 1);
                if (timedOut) continue; // 타임아웃은 애매하므로 스킵

                if (count >= genMinPaths && count <= genMaxPaths)
                {
                    // 정답 존재 확인됐으니 예시 경로 하나 찾아서 저장
                    var path = FindOnePath(tempLevel);
                    candidates.Add(new Candidate
                    {
                        blockedCells = new List<Vector2Int>(blocks),
                        pathCount = count,
                        firstPath = path
                    });
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Object.DestroyImmediate(tempLevel);
        }

        if (candidates.Count == 0)
            UnityEngine.Debug.LogWarning("조건 맞는 후보 없음. 목표 범위를 넓히거나 시도 횟수를 늘려보세요.");
    }

    // 구조적 구멍 배치: 인접 배치 70%, 랜덤 30%
    // 벽/클러스터가 자연스럽게 형성 → 통로/구석 만들기 → 정답 좁혀지기 쉬움
    // initialBlocks: 모양 템플릿의 필수 구멍 (모양 밖 셀). 없으면 첫 구멍부터 랜덤 배치
    void PlaceBlocksClustered(List<Vector2Int> outBlocks, int targetCount,
                              System.Random rng, List<Vector2Int> initialBlocks = null)
    {
        outBlocks.Clear();
        var used = new HashSet<Vector2Int> { genStart };

        // 초기 구멍(모양 템플릿)이 있으면 먼저 추가
        if (initialBlocks != null)
            foreach (var b in initialBlocks)
                if (used.Add(b))
                    outBlocks.Add(b);

        // 첫 랜덤 구멍 (초기 구멍이 없을 때만)
        if (outBlocks.Count == 0)
        {
            var first = PickRandomEmpty(used, rng);
            if (first.x < 0) return;
            outBlocks.Add(first);
            used.Add(first);
        }

        // 추가 구멍: 70% 인접, 30% 랜덤
        while (outBlocks.Count < targetCount)
        {
            Vector2Int next = new Vector2Int(-1, -1);
            if (rng.NextDouble() < 0.7)
            {
                var basis = outBlocks[rng.Next(outBlocks.Count)];
                next = PickAdjacentEmpty(basis, used, rng);
            }
            if (next.x < 0) next = PickRandomEmpty(used, rng);
            if (next.x < 0) break; // 더 놓을 자리 없음

            outBlocks.Add(next);
            used.Add(next);
        }
    }

    // 모양 템플릿별 기본 구멍(모양 밖 셀) 계산
    List<Vector2Int> GetShapeBaseBlocks(ShapeTemplate shape, int W, int H)
    {
        var blocks = new List<Vector2Int>();
        if (shape == ShapeTemplate.Rectangle || W <= 0 || H <= 0) return blocks;

        // 격자 중심 좌표 (짝수 크기면 두 셀 사이)
        float cx = (W - 1) / 2f, cy = (H - 1) / 2f;

        switch (shape)
        {
            case ShapeTemplate.Diamond:
                // Manhattan 거리 > 반지름이면 밖. 반지름 = min(W,H)/2
                float radius = Mathf.Min(W, H) / 2f;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        if (Mathf.Abs(x - cx) + Mathf.Abs(y - cy) > radius)
                            blocks.Add(new Vector2Int(x, y));
                break;

            case ShapeTemplate.Cross:
                // 가운데 (armWidth*2+1) 열/행만 usable. armWidth는 격자 크기의 1/5 정도
                int armWidth = Mathf.Max(1, Mathf.Min(W, H) / 5);
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        // 중심 근처 열도 아니고 중심 근처 행도 아니면 밖
                        if (Mathf.Abs(x - cx) > armWidth && Mathf.Abs(y - cy) > armWidth)
                            blocks.Add(new Vector2Int(x, y));
                break;

            case ShapeTemplate.Hexagon:
                // 각 코너에서 대각선 컷. 컷 크기 = 격자의 1/4
                int cut = Mathf.Max(1, Mathf.Min(W, H) / 4);
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        // 4개 코너에서 삼각형 컷: (x, y가 코너에 가까우면 밖)
                        bool cornerCut =
                            (x + y < cut) ||                                    // 좌하
                            ((W - 1 - x) + y < cut) ||                          // 우하
                            (x + (H - 1 - y) < cut) ||                          // 좌상
                            ((W - 1 - x) + (H - 1 - y) < cut);                  // 우상
                        if (cornerCut) blocks.Add(new Vector2Int(x, y));
                    }
                break;
        }
        return blocks;
    }

    Vector2Int PickRandomEmpty(HashSet<Vector2Int> used, System.Random rng)
    {
        var free = new List<Vector2Int>();
        for (int y = 0; y < genHeight; y++)
            for (int x = 0; x < genWidth; x++)
            {
                var p = new Vector2Int(x, y);
                if (!used.Contains(p)) free.Add(p);
            }
        if (free.Count == 0) return new Vector2Int(-1, -1);
        return free[rng.Next(free.Count)];
    }

    Vector2Int PickAdjacentEmpty(Vector2Int basis, HashSet<Vector2Int> used, System.Random rng)
    {
        var opts = new List<Vector2Int>();
        for (int i = 0; i < 4; i++)
        {
            var p = new Vector2Int(basis.x + DX[i], basis.y + DY[i]);
            if (p.x < 0 || p.x >= genWidth || p.y < 0 || p.y >= genHeight) continue;
            if (used.Contains(p)) continue;
            opts.Add(p);
        }
        if (opts.Count == 0) return new Vector2Int(-1, -1);
        return opts[rng.Next(opts.Count)];
    }

    void ApplyCandidate(Candidate c)
    {
        if (genTarget == null) return;
        Undo.RecordObject(genTarget, "Apply generated candidate");
        genTarget.width = genWidth;
        genTarget.height = genHeight;
        genTarget.startCell = genStart;
        genTarget.blockedCells = new List<Vector2Int>(c.blockedCells);
        EditorUtility.SetDirty(genTarget);
        AssetDatabase.SaveAssetIfDirty(genTarget);
        UnityEngine.Debug.Log($"{genTarget.name}에 적용됨 (정답 {c.pathCount}개)");
    }

    // 격자 시각화. y=0을 아래에 두는 게임 좌표계 그대로.
    // firstPath 있으면 방문 순서를 숫자로 표시 (01=시작, 02=두 번째 이동, ...)
    string PreviewGrid(Candidate c) =>
        PreviewGrid(genWidth, genHeight, c.blockedCells, c.firstPath);

    string PreviewGrid(int W, int H, List<Vector2Int> blockedCells, List<Vector2Int> firstPath)
    {
        var blocked = new HashSet<Vector2Int>(blockedCells);
        var pathIndex = new Dictionary<Vector2Int, int>();
        if (firstPath != null)
            for (int i = 0; i < firstPath.Count; i++)
                pathIndex[firstPath[i]] = i + 1;

        // 셀 폭 계산 (사용칸 100+ 대비 3자리, 아니면 2자리)
        int totalCells = W * H - blockedCells.Count;
        int width = totalCells >= 100 ? 3 : 2;
        string blockStr = new string(' ', width - 1) + "#";
        string emptyStr = new string(' ', width - 1) + ".";

        var sb = new StringBuilder();
        for (int y = H - 1; y >= 0; y--)
        {
            for (int x = 0; x < W; x++)
            {
                var p = new Vector2Int(x, y);
                if (blocked.Contains(p))                              sb.Append(blockStr);
                else if (pathIndex.TryGetValue(p, out int idx))       sb.Append(idx.ToString().PadLeft(width, '0'));
                else                                                  sb.Append(emptyStr);
                sb.Append(' ');
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    // 정답 하나 찾아서 방문 순서 반환. 없으면 null.
    // CountPaths와 별개로 first-hit-only DFS. 오버헤드 무시할 수준.
    List<Vector2Int> FindOnePath(LevelData level)
    {
        int W = level.width, H = level.height;
        bool[,] blocked = new bool[W, H];
        foreach (var b in level.blockedCells)
            if (b.x >= 0 && b.x < W && b.y >= 0 && b.y < H)
                blocked[b.x, b.y] = true;

        int totalCells = W * H - level.blockedCells.Count;
        var start = level.startCell;
        if (start.x < 0 || start.x >= W || start.y < 0 || start.y >= H) return null;
        if (blocked[start.x, start.y]) return null;

        bool[,] visited = new bool[W, H];
        visited[start.x, start.y] = true;
        var currentPath = new List<Vector2Int> { start };
        var result = new List<Vector2Int>();

        if (DFSFindPath(start.x, start.y, 1, totalCells, W, H, blocked, visited, currentPath, result))
            return result;
        return null;
    }

    bool DFSFindPath(int x, int y, int filled, int total, int W, int H,
                     bool[,] blocked, bool[,] visited,
                     List<Vector2Int> currentPath, List<Vector2Int> result)
    {
        if (filled == total)
        {
            result.AddRange(currentPath);
            return true;
        }
        for (int i = 0; i < 4; i++)
        {
            int nx = x + DX[i], ny = y + DY[i];
            if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
            if (blocked[nx, ny] || visited[nx, ny]) continue;

            visited[nx, ny] = true;
            currentPath.Add(new Vector2Int(nx, ny));
            if (DFSFindPath(nx, ny, filled + 1, total, W, H, blocked, visited, currentPath, result))
                return true;
            currentPath.RemoveAt(currentPath.Count - 1);
            visited[nx, ny] = false;
        }
        return false;
    }

    string BlockedCellsToText(Candidate c)
    {
        var sb = new StringBuilder("blocked: ");
        for (int i = 0; i < c.blockedCells.Count; i++)
        {
            if (i > 0) sb.Append(' ');
            var b = c.blockedCells[i];
            sb.Append($"({b.x},{b.y})");
        }
        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════
    // ── 아래는 기존 Solver 코드. 손대지 않음 ──
    // ═══════════════════════════════════════════════════

    void RunBatchScan()
    {
        batchResults.Clear();

        var guids = AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/Levels" });
        var levels = new List<LevelData>(guids.Length);
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var lv = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (lv != null) levels.Add(lv);
        }
        levels.Sort((a, b) => ExtractNumber(a.name).CompareTo(ExtractNumber(b.name)));

        try
        {
            for (int i = 0; i < levels.Count; i++)
            {
                bool canceled = EditorUtility.DisplayCancelableProgressBar(
                    "Level Solver",
                    $"{levels[i].name} 계산 중... ({i + 1}/{levels.Count})",
                    (float)i / levels.Count);
                if (canceled) { batchResults.Add("(사용자 중단)"); break; }

                var (count, timedOut) = CountPaths(levels[i]);
                batchResults.Add(FormatResult(levels[i].name, count, timedOut));
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    int ExtractNumber(string name)
    {
        int underscore = name.LastIndexOf('_');
        if (underscore < 0) return 0;
        int.TryParse(name.Substring(underscore + 1), out int n);
        return n;
    }

    string FormatResult(string levelName, int count, bool timedOut)
    {
        string tag;
        if (timedOut)               tag = $"[타임아웃 - 최소 {count}개 확인]";
        else if (count == 0)        tag = "[불가]";
        else if (count == 1)        tag = "[극악 - 유일]";
        else if (count <= 5)        tag = "[매우 어려움]";
        else if (count <= 50)       tag = "[어려움]";
        else if (count < MAX_PATHS) tag = "[중간]";
        else                        tag = $"[쉬움 - {MAX_PATHS}+]";
        return $"{levelName,-12} 정답 {count,4}개  {tag}";
    }

    // maxCount: 정답이 이 개수 도달 시 조기 종료. Generator는 (목표max+1)을 넘겨
    //           "너무 쉬운 후보"를 빨리 걸러냄. Solver 단독 사용은 MAX_PATHS 유지.
    (int count, bool timedOut) CountPaths(LevelData level, int maxCount = MAX_PATHS)
    {
        int W = level.width, H = level.height;
        if (W <= 0 || H <= 0) return (0, false);

        bool[,] blocked = new bool[W, H];
        foreach (var b in level.blockedCells)
            if (b.x >= 0 && b.x < W && b.y >= 0 && b.y < H)
                blocked[b.x, b.y] = true;

        int totalCells = W * H - level.blockedCells.Count;
        if (totalCells <= 0) return (0, false);

        var start = level.startCell;
        if (start.x < 0 || start.x >= W || start.y < 0 || start.y >= H) return (0, false);
        if (blocked[start.x, start.y]) return (0, false);

        bool[,] visited = new bool[W, H];
        visited[start.x, start.y] = true;

        int count = 0;
        bool timedOut = false;
        var sw = Stopwatch.StartNew();
        bool[,] seenBuf = new bool[W, H];
        var bfsQueue = new Queue<(int, int)>();

        DFS(start.x, start.y, 1, totalCells, W, H, blocked, visited,
            seenBuf, bfsQueue, sw, maxCount, ref count, ref timedOut);
        return (count, timedOut);
    }

    static readonly int[] DX = { 1, -1, 0, 0 };
    static readonly int[] DY = { 0, 0, 1, -1 };

    void DFS(int x, int y, int filled, int total,
             int W, int H, bool[,] blocked, bool[,] visited,
             bool[,] seenBuf, Queue<(int, int)> bfsQueue,
             Stopwatch sw, int maxCount, ref int count, ref bool timedOut)
    {
        if (timedOut || count >= maxCount) return;
        if (sw.ElapsedMilliseconds > TIMEOUT_MS) { timedOut = true; return; }

        if (filled == total)
        {
            count++;
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            int nx = x + DX[i], ny = y + DY[i];
            if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
            if (blocked[nx, ny] || visited[nx, ny]) continue;

            visited[nx, ny] = true;

            int remaining = total - filled - 1;
            if (remaining == 0 || IsConnected(nx, ny, W, H, blocked, visited,
                                              seenBuf, bfsQueue, remaining))
            {
                DFS(nx, ny, filled + 1, total, W, H, blocked, visited,
                    seenBuf, bfsQueue, sw, maxCount, ref count, ref timedOut);
            }

            visited[nx, ny] = false;

            if (timedOut || count >= maxCount) return;
        }
    }

    bool IsConnected(int cx, int cy, int W, int H,
                     bool[,] blocked, bool[,] visited,
                     bool[,] seenBuf, Queue<(int, int)> bfsQueue, int remaining)
    {
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                seenBuf[x, y] = false;
        bfsQueue.Clear();

        for (int i = 0; i < 4; i++)
        {
            int nx = cx + DX[i], ny = cy + DY[i];
            if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
            if (blocked[nx, ny] || visited[nx, ny] || seenBuf[nx, ny]) continue;
            seenBuf[nx, ny] = true;
            bfsQueue.Enqueue((nx, ny));
        }

        int reachable = 0;
        while (bfsQueue.Count > 0)
        {
            var (x, y) = bfsQueue.Dequeue();
            reachable++;
            if (reachable == remaining) return true;

            for (int i = 0; i < 4; i++)
            {
                int nx = x + DX[i], ny = y + DY[i];
                if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                if (blocked[nx, ny] || visited[nx, ny] || seenBuf[nx, ny]) continue;
                seenBuf[nx, ny] = true;
                bfsQueue.Enqueue((nx, ny));
            }
        }

        return reachable == remaining;
    }
}
