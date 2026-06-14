using System.Collections.Generic;
using UnityEngine;

// 역할: 격자를 화면에 그리고, 손가락(마우스) 입력을 받아 경로를 칠하고, 클리어를 판정
// GameManager가 ShowLevel()을 호출하면 그때부터 이 스크립트가 동작을 시작함
public class BoardRenderer : MonoBehaviour
{
  // ── 인스펙터 연결 ───────────────────────────────────────────────
  public GameManager gameManager;  // 클리어 시 OnLevelSolved()를 불러줄 대상. 인스펙터에서 GameManager 오브젝트 연결
  public GameObject cellPrefab;    // 칸 하나를 찍어낼 프리팹. 인스펙터에서 Cell 프리팹 연결
  public float cellSize = 1.1f;    // 칸 사이 간격(유닛). 1.0이면 딱 붙고, 1.1이면 약간 간격이 생김

  // ── 칸 색상 ─────────────────────────────────────────────────────
  // new Color(R, G, B)  ← 각 값은 0~1 범위 (255 기준이 아님)
  Color emptyColor  = new Color(0.85f, 0.85f, 0.85f);  // 아직 지나지 않은 빈 칸 → 연한 회색
  Color startColor  = new Color(1f,    0.8f,  0.2f);   // 시작 칸 → 노란색
  Color filledColor = new Color(0.3f,  0.6f,  1f);     // 경로로 채운 칸 → 파란색
  Color winColor    = new Color(0.3f,  0.85f, 0.4f);   // 클리어 시 모든 칸 → 초록색

  // ── 런타임 데이터 ───────────────────────────────────────────────
  // 격자 좌표(Vector2Int) → 해당 칸의 SpriteRenderer
  // Dictionary를 쓰는 이유: "이 좌표에 칸이 있는지" 확인하고 색을 바꿀 때 O(1)로 빠르게 접근 가능
  Dictionary<Vector2Int, SpriteRenderer> cells = new Dictionary<Vector2Int, SpriteRenderer>();

  // 플레이어가 지나온 칸을 순서대로 저장한 목록
  // 순서가 필요한 이유 1: 되돌리기 → 마지막 칸(path[끝])을 제거
  // 순서가 필요한 이유 2: 선 그리기 → 순서대로 점을 연결해야 경로선이 올바르게 그려짐
  List<Vector2Int> path = new List<Vector2Int>();

  LevelData level;       // 현재 표시 중인 레벨 데이터 (GameManager가 ShowLevel로 넘겨줌)
  LineRenderer line;      // 경로 위에 그려지는 선. CreateLine()에서 생성
  Sprite cellSprite;      // 모든 칸이 공유하는 둥근 사각형 그림. Awake에서 한 번만 생성
  Material lineMaterial;  // 경로 선용 머티리얼. new Material()은 자동 해제가 안 되므로 Awake에서 한 번만 생성
  float offsetX, offsetY;  // 격자를 화면 중앙에 맞추기 위한 이동값 (ShowLevel에서 계산)
  int fillableCount;     // 채워야 할 칸의 총 수. path.Count가 이 값과 같아지면 클리어
  bool isDrawing = false;  // 현재 손가락을 누르고 있는 중인지 여부
  bool won = false;        // 클리어 여부. true가 되면 Update에서 입력을 완전히 무시


  // ── 초기화 ──────────────────────────────────────────────────────

  void Awake()
  {
    // Awake는 Start보다 먼저 실행됨
    // 스프라이트를 여기서 만들어야, 곧바로 호출될 수 있는 ShowLevel() 안에서 안전하게 사용 가능
    cellSprite = MakeRoundedSprite(256, 64);
    lineMaterial = new Material(Shader.Find("Sprites/Default"));  // 레벨이 바뀌어도 재사용
  }

