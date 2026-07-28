using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 목숨(하트) 시스템.
// - 목숨 값 하나로 관리 (기본 3, 상한 99)
// - 리셋 시 -1, 0 되면 잠금 오버레이
// - 광고 시청 시 +3
// - 판 클리어 시: 3 미만이면 3으로 리필, 3 이상이면 그대로 유지 (누적 자원 소모형)
// - 자동 복구: 3 미만이면 10분마다 1개씩 회복 (상한 3). 하트 옆 recoveryTimerText에 다음 회복까지 MM:SS 표시.
public class LivesSystem : MonoBehaviour
{
  [Header("연결")]
  public AdManager adManager;          // 보상형 광고 호출
  public TMP_Text livesText;           // 상단 "x3" 표시. 없어도 동작함
  public TMP_Text recoveryTimerText;   // 하트 옆 다음 회복까지 카운트다운(MM:SS). 3 미만일 때만 표시. 없어도 동작함
  public Button fillButton;          // "+ 목숨 채우기" 버튼. 없어도 동작함
  public LoadingScreen loadingScreen;  // 로딩 끝나기 전까지 잠금 팝업 표시를 지연. 미연결 시 즉시 표시

  // 카드·버튼 배경용 라운드 스프라이트. Start()에서 procedural로 자동 생성. 인스펙터 세팅 불필요.
  Sprite roundedSprite;

  // 규칙 상수 (설계 값. 튜닝은 여기서만)
  const int BaseLives = 3;                    // 기본값 · 자동 복구 상한 · 판 클리어 리필 목표
  const int MaxLives = 99;                   // 광고 누적 절대 상한
  const int RewardPerAd = 3;
  const long RecoverIntervalMs = 10L * 60L * 1000L;    // 10분마다 1개 회복

  // PlayerPrefs 키
  const string CurrentKey = "livesCurrent";
  const string LostAtKey = "livesLostAt";

  // 상태
  int current;
  long lastLostAt;   // 다음 회복 tick의 앵커(Unix ms). 회복 발생 시 그만큼 앞으로 이동. 3 이상이면 0.

  // 잠금 오버레이
  Canvas overlayCanvas;
  Text statusText;   // 팝업 안 상태 메시지("광고 준비 중..." 등)

  IEnumerator Start()
  {
    LoadState();
    ApplyAutoRecovery();
    UpdateHudText();
    UpdateRecoveryTimerText();

    // 라운드 스프라이트 자동 생성 (팝업 카드·버튼 배경에서 재사용)
    roundedSprite = BuildRoundedSprite(64, 16);

    if (fillButton != null) fillButton.onClick.AddListener(RequestRewardedAd);

    // 하트 < 3인 동안 매초 회복 체크 + 타이머 UI 갱신 + 잠금 오버레이 자동 해제.
    StartCoroutine(TickRecovery());

    // 로딩 스크린이 alpha 페이드 후 gameObject.SetActive(false) 될 때까지 대기.
    // 안 하면 로딩 화면 위에 잠금 팝업이 겹쳐 보임.
    if (loadingScreen != null)
      yield return new WaitUntil(() => !loadingScreen.gameObject.activeSelf);

    // 이전 세션에서 잠긴 상태로 종료했다면 로딩 끝난 뒤 UI 표시
    if (current <= 0) ShowLockOverlay();
  }

  // 흰색 라운드 사각형 스프라이트를 코드로 생성. 9-slice 설정까지 포함해서 어떤 크기로 늘려도 코너 유지됨.
  // size=64, radius=16이면 코너 반지름이 16px인 은은한 라운드.
  static Sprite BuildRoundedSprite(int size, int radius)
  {
    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    tex.filterMode = FilterMode.Bilinear;

    var pixels = new Color32[size * size];
    var transparent = new Color32(255, 255, 255, 0);
    var opaque = new Color32(255, 255, 255, 255);
    int r2 = radius * radius;

    for (int y = 0; y < size; y++)
    {
      for (int x = 0; x < size; x++)
      {
        // 각 코너 영역까지의 거리 계산. 코너 밖(원 밖)이면 투명 처리.
        int dx = 0, dy = 0;
        if (x < radius) dx = radius - x;
        else if (x >= size - radius) dx = x - (size - radius - 1);
        if (y < radius) dy = radius - y;
        else if (y >= size - radius) dy = y - (size - radius - 1);

        pixels[y * size + x] = (dx * dx + dy * dy) <= r2 ? opaque : transparent;
      }
    }
    tex.SetPixels32(pixels);
    tex.Apply();

    // border 값이 9-slice 코너 크기. Image.Type.Sliced와 조합돼야 라운드 유지됨.
    return Sprite.Create(
      tex,
      new Rect(0, 0, size, size),
      new Vector2(0.5f, 0.5f),
      100f,
      0,
      SpriteMeshType.FullRect,
      new Vector4(radius, radius, radius, radius));
  }

  // ── 상태 저장/로드 ─────────────────────────────────────

