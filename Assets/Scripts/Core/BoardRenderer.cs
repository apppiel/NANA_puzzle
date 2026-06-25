using System.Collections;
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
  public AudioClip fillSound;      // 칸을 채울 때 재생할 효과음. 인스펙터에서 AudioClip 연결
  public AudioClip winSound;       // 클리어 시 재생할 효과음. 인스펙터에서 AudioClip 연결
  public AudioClip stuckSound;     // 막힘 감지 시 재생할 효과음. 인스펙터에서 AudioClip 연결

  // ── 칸 색상 ─────────────────────────────────────────────────────
  // public으로 선언해 인스펙터에서 직접 색을 바꿀 수 있음. 코드 수정 없이 테마 변경 가능
  public Color emptyColor = Color.white;                              // 빈 칸 → 흰색
  public Color startColor = new Color(1f, 0.8f, 0.2f);              // 시작 칸 → 노란색
  public Color filledColor = new Color(0.996f, 0.871f, 0.871f);     // 경로 칸 → 연한 핑크
  public Color winColor = new Color(0.72f, 0.52f, 0.95f);           // 클리어 시 → 라벤더
  public Color outlineColor = new Color(0.75f, 0.75f, 0.75f, 0.8f);  // 외곽선 → 연한 회색
  // ── 런타임 데이터 ───────────────────────────────────────────────
  // 격자 좌표(Vector2Int) → 해당 칸의 SpriteRenderer
  // Dictionary를 쓰는 이유: "이 좌표에 칸이 있는지" 확인하고 색을 바꿀 때 O(1)로 빠르게 접근 가능
  Dictionary<Vector2Int, SpriteRenderer> cells = new Dictionary<Vector2Int, SpriteRenderer>();
  Dictionary<Vector2Int, SpriteRenderer> glows = new Dictionary<Vector2Int, SpriteRenderer>(); // 칸 바깥 글로우 스프라이트

  // 플레이어가 지나온 칸을 순서대로 저장한 목록
  // 순서가 필요한 이유 1: 되돌리기 → 마지막 칸(path[끝])을 제거
  // 순서가 필요한 이유 2: 선 그리기 → 순서대로 점을 연결해야 경로선이 올바르게 그려짐
  List<Vector2Int> path = new List<Vector2Int>();
  HashSet<Vector2Int> pathSet = new HashSet<Vector2Int>(); // path와 항상 동기화. Contains를 O(1)로 만들기 위해 병행 사용

  LevelData level;       // 현재 표시 중인 레벨 데이터 (GameManager가 ShowLevel로 넘겨줌)
  LineRenderer line;      // 경로 위에 그려지는 선. CreateLine()에서 생성
  AudioSource audioSource; // 효과음 재생기. Awake에서 자동 추가
  Sprite cellSprite;      // 모든 칸이 공유하는 둥근 사각형 그림. Awake에서 한 번만 생성
  Material lineMaterial;  // 경로 선용 머티리얼. new Material()은 자동 해제가 안 되므로 Awake에서 한 번만 생성
  float offsetX, offsetY;  // 격자를 화면 중앙에 맞추기 위한 이동값 (ShowLevel에서 계산)
  readonly Color lineColor = new Color(1f, 0.541f, 0.541f);  // 선·닷 공통 색상 (#ff8a8a)
  readonly WaitForSeconds waitStuck = new WaitForSeconds(0.6f);  // 캐싱: 매번 new 하면 GC 부담
  static readonly Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
  SpriteRenderer startDot;    // 시작점 중앙 흰 원 표시
  Sprite circleSprite;        // 원형 스프라이트 (시작/끝 닷 전용)
  bool dotAnimated = false;   // 시작 닷 팽창 애니메이션이 이미 실행됐는지
  SpriteRenderer endDot;      // 클리어 시 마지막 칸 중앙 흰 원
  int fillableCount;     // 채워야 할 칸의 총 수. path.Count가 이 값과 같아지면 클리어
  bool isDrawing = false;      // 현재 손가락을 누르고 있는 중인지 여부
  bool armed = false;          // 마지막 칸 탭 후 이어그리기 대기 중
  bool movedInGesture = false; // 현재 제스처에서 칸을 하나라도 이동했는지 (armed 해제 타이밍 판단용)
  bool won = false;            // 클리어 여부. true가 되면 Update에서 입력을 완전히 무시
  bool isResetting = false;    // 막힘 감지 후 자동 리셋 대기 중. 이 동안 입력 무시

  // ── 등장 애니메이션 설정 ────────────────────────────────────────
  // 인스펙터에서 값을 바꾸면 플레이 중에도 즉시 반영됨
  public float appearDuration = 0.22f;
  // ↑ 칸 하나가 0→1 크기로 커지는 데 걸리는 시간(초).
  //   줄이면(0.1) 빠릿하게, 늘리면(0.5) 여유롭게 등장.

  public float staggerStep = 0.03f;
  // ↑ (c.x + c.y) * staggerStep = 각 칸의 등장 시작 딜레이(초).
  //   0으로 하면 모든 칸이 동시에 팡! 나타남.
  //   0.05~0.1 이상이면 물결이 느려져서 각 칸이 뚜렷하게 구분되어 나타남.

  bool appearing = false;
  // ↑ 애니메이션이 돌아가는 동안 true. Update에서 입력을 막는 플래그로 사용.


  // ── 초기화 ──────────────────────────────────────────────────────

  void Awake()
  {
    // Awake는 Start보다 먼저 실행됨
    // 스프라이트를 여기서 만들어야, 곧바로 호출될 수 있는 ShowLevel() 안에서 안전하게 사용 가능
    cellSprite = MakeRoundedSprite(256, 64);
    circleSprite = MakeCircleSprite(128);
    lineMaterial = new Material(Shader.Find("Sprites/Default"));  // 레벨이 바뀌어도 재사용
    audioSource = gameObject.AddComponent<AudioSource>();
    audioSource.playOnAwake = false;
  }

  void OnDestroy()
  {
    if (lineMaterial != null) Destroy(lineMaterial);
  }

  // GameManager가 "이 레벨 그려줘" 하고 호출하는 함수 (외부에서 부를 수 있도록 public)
  public void ShowLevel(LevelData newLevel)
  {
    StopAllCoroutines();   // 이전 등장 애니메이션이 돌고 있으면 중단
    level = newLevel;

    ClearBoard();   // 이전 레벨의 칸·선 오브젝트를 전부 제거

    won = false;
    isDrawing = false;
    armed = false;
    isResetting = false;

    // 격자의 중심이 (0, 0)이 되도록 오프셋 계산
    // 예) 3×3 격자 → offsetX = (3-1)/2 = 1 → x좌표가 -1, 0, 1로 배치됨
    offsetX = (level.width - 1) / 2f;
    offsetY = (level.height - 1) / 2f;

    DrawBoard();    // 칸 오브젝트 생성
    CreateLine();   // 경로 선 오브젝트 생성
    dotAnimated = false;
    CreateStartDot();  // 시작점 흰 원 생성

    // DrawBoard() 다음에 세야 막힌 칸이 제외된 실제 칸 수를 얻을 수 있음
    fillableCount = cells.Count;

    // 시작 칸을 경로의 첫 번째 칸으로 추가 (시작 칸은 이미 밟은 상태로 시작)
    path.Add(level.startCell);
    pathSet.Add(level.startCell);

    Redraw();       // 색상·선 초기 반영
    FitCamera();    // 레벨 크기에 맞게 카메라 범위 자동 조정

    StartCoroutine(AnimateAppear());   // 칸들이 톡톡 나타나기
  }


  // ── 보드 생성·삭제 ──────────────────────────────────────────────

  void ClearBoard()
  {
    // 이 스크립트가 붙은 오브젝트(Board)의 자식을 전부 파괴
    // 칸 스프라이트와 PathLine 오브젝트가 자식으로 붙어 있으므로 모두 제거됨
    foreach (Transform child in transform)
      Destroy(child.gameObject);

    cells.Clear();
    glows.Clear();
    path.Clear();
    pathSet.Clear();
    startDot = null;
    endDot = null;  // Destroy로 제거됐으므로 참조도 비움
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

        // 칸 뒤에 외곽선 추가 (칸보다 살짝 큰 스프라이트 → 테두리처럼 보임)
        GameObject outlineObj = new GameObject("Outline");
        outlineObj.transform.SetParent(obj.transform);
        outlineObj.transform.localPosition = new Vector3(0.04f, -0.04f, 0f);  // 오른쪽·아래로 오프셋 → 해당 방향만 테두리 노출
        outlineObj.transform.localScale = Vector3.one * 1.05f;  // 8% 크게. 키우면 테두리 두꺼워짐

        SpriteRenderer outlineSr = outlineObj.AddComponent<SpriteRenderer>();
        outlineSr.sprite = cellSprite;
        outlineSr.color = outlineColor;
        outlineSr.sortingOrder = -1;  // 칸(order 0) 뒤에 그려짐

        // 칸 뒤에 글로우 스프라이트 추가 (처음엔 투명, 채워질 때 맥동)
        GameObject glowObj = new GameObject("Glow");
        glowObj.transform.SetParent(obj.transform);
        glowObj.transform.localPosition = Vector3.zero;
        glowObj.transform.localScale = Vector3.one * 1.15f;  // 칸보다 45% 더 크게 → 바깥으로 삐져나옴

        SpriteRenderer glowSr = glowObj.AddComponent<SpriteRenderer>();
        glowSr.sprite = cellSprite;
        glowSr.color = new Color(1f, 1f, 1f, 0f);  // 처음엔 완전 투명
        glowSr.sortingOrder = -2;  // 외곽선(-1) 뒤에 그려짐

        glows[coord] = glowSr;
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

    line.startColor = line.endColor = lineColor;  // 선 색상: #ff8a8a 핑크
    line.startWidth = line.endWidth = 0.18f;   // 선 두께(유닛). 키우면 선이 굵어짐
    line.numCapVertices = 6;  // 선 끝부분을 몇 각형으로 둥글게 만들지 (클수록 더 동그래짐)
    line.numCornerVertices = 6;  // 꺾이는 모서리를 몇 각형으로 둥글게 만들지
    line.sortingOrder = 1;       // 칸 스프라이트(기본 order=0) 위에 선이 그려지도록 1로 설정
    line.useWorldSpace = true;
    line.positionCount = 0;      // 아직 경로가 없으므로 점 0개로 시작
  }

  // ── 시작점 닷 ───────────────────────────────────────────────────

  void CreateStartDot()
  {
    GameObject dotObj = new GameObject("StartDot");
    dotObj.transform.SetParent(transform);
    dotObj.transform.position = CellToWorld(level.startCell);
    dotObj.transform.localScale = Vector3.one * 0.35f;  // 칸 크기의 35%

    startDot = dotObj.AddComponent<SpriteRenderer>();
    startDot.sprite = circleSprite;
    startDot.color = Color.white;
    startDot.sortingOrder = 2;  // 선(1)보다 위에 그려짐
  }

  void ResetStartDot()
  {
    if (startDot == null) return;
    startDot.gameObject.SetActive(true);
    startDot.transform.localScale = Vector3.one * 0.35f;
    startDot.color = Color.white;
  }

  // 첫 이동 시 원이 흰색 → 선 색으로 채워지고 그대로 유지
  IEnumerator AnimateStartDot()
  {
    if (startDot == null) yield break;

    float duration = 0.2f;
    float t = 0f;

    while (t < duration)
    {
      t += Time.deltaTime;
      float p = Mathf.Clamp01(t / duration);
      if (startDot == null) yield break;
      startDot.color = Color.Lerp(Color.white, lineColor, p);
      yield return null;
    }

    if (startDot != null) startDot.color = lineColor;
  }

  // 클리어 시 마지막 칸에 흰 원이 팡! 하고 등장
  IEnumerator AnimateEndDot()
  {
    if (path.Count == 0) yield break;

    GameObject dotObj = new GameObject("EndDot");
    dotObj.transform.SetParent(transform);
    dotObj.transform.position = CellToWorld(path[^1]);
    dotObj.transform.localScale = Vector3.zero;

    endDot = dotObj.AddComponent<SpriteRenderer>();
    endDot.sprite = circleSprite;
    endDot.color = lineColor;
    endDot.sortingOrder = 2;

    // EaseOutBack 곡선으로 0 → 0.35 크기로 팡! 등장
    float duration = 0.28f;
    float t = 0f;
    while (t < duration)
    {
      t += Time.deltaTime;
      float p = Mathf.Clamp01(t / duration);
      if (endDot == null) yield break;
      endDot.transform.localScale = Vector3.one * EaseOutBack(p) * 0.35f;
      yield return null;
    }

    if (endDot != null) endDot.transform.localScale = Vector3.one * 0.35f;
  }

  // 안티앨리어싱이 적용된 원형 스프라이트 생성
  Sprite MakeCircleSprite(int size)
  {
    Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    tex.wrapMode = TextureWrapMode.Clamp;
    tex.filterMode = FilterMode.Bilinear;

    float center = (size - 1) / 2f;
    float r = center - 0.5f;

    for (int py = 0; py < size; py++)
    {
      for (int px = 0; px < size; px++)
      {
        float dx = px - center;
        float dy = py - center;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        // 경계 부근 1픽셀을 선형 보간해 부드러운 원 가장자리 처리
        float alpha = Mathf.Clamp01(r - dist + 0.5f);
        tex.SetPixel(px, py, new Color(1f, 1f, 1f, alpha));
      }
    }

    tex.Apply();
    return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
  }


  // ── 입력 처리 (매 프레임 실행) ──────────────────────────────────

  void Update()
  {
    // 클리어됐거나, 등장 애니메이션 중이거나, 자동 리셋 대기 중이거나, 레벨 미로드 시 입력 무시
    if (won || appearing || isResetting || level == null) return;

    AnimateGlow();

    // 손가락(마우스 버튼) 처음 눌렀을 때
    if (Input.GetMouseButtonDown(0))
    {
      Vector2Int c = WorldToCell(Camera.main.ScreenToWorldPoint(Input.mousePosition));

      movedInGesture = false;  // 새 제스처 시작 시 초기화

      if (c == level.startCell)
      {
        // 시작 칸 → 경로 초기화 후 처음부터 다시 그리기
        armed = false;
        isDrawing = true;
        path.Clear();
        pathSet.Clear();
        path.Add(level.startCell);
        pathSet.Add(level.startCell);
        dotAnimated = false;
        ResetStartDot();  // 흰 원 원래 상태로 복원
        Redraw();
      }
      else if (armed)
      {
        // armed 상태에서 두 번째 탭 → 즉시 그리기 시작
        armed = false;
        isDrawing = true;
      }
      else if (path.Count > 0 && c == path[^1])
      {
        // 마지막 칸 탭 → 바로 드래그도 되고, 탭만 하고 떼면 armed 상태로 전환
        isDrawing = true;
        armed = true;  // 탭만 하고 뗐을 때를 대비해 armed 세팅 (이동 성공 시 MouseButtonUp에서 해제)
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
    {
      // 이동이 실제로 일어났으면 armed 해제. 탭만 했으면 armed 유지 → 다음 탭+드래그 가능
      if (movedInGesture) armed = false;
      isDrawing = false;
    }
  }


  // ── 이동 로직 ───────────────────────────────────────────────────

  void TryMove(Vector2Int target)
  {
    Vector2Int head = path[^1];  // 현재 경로의 맨 끝 칸

    // 현재 위치와 같은 칸이면 아무것도 안 함
    if (target == head) return;

    // 맨해튼 거리 = |Δx| + |Δy|. 이 값이 1이면 상하좌우 딱 한 칸 인접
    // (대각선이면 거리가 2가 되므로 자동으로 걸러짐)
    bool adjacent = Mathf.Abs(target.x - head.x) + Mathf.Abs(target.y - head.y) == 1;

    // 세 조건을 모두 만족해야 이동 가능:
    // 1. cells에 있는 칸(막힌 칸이 아님)
    // 2. 인접한 칸(대각선 이동 불가)
    // 3. 아직 경로에 포함되지 않은 칸(이미 지난 칸 재방문 불가)
    if (cells.ContainsKey(target) && adjacent && !pathSet.Contains(target))
    {
      path.Add(target);
      pathSet.Add(target);
      // 시작 칸에서 첫 이동 시 흰 원 팽창 애니메이션 실행
      if (path.Count == 2 && !dotAnimated)
      {
        dotAnimated = true;
        StartCoroutine(AnimateStartDot());
      }
      movedInGesture = true;
      if (fillSound != null) audioSource.PlayOneShot(fillSound);
      if (SettingsManager.Instance != null) SettingsManager.Instance.Vibrate();  // 진동 설정이 켜져 있으면 진동
      StartCoroutine(AnimateCellPop(cells[target]));  // 칸이 추가될 때 팝 애니메이션
      Redraw();
      CheckWin();
      CheckStuck();  // 클리어가 아닌데 갈 곳이 없으면 자동 리셋
    }
  }


  // ── 막힘 감지 및 자동 리셋 ──────────────────────────────────────

  // 현재 경로 끝에서 이동 가능한 칸이 하나라도 있는지 확인
  bool HasAnyMove()
  {
    if (path.Count == 0) return false;
    Vector2Int head = path[^1];
    foreach (var d in dirs)
    {
      Vector2Int next = head + d;
      if (cells.ContainsKey(next) && !pathSet.Contains(next))
        return true;
    }
    return false;
  }

  void CheckStuck()
  {
    // 클리어 상태이거나 이미 리셋 중이면 무시
    if (won || isResetting) return;
    if (!HasAnyMove())
      StartCoroutine(AutoReset());
  }

  IEnumerator AutoReset()
  {
    isResetting = true;

    if (stuckSound != null) audioSource.PlayOneShot(stuckSound);
    // 막힘을 알리기 위해 모든 칸을 빨간색으로 잠깐 표시
    Color stuckColor = new Color(1f, 0.4f, 0.4f);
    foreach (var kv in cells)
      kv.Value.color = stuckColor;

    yield return waitStuck;

    // ShowLevel 안에서 isResetting = false로 초기화됨
    ShowLevel(level);
  }


  // ── 채워진 칸 바깥 글로우 ──────────────────────────────────────
  // 칸보다 큰 핑크 스프라이트(glows)의 알파를 맥동시켜 외곽이 빛나는 것처럼 보이게 함
  void AnimateGlow()
  {
    float t = (Mathf.Sin(Time.time * 2.5f) + 1f) * 0.5f;
    // 알파 0.2 ↔ 0.55 사이를 맥동. 높일수록 더 선명한 글로우
    float alpha = Mathf.Lerp(0.2f, 0.55f, t);

    foreach (var kv in glows)
    {
      bool filled = pathSet.Contains(kv.Key) && kv.Key != level.startCell;
      // 채워진 칸만 글로우 표시, 나머지는 투명 유지
      Color c = kv.Value.color;
      c.a = filled ? alpha : 0f;
      kv.Value.color = c;
    }
  }


  // ── 칸 팝 애니메이션 ────────────────────────────────────────────
  // 칸을 밟는 순간 톡! 하고 커졌다 원래 크기로 돌아오는 효과
  IEnumerator AnimateCellPop(SpriteRenderer sr)
  {
    float duration = 0.18f;   // 애니메이션 총 시간(초). 줄이면 더 빠릿하게, 늘리면 느긋하게
    float peak = 1.3f;        // 최대로 커지는 배율. 1.3 = 30% 더 커짐
    float t = 0f;
    while (t < duration)
    {
      t += Time.deltaTime;
      float p = t / duration;
      // sin(p * π): 0→1→0 곡선 → 중간에 peak까지 부풀었다가 1로 돌아옴
      float scale = 1f + (peak - 1f) * Mathf.Sin(p * Mathf.PI);
      sr.transform.localScale = Vector3.one * scale;
      yield return null;
    }
    sr.transform.localScale = Vector3.one;  // 오차 없이 정확히 원래 크기로 복귀
  }


  // ── 클리어 판정 ─────────────────────────────────────────────────

  void CheckWin()
  {
    // 경로 길이 == 전체 채울 수 있는 칸 수이면 모든 칸을 한 번씩 지난 것 → 클리어
    if (path.Count == fillableCount)
    {
      won = true;
      if (winSound != null) audioSource.PlayOneShot(winSound);
      if (SettingsManager.Instance != null) SettingsManager.Instance.Vibrate();  // 클리어 시 진동
      Redraw();                              // 클리어 색(라벤더)으로 전체 갱신
      StartCoroutine(AnimateEndDot());       // 마지막 칸에 흰 원 팝 등장
      StartCoroutine(AnimateWin());          // 클리어 직후 번쩍 연출 시작
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
      else if (pathSet.Contains(c))
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
    tex.wrapMode = TextureWrapMode.Clamp;    // 텍스처 가장자리가 늘어나지 않도록
    tex.filterMode = FilterMode.Bilinear;       // 확대/축소 시 부드럽게 보이도록

    Color on = Color.white;                   // 사각형 내부 → 흰색 (나중에 sr.color로 색을 덮어씌움)
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

    float boardW = level.width * cellSize;  // 격자 전체 가로 길이(유닛)
    float boardH = level.height * cellSize;  // 격자 전체 세로 길이(유닛)
    float margin = 1f;  // 격자와 화면 끝 사이의 여백. 줄이면 더 꽉 차게, 늘리면 더 여유롭게 보임

    // orthographicSize = 화면 세로의 절반 길이(유닛)
    // 세로 기준 크기와 가로 기준 크기 중 큰 값을 써야 어떤 화면 비율에서도 격자가 잘리지 않음
    float sizeForHeight = boardH / 2f + margin;
    float sizeForWidth = (boardW / 2f + margin) / cam.aspect;  // 가로 길이를 세로 기준으로 환산
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


  // ── 등장 애니메이션 ─────────────────────────────────────────────

  IEnumerator AnimateAppear()
  {
    appearing = true;

    // 모든 칸을 크기 0으로 숨긴 뒤 한 프레임씩 키워 나감
    foreach (var kv in cells)
      kv.Value.transform.localScale = Vector3.zero;

    float t = 0f;
    bool done = false;
    while (!done)
    {
      t += Time.deltaTime;
      done = true;
      foreach (var kv in cells)
      {
        Vector2Int c = kv.Key;
        // c.x + c.y 가 클수록(오른쪽 위 칸일수록) 늦게 시작 → 왼쪽 아래→오른쪽 위 물결
        float delay = (c.x + c.y) * staggerStep;
        // p: 이 칸의 진행도 0→1. delay 이전엔 0, appearDuration 후엔 1
        float p = Mathf.Clamp01((t - delay) / appearDuration);
        kv.Value.transform.localScale = Vector3.one * EaseOutBack(p);
        if (p < 1f) done = false;  // 아직 다 안 끝난 칸이 있으면 루프 유지
      }
      yield return null;  // 다음 프레임까지 대기
    }

    appearing = false;
  }

  // EaseOutBack: 1을 살짝 넘었다가 1로 돌아오는 곡선 (통통 튀는 느낌)
  // x=0 → 0, x=1 → 1 이지만 중간에 1.1~1.2 정도까지 올라갔다 내려옴
  // c1(1.70158)이 클수록 overshoot(튀어오르는 양)이 커짐. 3 이상이면 많이 튐
  float EaseOutBack(float x)
  {
    float c1 = 1.30158f;
    float c3 = c1 + 1f;
    return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
  }


  // ── 클리어 번쩍 애니메이션 ──────────────────────────────────────

  IEnumerator AnimateWin()
  {
    // winColor에서 흰색 방향으로 80% 섞은 색
    // Lerp 비율을 높이면(0.9) 더 새하얗게, 낮추면(0.5) 은은하게 빛남
    Color bright = Color.Lerp(winColor, Color.white, 0.8f);

    int pulseCount = 2;
    // ↑ 번쩍이는 횟수. 3으로 올리면 더 화려하지만 다음 레벨 전환 전까지 시간이 빡빡해짐.

    float pulseDuration = 0.28f;
    // ↑ 번쩍 1회 왕복 시간(초). 짧으면(0.15) 강렬하고, 길면(0.5) 천천히 빛남.
    //   pulseCount * pulseDuration이 GoToNextAfterDelay의 대기(1.0s)보다 짧아야 연출이 잘림.

    float scalePeak = 1.06f;
    // ↑ 번쩍 정점에서 칸이 커지는 배율. 1.0이면 크기 변화 없음, 1.1이면 10% 더 커짐.

    for (int pulse = 0; pulse < pulseCount; pulse++)
    {
      float t = 0f;
      bool done = false;
      while (!done)
      {
        t += Time.deltaTime;
        done = true;
        foreach (var kv in cells)
        {
          Vector2Int c = kv.Key;
          // 등장 애니메이션과 같은 대각선 물결 방향(왼쪽 아래 → 오른쪽 위)
          float delay = (c.x + c.y) * staggerStep;
          float p = (t - delay) / pulseDuration;

          float sinVal = (p >= 0f && p < 1f) ? Mathf.Sin(p * Mathf.PI) : 0f;
          // sinVal: 0→1→0 종 모양. 정점(p=0.5)에서 가장 밝고 가장 크게

          if (p < 0f)
          {
            // 딜레이 대기 중 → winColor·크기 1 유지
            kv.Value.color = winColor;
            kv.Value.transform.localScale = Vector3.one;
            done = false;
          }
          else if (p < 1f)
          {
            // 번쩍 진행 중: 색과 크기를 sin 곡선으로 함께 키웠다 줄임
            kv.Value.color = Color.Lerp(winColor, bright, sinVal);
            kv.Value.transform.localScale = Vector3.one * Mathf.Lerp(1f, scalePeak, sinVal);
            done = false;
          }
          else
          {
            // 이 칸의 이번 펄스 완료 → 원래 상태로 복귀
            kv.Value.color = winColor;
            kv.Value.transform.localScale = Vector3.one;
          }
        }
        yield return null;
      }
      // 두 번째 펄스가 바로 이어지면 칸마다 딜레이 없이 동시에 시작해 더 강하게 보임
    }
  }
}