  // GameManager가 "이 레벨 그려줘" 하고 호출하는 함수 (외부에서 부를 수 있도록 public)
  public void ShowLevel(LevelData newLevel)
  {
    level = newLevel;

    ClearBoard();   // 이전 레벨의 칸·선 오브젝트를 전부 제거

    won = false;
    isDrawing = false;

    // 격자의 중심이 (0, 0)이 되도록 오프셋 계산
    // 예) 3×3 격자 → offsetX = (3-1)/2 = 1 → x좌표가 -1, 0, 1로 배치됨
    offsetX = (level.width  - 1) / 2f;
    offsetY = (level.height - 1) / 2f;

    DrawBoard();    // 칸 오브젝트 생성
    CreateLine();   // 경로 선 오브젝트 생성

    // DrawBoard() 다음에 세야 막힌 칸이 제외된 실제 칸 수를 얻을 수 있음
    fillableCount = cells.Count;

    // 시작 칸을 경로의 첫 번째 칸으로 추가 (시작 칸은 이미 밟은 상태로 시작)
    path.Add(level.startCell);

    Redraw();       // 색상·선 초기 반영
    FitCamera();    // 레벨 크기에 맞게 카메라 범위 자동 조정
  }


  // ── 보드 생성·삭제 ──────────────────────────────────────────────