  void LoadState()
  {
    current = Mathf.Clamp(PlayerPrefs.GetInt(CurrentKey, BaseLives), 0, MaxLives);
    long.TryParse(PlayerPrefs.GetString(LostAtKey, "0"), out lastLostAt);
  }

  void SaveState()
  {
    PlayerPrefs.SetInt(CurrentKey, current);
    PlayerPrefs.SetString(LostAtKey, lastLostAt.ToString());
    PlayerPrefs.Save();
  }

  // ── 자동 복구 (10분마다 1개씩, 상한 3) ─────────────────

  // 앱 재실행 시점·매초 TickRecovery에서 호출됨.
  // lastLostAt 이후 경과 시간을 10분으로 나눈 만큼 회복. 앵커를 그만큼 앞으로 이동시켜 남은 카운트 유지.
  // 앱을 오래 껐다 켰을 때도 정확히 계산됨(예: 25분 후 켜면 2개 회복 + 5분 남은 카운트 유지).
  void ApplyAutoRecovery()
  {
    if (current >= BaseLives) { lastLostAt = 0; return; }
    if (lastLostAt <= 0) return;

    long elapsed = NowMs() - lastLostAt;
    if (elapsed < RecoverIntervalMs) return;

    int ticks = (int)(elapsed / RecoverIntervalMs);
    int need = BaseLives - current;
    int gained = Mathf.Min(ticks, need);
    current += gained;
    if (current >= BaseLives) lastLostAt = 0;
    else lastLostAt += (long)gained * RecoverIntervalMs;
    SaveState();
  }

  static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

  // ── 게임 이벤트 응답 ────────────────────────────────────

  // 판 안에서 리셋 발생 → 목숨 -1. GameManager.OnStuckReset에서 호출
  public void Decrement()
  {
    if (current <= 0) return;
    current--;
    // 3 미만 진입 순간(정확히는 3→2 순간)에만 카운트 시작. 이미 진행 중이면 유지.
    if (current < BaseLives && lastLostAt == 0)
      lastLostAt = NowMs();
    SaveState();
    UpdateHudText();
    UpdateRecoveryTimerText();
    if (current <= 0) ShowLockOverlay();
  }

  // 판 클리어 → 3 미만이면 3으로 리필. 3 이상(누적된 여분)이면 그대로 유지. GameManager.OnLevelSolved에서 호출
  public void OnLevelCleared()
  {
    if (current < BaseLives)
    {
      current = BaseLives;
      lastLostAt = 0;
      SaveState();
      UpdateHudText();
      UpdateRecoveryTimerText();
      HideLockOverlay();
    }
    // else: current 그대로 유지 (누적 자원은 소모형)
  }

  // 광고 시청 완료 시 실행되는 콜백. 그냥 +3 (99 상한). max 개념 없음.
  void ApplyRewardedAdReward()
  {
    current = Mathf.Min(MaxLives, current + RewardPerAd);
    if (current >= BaseLives) lastLostAt = 0;   // 3 도달 시 카운트 종료
    SaveState();
    UpdateHudText();
    UpdateRecoveryTimerText();
    HideLockOverlay();
  }

  // "+ 목숨 채우기" 버튼과 잠금 오버레이 "광고 보기" 버튼 공용
  void RequestRewardedAd()
  {
    if (adManager == null) { Debug.LogWarning("AdManager 미연결"); return; }

    bool shown = adManager.ShowRewardedAd(ApplyRewardedAdReward);
    if (!shown) SetLockStatus("광고 준비 중입니다. 잠시 후 다시 시도해주세요.");
  }

  // ── UI ───────────────────────────────────────────────

  void UpdateHudText()
  {
    if (livesText != null) livesText.text = "x" + current;
  }

  void SetLockStatus(string msg)
  {
    if (statusText != null) statusText.text = msg;
  }

  // 잠금 오버레이. UpdateChecker와 같은 방식(코드로 Canvas·Card 생성)
  void ShowLockOverlay()
  {
    if (overlayCanvas != null) return;

    var canvasGo = new GameObject("LivesLockCanvas");
    canvasGo.transform.SetParent(transform);
    overlayCanvas = canvasGo.AddComponent<Canvas>();
    overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
    overlayCanvas.sortingOrder = 29000;   // UpdateChecker(30000)보다 낮게 두어 강제 업데이트 팝업 우선
    canvasGo.AddComponent<CanvasScaler>();
    canvasGo.AddComponent<GraphicRaycaster>();

    var bg = MakeImage(canvasGo, "Background", new Color(0, 0, 0, 0.7f));
    Stretch(bg.rectTransform);

    var cardGo = new GameObject("Card");
    cardGo.transform.SetParent(canvasGo.transform, false);
    var card = cardGo.AddComponent<Image>();
    card.color = Color.white;
    ApplyRoundedSprite(card);
    var cardRt = card.rectTransform;
    cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
    cardRt.anchoredPosition = Vector2.zero;
    cardRt.sizeDelta = new Vector2(720, 520);

    AddText(cardGo, "Title", "하트를 모두 소진했어요",
      36, Color.black, TextAnchor.MiddleCenter,
      new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
      new Vector2(20, -90), new Vector2(-20, -20));

    AddText(cardGo, "Message",
      "과자가 기다리고 있어요!!",
      24, new Color(0.2f, 0.2f, 0.2f), TextAnchor.MiddleCenter,
      new Vector2(0, 0.6f), new Vector2(1, 0.78f), new Vector2(0.5f, 0.5f),
      new Vector2(30, 0), new Vector2(-30, 0));

    statusText = AddText(cardGo, "Status", "",
      28, new Color(0.5f, 0.3f, 0.3f), TextAnchor.MiddleCenter,
      new Vector2(0, 0.35f), new Vector2(1, 0.58f), new Vector2(0.5f, 0.5f),
      new Vector2(30, 0), new Vector2(-30, 0));

    AddButton(cardGo, "Watch", "하트 채우러 가기",
      new Vector2(0.1f, 0.06f), new Vector2(0.9f, 0.28f),
      new Color(1f, 0.54f, 0.54f), Color.white,
      RequestRewardedAd,
      roundedSprite);
  }

