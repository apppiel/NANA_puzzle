using System.Collections.Generic;
using UnityEngine;

public class BoardRenderer : MonoBehaviour
{
    public GameObject cellPrefab;
    public float cellSize = 1.1f;
    // 클리어 판정 후 GameManager.OnLevelSolved()를 호출할 대상. 인스펙터에서 연결
    public GameManager gameManager;

    Color emptyColor  = new Color(0.85f, 0.85f, 0.85f); // 빈 칸
    Color startColor  = new Color(1f, 0.8f, 0.2f);      // 시작 칸
    Color filledColor = new Color(0.3f, 0.6f, 1f);      // 채운 칸
    Color winColor    = new Color(0.3f, 0.85f, 0.4f);   // 클리어

    // 격자 좌표 → SpriteRenderer 맵. Redraw에서 O(1)로 색상 접근하기 위해 Dictionary 사용
    Dictionary<Vector2Int, SpriteRenderer> cells = new Dictionary<Vector2Int, SpriteRenderer>();
    // 지나온 칸 순서 목록. 순서가 있어야 되돌리기(마지막 제거)와 선 그리기(순서대로 점 연결)가 가능
    List<Vector2Int> path = new List<Vector2Int>();

    LevelData level;
    LineRenderer line;
    Sprite cellSprite;      // 모든 칸이 공유하는 둥근 사각형 그림
    float offsetX, offsetY; // 격자를 화면 중앙에 놓기 위한 오프셋
    int fillableCount;      // 채워야 할 총 칸 수(막힌 칸 제외). path.Count와 같으면 클리어
    bool isDrawing = false;
    bool won = false;

    void Awake()
    {
        // Start보다 먼저 실행되는 Awake에서 생성해야
        // GameManager.Start()가 ShowLevel을 호출할 때 스프라이트가 이미 준비된 상태가 됨
        cellSprite = MakeRoundedSprite(256, 48);
    }

    // GameManager가 호출. 주어진 레벨 데이터로 보드를 처음부터 초기화
    public void ShowLevel(LevelData levelData)
    {
        level = levelData;

        // 이전 레벨의 칸 오브젝트·선을 모두 제거한 뒤 새로 생성
        ClearBoard();

        won = false;
        isDrawing = false;

        offsetX = (level.width - 1) / 2f;
        offsetY = (level.height - 1) / 2f;

        DrawBoard();
        CreateLine();

        // DrawBoard()가 먼저 실행돼야 cells에 막힌 칸이 빠진 실제 칸만 담김
        fillableCount = cells.Count;
        path.Add(level.startCell);
        Redraw();
    }

    void ClearBoard()
    {
        // Board 오브젝트의 자식(칸 스프라이트, PathLine 등)을 모두 파괴해 이전 레벨 흔적 제거
        foreach (Transform child in transform)
            Destroy(child.gameObject);
        cells.Clear();
        path.Clear();
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
                SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
                sr.sprite = cellSprite;   // 프리팹 기본 스프라이트 대신 둥근 사각형으로 교체
                cells[cell] = sr;
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
            // UI 표시·레벨 전환 등은 GameManager가 담당
            if (gameManager != null) gameManager.OnLevelSolved();
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

    // 모서리가 둥근 흰색 사각형 텍스처를 코드로 생성해 Sprite로 반환
    // size: 텍스처 해상도(px), radius: 모서리 반지름(px)
    Sprite MakeRoundedSprite(int size, int radius)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color on  = Color.white;
        Color off = new Color(1f, 1f, 1f, 0f); // 투명

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 각 픽셀에서 가장 가까운 "안쪽 모서리 꼭짓점" 좌표를 구함.
                // Clamp로 모서리 영역 안에 있으면 픽셀 자신이 기준점이 되고,
                // 네 귀퉁이 바깥이면 해당 모서리 꼭짓점이 기준점이 됨.
                float cx = Mathf.Clamp(x, radius, size - 1 - radius);
                float cy = Mathf.Clamp(y, radius, size - 1 - radius);
                float dx = x - cx;
                float dy = y - cy;
                // 기준점까지의 거리가 radius 이내면 내부(흰색), 이외면 투명
                bool inside = (dx * dx + dy * dy) <= (radius * radius);
                tex.SetPixel(x, y, inside ? on : off);
            }
        }
        tex.Apply();
        // pixelsPerUnit = size로 설정하면 스프라이트의 월드 크기가 정확히 1유닛이 됨
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
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
