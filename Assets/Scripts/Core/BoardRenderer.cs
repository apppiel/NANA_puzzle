using System.Collections.Generic;
using UnityEngine;

public class BoardRenderer : MonoBehaviour
{
  public GameManager gameManager;   // 클리어를 알릴 대상
  public GameObject cellPrefab;
  public float cellSize = 1.1f;

  Color emptyColor = new Color(0.85f, 0.85f, 0.85f);
  Color startColor = new Color(1f, 0.8f, 0.2f);
  Color filledColor = new Color(0.3f, 0.6f, 1f);
  Color winColor = new Color(0.3f, 0.85f, 0.4f);

  Dictionary<Vector2Int, SpriteRenderer> cells = new Dictionary<Vector2Int, SpriteRenderer>();
  List<Vector2Int> path = new List<Vector2Int>();

  LevelData level;
  LineRenderer line;
  Sprite cellSprite;
  float offsetX, offsetY;
  int fillableCount;
  bool isDrawing = false;
  bool won = false;

  void Awake()
  {
    cellSprite = MakeRoundedSprite(256, 64);   // 둥근 그림 (한 번만 생성)
  }

  // GameManager가 "이 레벨 그려줘" 하고 부르는 함수
  public void ShowLevel(LevelData newLevel)
  {
    level = newLevel;
    ClearBoard();

    won = false;
    isDrawing = false;

    offsetX = (level.width - 1) / 2f;
    offsetY = (level.height - 1) / 2f;

    DrawBoard();
    CreateLine();

    fillableCount = cells.Count;
    path.Add(level.startCell);
    Redraw();
    FitCamera();   // 레벨 크기에 맞게 카메라 자동 조정
  }

  void ClearBoard()
  {
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
        sr.sprite = cellSprite;
        cells[cell] = sr;
      }
    }
  }

  void CreateLine()
  {
    GameObject lineObj = new GameObject("PathLine");
    lineObj.transform.SetParent(transform);
    line = lineObj.AddComponent<LineRenderer>();
    line.material = new Material(Shader.Find("Sprites/Default"));
    line.startColor = line.endColor = new Color(0.15f, 0.4f, 0.9f);
    line.startWidth = line.endWidth = 0.18f;
    line.numCapVertices = 6;
    line.numCornerVertices = 6;
    line.sortingOrder = 1;
    line.useWorldSpace = true;
    line.positionCount = 0;
  }

  void Update()
  {
    if (won || level == null) return;

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

    if (path.Count >= 2 && target == path[path.Count - 2])
    {
      path.RemoveAt(path.Count - 1);
      Redraw();
      return;
    }

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
      if (gameManager != null) gameManager.OnLevelSolved();   // 위로 알림
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

    line.positionCount = path.Count;
    for (int i = 0; i < path.Count; i++)
      line.SetPosition(i, CellToWorld(path[i]));
  }

  Sprite MakeRoundedSprite(int size, int radius)
  {
    Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    tex.wrapMode = TextureWrapMode.Clamp;
    tex.filterMode = FilterMode.Bilinear;

    Color on = Color.white;
    Color off = new Color(1f, 1f, 1f, 0f);

    for (int y = 0; y < size; y++)
    {
      for (int x = 0; x < size; x++)
      {
        float cx = Mathf.Clamp(x, radius, size - 1 - radius);
        float cy = Mathf.Clamp(y, radius, size - 1 - radius);
        float dx = x - cx;
        float dy = y - cy;
        bool inside = (dx * dx + dy * dy) <= (radius * radius);
        tex.SetPixel(x, y, inside ? on : off);
      }
    }
    tex.Apply();
    return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
  }

  void FitCamera()
  {
    Camera cam = Camera.main;
    if (cam == null) return;

    // 카메라를 항상 격자 정중앙 위로 고정. z=-10은 2D에서 카메라가 씬을 볼 수 있는 표준 위치
    cam.transform.position = new Vector3(0f, 0f, -10f);

    float boardW = level.width  * cellSize;
    float boardH = level.height * cellSize;
    float margin = 1f;  // 격자 가장자리와 화면 끝 사이의 여백. 줄이면 더 꽉 차게, 늘리면 더 여유롭게 보임

    // orthographicSize = 화면 세로 절반 크기(유닛).
    // 세로 기준과 가로 기준 중 더 큰 값을 써야 격자가 화면을 벗어나지 않음.
    float sizeForHeight = boardH / 2f + margin;
    float sizeForWidth  = (boardW / 2f + margin) / cam.aspect;  // 가로 → 세로 단위로 환산
    cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);
  }

  Vector3 CellToWorld(Vector2Int c)
  {
    return new Vector3((c.x - offsetX) * cellSize, (c.y - offsetY) * cellSize, 0f);
  }

  Vector2Int WorldToCell(Vector3 world)
  {
    int x = Mathf.RoundToInt(world.x / cellSize + offsetX);
    int y = Mathf.RoundToInt(world.y / cellSize + offsetY);
    return new Vector2Int(x, y);
  }
}