  void HideLockOverlay()
  {
    if (overlayCanvas == null) return;
    Destroy(overlayCanvas.gameObject);
    overlayCanvas = null;
    statusText = null;
  }

  // 매초 회복 체크 + 하트 옆 타이머 UI 갱신 + 잠금 오버레이 자동 해제.
  // 앱 실행 중 계속 돎(오브젝트 파괴 시 자동 종료).
  IEnumerator TickRecovery()
  {
    var wait = new WaitForSeconds(1f);
    while (true)
    {
      int prev = current;
      ApplyAutoRecovery();
      if (current != prev) UpdateHudText();
      UpdateRecoveryTimerText();
      if (current > 0 && overlayCanvas != null) HideLockOverlay();
      yield return wait;
    }
  }

  void UpdateRecoveryTimerText()
  {
    if (recoveryTimerText == null) return;
    if (current >= BaseLives || lastLostAt <= 0)
    {
      recoveryTimerText.text = "";
      return;
    }
    long remain = RecoverIntervalMs - (NowMs() - lastLostAt);
    if (remain < 0) remain = 0;
    int mins = (int)(remain / 60000);
    int secs = (int)((remain / 1000) % 60);
    recoveryTimerText.text = $"{mins:D2}:{secs:D2}";
  }

  // ── UI 헬퍼 (UpdateChecker와 동일 스타일) ────────────────

  static Image MakeImage(GameObject parent, string name, Color color)
  {
    var go = new GameObject(name);
    go.transform.SetParent(parent.transform, false);
    var img = go.AddComponent<Image>();
    img.color = color;
    return img;
  }

  static void Stretch(RectTransform rt)
  {
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.offsetMin = rt.offsetMax = Vector2.zero;
  }

  static Text AddText(GameObject parent, string name, string content,
    int fontSize, Color color, TextAnchor align,
    Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
    Vector2 offsetMin, Vector2 offsetMax)
  {
    var go = new GameObject(name);
    go.transform.SetParent(parent.transform, false);
    var text = go.AddComponent<Text>();
    text.text = content;
    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    text.fontSize = fontSize;
    text.color = color;
    text.alignment = align;
    var rt = text.rectTransform;
    rt.anchorMin = anchorMin;
    rt.anchorMax = anchorMax;
    rt.pivot = pivot;
    rt.offsetMin = offsetMin;
    rt.offsetMax = offsetMax;
    return text;
  }

  static void AddButton(GameObject parent, string name, string label,
    Vector2 anchorMin, Vector2 anchorMax,
    Color bgColor, Color textColor,
    UnityEngine.Events.UnityAction onClick,
    Sprite bgSprite = null)
  {
    var btnGo = new GameObject(name);
    btnGo.transform.SetParent(parent.transform, false);
    var img = btnGo.AddComponent<Image>();
    img.color = bgColor;
    if (bgSprite != null)
    {
      img.sprite = bgSprite;
      img.type = Image.Type.Sliced;  // 9-slice 설정된 스프라이트여야 라운드 유지
    }
    var rt = img.rectTransform;
    rt.anchorMin = anchorMin;
    rt.anchorMax = anchorMax;
    rt.offsetMin = rt.offsetMax = Vector2.zero;

    var btn = btnGo.AddComponent<Button>();
    btn.onClick.AddListener(onClick);

    AddText(btnGo, "Text", label,
      28, textColor, TextAnchor.MiddleCenter,
      Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
      Vector2.zero, Vector2.zero);
  }

  // 인스펙터에 스프라이트 지정된 경우에만 카드/버튼 배경에 적용. 없으면 각진 흰 사각형으로 폴백.
  void ApplyRoundedSprite(Image img)
  {
    if (roundedSprite == null) return;
    img.sprite = roundedSprite;
    img.type = Image.Type.Sliced;
  }
}
