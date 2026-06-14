using System.Collections.Generic;
using UnityEngine;

public class BoardRenderer : MonoBehaviour
{
    public LevelData level;
    public GameObject cellPrefab;
    public float cellSize = 1.1f;

    Color emptyColor  = new Color(0.85f, 0.85f, 0.85f); // 빈 칸
    Color startColor  = new Color(1f, 0.8f, 0.2f);      // 시작 칸
    Color filledColor = new Color(0.3f, 0.6f, 1f);      // 채운 칸
    Color winColor    = new Color(0.3f, 0.85f, 0.4f);   // 클리어

    // 격자 좌표 → SpriteRenderer 맵. Redraw에서 O(1)로 색상 접근하기 위해 Dictionary 사용
    Dictionary<Vector2Int, SpriteRenderer> cells = new Dictionary<Vector2Int, SpriteRenderer>();
    // 지나온 칸 순서 목록. 순서가 있어야 되돌리기(마지막 제거)와 선 그리기(순서대로 점 연결)가 가능
    List<Vector2Int> path = new List<Vector2Int>();

    LineRenderer line;
    // 격자를 화면 중앙에 놓기 위한 오프셋. (0,0)칸이 왼쪽 아래 끝이 아닌 중앙 기준이 되도록 이동
    float offsetX, offsetY;
    // 채워야 할 총 칸 수 (막힌 칸 제외). 이 수와 path.Count가 같으면 클리어
    int fillableCount;
    bool isDrawing = false;
    bool won = false;

    void Start()
    {
        offsetX = (level.width - 1) / 2f;
        offsetY = (level.height - 1) / 2f;

        DrawBoard();
        CreateLine();

        // DrawBoard()가 먼저 실행돼야 cells에 막힌 칸이 빠진 실제 칸만 담김
        fillableCount = cells.Count;
        path.Add(level.startCell);
        Redraw();
    }

    void DrawBoard()
    {
        for (int x = 0; x < level.width; x++)
        {
            for (int y = 0; y < level.height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (level.blockedCells.Contains(cell)) continue;

                GameObject obj = Instantiate(cellPrefab, CellToWorld(cell), Quaternion.identity, transform);
                obj.name = $"Cell_{x}_{y}";
                cells[cell] = obj.GetComponent<SpriteRenderer>();
            }
        }
    }

    void CreateLine()
    {
        GameObject lineObj = new GameObject("PathLine");
        lineObj.transform.SetParent(transform);
        line = lineObj.AddComponent<LineRenderer>();
        // Sprites/Default 셰이더가 있어야 startColor/endColor가 실제로 화면에 반영됨
        // 이 줄 없으면 LineRenderer가 분홍색(missing material) 또는 흰색으로만 표시됨
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = new Color(0.15f, 0.4f, 0.9f);
        line.startWidth = line.endWidth = 0.18f;
        line.numCapVertices = 6;    // 선 끝을 둥글게
        line.numCornerVertices = 6; // 꺾이는 모서리를 둥글게
        line.sortingOrder = 1;      // 칸 스프라이트(order 0) 위에 그려지도록
        line.useWorldSpace = true;
        line.positionCount = 0;
    }

    void Update()
    {
        if (won) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector2Int c = WorldToCell(Camera.main.ScreenToWorldPoint(Input.mousePosition));
            if (c == level.startCell)
            {
                isDrawing = true;
                path.Clear();
                path.Add(level.startCell);
                Redraw();
            }
        }

        if (Input.GetMouseButton(0) && isDrawing)
        {
            Vector2Int c = WorldToCell(Camera.main.ScreenToWorldPoint(Input.mousePosition));
            TryMove(c);
        }

        if (Input.GetMouseButtonUp(0))
            isDrawing = false;
    }

    void TryMove(Vector2Int target)
    {
        Vector2Int head = path[path.Count - 1];
        if (target == head) return;

        // path[Count-2]는 head 바로 직전 칸. 거기로 되돌아가면 head를 하나 취소(되돌리기)
        if (path.Count >= 2 && target == path[path.Count - 2])
        {
            path.RemoveAt(path.Count - 1);
            Redraw();
            return;
        }

        // 맨해튼 거리 1 = 상하좌우 정확히 한 칸 인접
        bool adjacent = Mathf.Abs(target.x - head.x) + Mathf.Abs(target.y - head.y) == 1;
        if (cells.ContainsKey(target) && adjacent && !path.Contains(target))
        {
            path.Add(target);
            Redraw();
            CheckWin();
        }
    }

    void CheckWin()
    {
        if (path.Count == fillableCount)
        {
            won = true;
            Redraw();
            Debug.Log("클리어!");
        }
    }

    void Redraw()
    {
        foreach (var kv in cells)
        {
            Vector2Int c = kv.Key;
            if (won)
                kv.Value.color = winColor;
            else if (path.Contains(c))
                kv.Value.color = (c == level.startCell) ? startColor : filledColor;
            else
                kv.Value.color = emptyColor;
        }

        // positionCount를 먼저 설정해야 SetPosition 호출 시 인덱스 범위 오류가 나지 않음
        line.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
            line.SetPosition(i, CellToWorld(path[i]));
    }

    Vector3 CellToWorld(Vector2Int c)
    {
        return new Vector3((c.x - offsetX) * cellSize, (c.y - offsetY) * cellSize, 0f);
    }

    // CellToWorld의 역변환: world.x = (c.x - offsetX) * cellSize → c.x = world.x / cellSize + offsetX
    Vector2Int WorldToCell(Vector3 world)
    {
        int x = Mathf.RoundToInt(world.x / cellSize + offsetX);
        int y = Mathf.RoundToInt(world.y / cellSize + offsetY);
        return new Vector2Int(x, y);
    }
}