  void ClearBoard()
  {
    // 이 스크립트가 붙은 오브젝트(Board)의 자식을 전부 파괴
    // 칸 스프라이트와 PathLine 오브젝트가 자식으로 붙어 있으므로 모두 제거됨
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
        Vector2Int coord = new Vector2Int(x, y);

        // 막힌 칸(blockedCells)은 생성하지 않음 → 비정형 모양 레벨 구현에 활용
        if (level.blockedCells.Contains(coord)) continue;

        // 칸 프리팹을 격자 좌표에 해당하는 월드 위치에 생성, Board의 자식으로 붙임
        GameObject obj = Instantiate(cellPrefab, CellToWorld(coord), Quaternion.identity, transform);
        obj.name = $"Cell_{x}_{y}";

        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        sr.sprite = cellSprite;   // 프리팹 기본 스프라이트 대신 코드로 만든 둥근 사각형으로 교체
        cells[coord] = sr;
      }
    }
  }

  void CreateLine()
  {
    GameObject lineObj = new GameObject("PathLine");
    lineObj.transform.SetParent(transform);  // Board의 자식으로 붙여야 ClearBoard()에서 같이 제거됨

    line = lineObj.AddComponent<LineRenderer>();

    // Sprites/Default 셰이더가 있어야 아래의 색상 설정이 실제로 화면에 반영됨
    // 이 줄 없이 기본 셰이더를 쓰면 선이 분홍색(셰이더 누락) 또는 흰색으로만 표시됨
    line.material = lineMaterial;  // Awake에서 한 번만 만든 머티리얼을 재사용 (매번 new Material 하면 메모리 누수)

    line.startColor = line.endColor = new Color(0.15f, 0.4f, 0.9f);  // 선 색상: 진한 파란색
    line.startWidth = line.endWidth = 0.18f;   // 선 두께(유닛). 키우면 선이 굵어짐
    line.numCapVertices    = 6;  // 선 끝부분을 몇 각형으로 둥글게 만들지 (클수록 더 동그래짐)
    line.numCornerVertices = 6;  // 꺾이는 모서리를 몇 각형으로 둥글게 만들지
    line.sortingOrder = 1;       // 칸 스프라이트(기본 order=0) 위에 선이 그려지도록 1로 설정
    line.useWorldSpace = true;
    line.positionCount = 0;      // 아직 경로가 없으므로 점 0개로 시작
  }


  // ── 입력 처리 (매 프레임 실행) ──────────────────────────────────

  void Update()
  {
    // 클리어됐거나 아직 레벨이 로드되지 않은 경우 입력 무시
    if (won || level == null) return;

    // 손가락(마우스 버튼) 처음 눌렀을 때
    if (Input.GetMouseButtonDown(0))
    {
      Vector2Int c = WorldToCell(Camera.main.ScreenToWorldPoint(Input.mousePosition));

      // 시작 칸을 누른 경우에만 그리기 시작 (아무 칸이나 눌러서 시작하면 안 되므로)
      if (c == level.startCell)
      {
        isDrawing = true;
        path.Clear();
        path.Add(level.startCell);
        Redraw();
      }
    }

    // 손가락을 누른 채 움직이는 동안 (드래그)
    if (Input.GetMouseButton(0) && isDrawing)
    {
      Vector2Int c = WorldToCell(Camera.main.ScreenToWorldPoint(Input.mousePosition));
      TryMove(c);
    }

    // 손가락을 뗐을 때
    if (Input.GetMouseButtonUp(0))
      isDrawing = false;
  }


  // ── 이동 로직 ───────────────────────────────────────────────────

  void TryMove(Vector2Int target)
  {
    Vector2Int head = path[path.Count - 1];  // 현재 경로의 맨 끝 칸

    // 현재 위치와 같은 칸이면 아무것도 안 함
    if (target == head) return;

    // 바로 직전 칸으로 되돌아간 경우 → 마지막 칸을 경로에서 제거 (되돌리기)
    // path.Count >= 2 체크: 경로에 칸이 2개 이상 있어야 직전 칸이 존재
    if (path.Count >= 2 && target == path[path.Count - 2])
    {
      path.RemoveAt(path.Count - 1);
      Redraw();
      return;
    }

    // 맨해튼 거리 = |Δx| + |Δy|. 이 값이 1이면 상하좌우 딱 한 칸 인접
    // (대각선이면 거리가 2가 되므로 자동으로 걸러짐)
    bool adjacent = Mathf.Abs(target.x - head.x) + Mathf.Abs(target.y - head.y) == 1;

    // 세 조건을 모두 만족해야 이동 가능:
    // 1. cells에 있는 칸(막힌 칸이 아님)
    // 2. 인접한 칸(대각선 이동 불가)
    // 3. 아직 경로에 포함되지 않은 칸(이미 지난 칸 재방문 불가)
    if (cells.ContainsKey(target) && adjacent && !path.Contains(target))
    {
      path.Add(target);
      Redraw();
      CheckWin();
    }
  }


  // ── 클리어 판정 ─────────────────────────────────────────────────

  void CheckWin()
  {
    // 경로 길이 == 전체 채울 수 있는 칸 수이면 모든 칸을 한 번씩 지난 것 → 클리어
    if (path.Count == fillableCount)
    {
      won = true;
      Redraw();  // 클리어 색(초록)으로 전체 갱신
      if (gameManager != null) gameManager.OnLevelSolved();  // GameManager에 클리어 알림
    }
  }


  // ── 화면 갱신 ───────────────────────────────────────────────────

  void Redraw()
  {
    // 모든 칸의 색상을 현재 상태에 맞게 다시 칠함
    foreach (var kv in cells)
    {
      Vector2Int c = kv.Key;
      SpriteRenderer sr = kv.Value;

      if (won)
        sr.color = winColor;                                          // 클리어 → 전부 초록
      else if (path.Contains(c))
        sr.color = (c == level.startCell) ? startColor : filledColor; // 경로 칸 → 시작은 노랑, 나머지는 파랑
      else
        sr.color = emptyColor;                                        // 아직 안 지난 칸 → 회색
    }

    // 경로 선 업데이트
    // positionCount를 먼저 늘려야 SetPosition에서 인덱스 오류가 나지 않음
    line.positionCount = path.Count;
    for (int i = 0; i < path.Count; i++)
      line.SetPosition(i, CellToWorld(path[i]));
  }


  // ── 둥근 사각형 스프라이트 생성 ─────────────────────────────────

  // 외부 이미지 파일 없이 코드만으로 둥근 사각형 텍스처를 만들어 Sprite로 반환
  // size: 텍스처 해상도(픽셀). 클수록 선명하지만 메모리 더 사용 (256이면 충분)
  // radius: 모서리 둥글기(픽셀). 클수록 더 둥글어짐 (64면 꽤 동그스름한 사각형)
  Sprite MakeRoundedSprite(int size, int radius)
  {
    Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    tex.wrapMode   = TextureWrapMode.Clamp;    // 텍스처 가장자리가 늘어나지 않도록
    tex.filterMode = FilterMode.Bilinear;       // 확대/축소 시 부드럽게 보이도록

    Color on  = Color.white;                   // 사각형 내부 → 흰색 (나중에 sr.color로 색을 덮어씌움)
    Color off = new Color(1f, 1f, 1f, 0f);    // 사각형 바깥 → 완전 투명

    for (int py = 0; py < size; py++)
    {
      for (int px = 0; px < size; px++)
      {
        // 각 픽셀에서 가장 가까운 "둥근 모서리의 중심점" 좌표를 구함
        // Clamp 덕분에 네 귀퉁이 바깥 픽셀은 해당 모서리 중심점이 기준이 됨
        float cx = Mathf.Clamp(px, radius, size - 1 - radius);
        float cy = Mathf.Clamp(py, radius, size - 1 - radius);

        float dx = px - cx;
        float dy = py - cy;

        // 모서리 중심점과의 거리가 radius 이하면 사각형 내부(흰색), 초과면 투명
        bool inside = (dx * dx + dy * dy) <= (radius * radius);
        tex.SetPixel(px, py, inside ? on : off);
      }
    }

    tex.Apply();  // SetPixel 호출이 끝난 뒤 반드시 Apply() 해야 실제 텍스처에 반영됨

    // pixelsPerUnit을 size와 같게 설정 → 스프라이트의 월드 크기 = 1유닛
    // (cellSize가 1.1이면 칸 사이에 0.1유닛 간격이 생김)
    return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
  }


  // ── 카메라 조정 ─────────────────────────────────────────────────

  void FitCamera()
  {
    Camera cam = Camera.main;
    if (cam == null) return;

    // 카메라를 격자 정중앙 위에 고정. z = -10은 2D에서 씬 전체를 볼 수 있는 표준 위치
    cam.transform.position = new Vector3(0f, 0f, -10f);

    float boardW = level.width  * cellSize;  // 격자 전체 가로 길이(유닛)
    float boardH = level.height * cellSize;  // 격자 전체 세로 길이(유닛)
    float margin = 1f;  // 격자와 화면 끝 사이의 여백. 줄이면 더 꽉 차게, 늘리면 더 여유롭게 보임

    // orthographicSize = 화면 세로의 절반 길이(유닛)
    // 세로 기준 크기와 가로 기준 크기 중 큰 값을 써야 어떤 화면 비율에서도 격자가 잘리지 않음
    float sizeForHeight = boardH / 2f + margin;
    float sizeForWidth  = (boardW / 2f + margin) / cam.aspect;  // 가로 길이를 세로 기준으로 환산
    cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);
  }


  // ── 좌표 변환 ───────────────────────────────────────────────────

  // 격자 좌표(칸 번호) → 유니티 월드 좌표
  // 예) 3×3 격자에서 (2, 2)칸 → ((2-1)*1.1, (2-1)*1.1, 0) = (1.1, 1.1, 0)
  Vector3 CellToWorld(Vector2Int c)
  {
    return new Vector3((c.x - offsetX) * cellSize, (c.y - offsetY) * cellSize, 0f);
  }

  // 유니티 월드 좌표 → 격자 좌표 (CellToWorld의 역변환)
  // 터치/마우스 위치를 격자 칸 번호로 바꿀 때 사용
  Vector2Int WorldToCell(Vector3 world)
  {
    int x = Mathf.RoundToInt(world.x / cellSize + offsetX);
    int y = Mathf.RoundToInt(world.y / cellSize + offsetY);
    return new Vector2Int(x, y);
  }
}
