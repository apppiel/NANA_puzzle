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
  public static LivesSystem Instance { get; private set; }

  [Header("HUD")]
  public AdManager adManager;          // 보상형 광고 호출
  public TMP_Text livesText;           // 상단 "x3" 표시. 없어도 동작함
  public TMP_Text recoveryTimerText;   // 하트 옆 다음 회복까지 카운트다운(MM:SS). 3 미만일 때만 표시. 없어도 동작함
  public Button fillButton;            // "+ 목숨 채우기" 버튼. 없어도 동작함
  public LoadingScreen loadingScreen;  // 로딩 끝나기 전까지 잠금 팝업 표시를 지연. 미연결 시 즉시 표시

  [Header("잠금 팝업 (씬에 만들어둔 UI 연결)")]
  [SerializeField] GameObject lockPopup;   // 잠금 팝업 루트 (LivesLockCanvas). 초기 SetActive false. Show/Hide = SetActive 토글
  [SerializeField] Text statusText;        // 팝업 카드 안 상태 메시지 ("하트 불러오는 중..", "하트가 길을 잃었어요..." 등)
  [SerializeField] Button watchButton;     // 팝업 안 "하트 채우러 가기" 버튼

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

  void Awake()
  {
    Instance = this;
  }

  IEnumerator Start()
  {
    LoadState();
    ApplyAutoRecovery();
    UpdateHudText();
    UpdateRecoveryTimerText();
    RefreshRecoveryNotification();

    if (fillButton != null) fillButton.onClick.AddListener(RequestRewardedAd);
    if (watchButton != null) watchButton.onClick.AddListener(RequestRewardedAd);

    // AdManager의 보상형 광고 상태 이벤트 구독 (대기 중이면 버튼 비활성화 + 잠금 오버레이 statusText 갱신)
    if (adManager != null) adManager.OnRewardedStatus += HandleAdStatus;

    // 하트 < 3인 동안 매초 회복 체크 + 타이머 UI 갱신 + 잠금 오버레이 자동 해제.
    StartCoroutine(TickRecovery());

    // 로딩 스크린이 alpha 페이드 후 gameObject.SetActive(false) 될 때까지 대기.
    // 안 하면 로딩 화면 위에 잠금 팝업이 겹쳐 보임.
    if (loadingScreen != null)
      yield return new WaitUntil(() => !loadingScreen.gameObject.activeSelf);

    // 이전 세션에서 잠긴 상태로 종료했다면 로딩 끝난 뒤 UI 표시
    if (current <= 0) ShowLockOverlay();
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
    RefreshRecoveryNotification();
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
      RefreshRecoveryNotification();
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
    RefreshRecoveryNotification();
    HideLockOverlay();
  }

  // "+ 목숨 채우기" 버튼과 잠금 오버레이 "광고 보기" 버튼 공용.
  // 준비 안 됐어도 AdManager가 대기 상태로 걸어두고 로드 완료 시 자동 표시. 유저는 재탭 불필요.
  // "광고 준비 중..." 안내는 OnRewardedStatus 이벤트 → HandleAdStatus에서 UI 반영.
  void RequestRewardedAd()
  {
    if (adManager == null) { Debug.LogWarning("AdManager 미연결"); return; }
    adManager.ShowRewardedAd(ApplyRewardedAdReward);
  }

  // AdManager.OnRewardedStatus 콜백. 잠금 오버레이 statusText 갱신 + 버튼 활성화 토글.
  // 로드 중일 때만 버튼 비활성화(회색). 실패/idle 상태면 활성화해 재시도 가능.
  // 버튼 라벨은 텍스트 스왑하지 않음 — "+3" 같은 작은 라벨에 긴 메시지가 들어가면 UI 깨짐.
  void HandleAdStatus(string msg)
  {
    if (statusText != null) statusText.text = msg;
    if (fillButton != null)
      fillButton.interactable = msg != AdManager.RewardedLoadingMessage;
  }

  void OnDestroy()
  {
    if (adManager != null) adManager.OnRewardedStatus -= HandleAdStatus;
    if (Instance == this) Instance = null;
  }

  // ── UI ───────────────────────────────────────────────

  void UpdateHudText()
  {
    if (livesText != null) livesText.text = "x" + current;
  }

  // 잠금 팝업 = 씬에 만들어둔 GameObject를 SetActive 토글로 열고 닫음.
  // (예전엔 코드로 Canvas/Card/Text를 동적으로 만들었음 — 인스펙터 편집 불가라 시각 iteration이 힘들어 씬 배치로 마이그레이션함)
  void ShowLockOverlay()
  {
    if (lockPopup != null) lockPopup.SetActive(true);
  }

  void HideLockOverlay()
  {
    if (lockPopup != null) lockPopup.SetActive(false);
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
      if (current != prev)
      {
        UpdateHudText();
        RefreshRecoveryNotification();
      }
      UpdateRecoveryTimerText();
      if (current > 0 && lockPopup != null && lockPopup.activeSelf) HideLockOverlay();
      yield return wait;
    }
  }

  // 하트 회복 완료 시각을 계산해서 알림 예약. 상태가 바뀔 때마다 호출됨.
  // 설정 토글 OFF, 이미 가득참, 앵커 없음 중 하나면 취소만.
  public void RefreshRecoveryNotification()
  {
    if (NotificationHelper.Instance == null) return;
    bool enabled = SettingsManager.Instance == null || SettingsManager.Instance.NotificationOn;
    if (!enabled || current >= BaseLives || lastLostAt <= 0)
    {
      NotificationHelper.Instance.Cancel();
      return;
    }
    long fullAtMs = lastLostAt + (long)(BaseLives - current) * RecoverIntervalMs;
    var fullAt = DateTimeOffset.FromUnixTimeMilliseconds(fullAtMs).LocalDateTime;
    NotificationHelper.Instance.ScheduleHeartFull(fullAt);
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
}
