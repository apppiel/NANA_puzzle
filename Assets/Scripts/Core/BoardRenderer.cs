using System.Collections.Generic;
using UnityEngine;

public class BoardRenderer : MonoBehaviour
{
    public LevelData level;
    public GameObject cellPrefab;
    public float cellSize = 1.1f;

    // 칸 색깔
    Color emptyColor  = new Color(0.85f, 0.85f, 0.85f); // 빈 칸
    Color startColor  = new Color(1f, 0.8f, 0.2f);      // 시작 칸
    Color filledColor = new Color(0.3f, 0.6f, 1f);      // 채운 칸
    Color winColor    = new Color(0.3f, 0.85f, 0.4f);   // 클리어

    Dictionary<Vector2Int, SpriteRenderer> cells = new Dictionary<Vector2Int, SpriteRenderer>();
    List<Vector2Int> path = new List<Vector2Int>();     // 지금까지 채운 칸들 (순서대로)

    float offsetX, offsetY;
    int fillableCount;
    bool isDrawing = false;
    bool won = false;

    void Start()
    {
        offsetX = (level.width - 1) / 2f;
        offsetY = (level.height - 1) / 2f;

        DrawBoard();

        fillableCount = cells.Count;     // 막힌 칸 뺀 실제 칸 수
        path.Add(level.startCell);       // 시작 칸부터 시작
        RefreshColors();
    }

    void DrawBoard()
    {
        for (int x = 0; x < level.width; x++)
        {
            for (int y = 0; y < level.height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (level.blockedCells.Contains(cell)) continue;

                Vector3 pos = new Vector3((x - offsetX) * cellSize, (y - offsetY) * cellSize, 0f);
                GameObject obj = Instantiate(cellPrefab, pos, Quaternion.identity, transform);
                obj.name = $"Cell_{x}_{y}";
                cells[cell] = obj.GetComponent<SpriteRenderer>();
            }
        }
    }

    void Update()
    {
        if (won) return;

        // 시작 칸을 누르면 처음부터 다시 그리기
        if (Input.GetMouseButtonDown(0))
        {
            Vector2Int c = WorldToCell(Camera.main.ScreenToWorldPoint(Input.mousePosition));
            if (c == level.startCell)
            {
                isDrawing = true;
                path.Clear();
                path.Add(level.startCell);
                RefreshColors();
            }
        }

        // 누른 채로 끌고 있으면 칸 채우기 시도
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

        // 직전 칸으로 돌아가면 되돌리기
        if (path.Count >= 2 && target == path[path.Count - 2])
        {
            path.RemoveAt(path.Count - 1);
            RefreshColors();
            return;
        }

        // 칸이 존재하고, 머리와 상하좌우로 붙어있고, 아직 안 지난 칸이면 전진
        bool adjacent = Mathf.Abs(target.x - head.x) + Mathf.Abs(target.y - head.y) == 1;
        if (cells.ContainsKey(target) && adjacent && !path.Contains(target))
        {
            path.Add(target);
            RefreshColors();
            CheckWin();
        }
    }

    void CheckWin()
    {
        if (path.Count == fillableCount)
        {
            won = true;
            RefreshColors();
            Debug.Log("클리어!");
        }
    }

    void RefreshColors()
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
    }

    Vector2Int WorldToCell(Vector3 world)
    {
        int x = Mathf.RoundToInt(world.x / cellSize + offsetX);
        int y = Mathf.RoundToInt(world.y / cellSize + offsetY);
        return new Vector2Int(x, y);
    }
}
