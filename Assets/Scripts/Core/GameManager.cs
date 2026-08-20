using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
  public BoardRenderer board;        // 판을 그리는 친구. 인스펙터에서 Board 오브젝트 연결
  public LevelData[] levels;         // 레벨 목록 (asset index 0 = Level_1). 인스펙터에서 드래그로 추가
  public TMP_Text levelText;         // 상단 레벨 표시. "LEVEL {번호}" 형태로 매 판 갱신
  public TMP_Text roundCountText;    // (사용 안 함) 인스펙터 연결 보존
  public RewardManager rewardManager; // 모든 레벨 클리어 시 보상 처리. 인스펙터에서 연결
  public CompletionPanel completionPanel; // 완주 확정 화면. 앱 재시작 시 완주 상태면 자동 표시. 인스펙터에서 연결
  public AdManager adManager;         // 전면 광고 관리. 인스펙터에서 연결
  public LivesSystem livesSystem;     // 목숨 시스템. 리셋 이벤트 -1. 클리어 시엔 손대지 않음(상태 이월)

#if UNITY_EDITOR
  [Header("에디터 전용 (빌드에는 영향 없음)")]
  public LevelData editorTestLevel;  // 값이 있으면 랜덤/진행 무시하고 이 레벨만 계속 로드
#endif

  // 그룹 경계. 진입장벽 낮추기 위해 1~10은 순차 고정, 나머지는 그룹 안에서만 랜덤.
  // 각 그룹의 pool 크기 = slot 수 (모든 asset을 반드시 클리어).
  // asset index 기준: [0,10) [10,70) [70,90) [90,100)
  //   G1 1~10 순차 / G2 11~70 랜덤 / G3 71~90 랜덤 / G4 91~100 랜덤
  // (구 로직에 있던 [70,110) 40-pool·30-pick 재플레이 유도 방식은 폐기됨. asset 100~109는 신 로직에서 안 뽑힘.)
  static readonly int[] GroupBoundaries = { 0, 10, 70, 90, 100 };

  // 유저에게 노출되는 총 판 수 (진행도 표시·보상 트리거 기준).
  // 신 로직에선 asset index [0,100)와 정확히 일치. levels.Length=110은 구 유저 데이터 호환용으로 유지.
  const int TotalDisplayLevels = 100;

  const string LegacyProgressKey = "currentLevel";  // 구버전 유저 마이그레이션용
  const string ClearedCountKey = "clearedCount";  // 지금까지 클리어한 판 수
  const string ClearedMaskKey = "clearedMask";   // "01010..." 형태. 각 자리 = asset index 클리어 여부

  int currentIndex = 0;   // 지금 도전 중인 판의 asset index
  int clearedCount = 0;   // 지금까지 클리어한 판 수 (진행도 표시에 사용)
  bool[] cleared;         // asset index별 클리어 여부

  // 100판 완주 여부. 설정창 "인증코드 보기" 버튼 조건부 노출에 사용.
  public bool IsAllCleared => clearedCount >= TotalDisplayLevels;

  void Start()
  {
    Application.targetFrameRate = 60;
    QualitySettings.vSyncCount = 0;

#if UNITY_EDITOR
    if (editorTestLevel != null)
    {
      ShowEditorTestLevel();
      return;
    }
#endif

    ClearPrefsIfReinstalled();
    LoadProgress();

    if (clearedCount >= TotalDisplayLevels)
    {
      // 완주 상태로 앱 재시작: 신 로직에선 뽑을 asset이 없어 PickAndLoadNext가 아무것도 안 함 → 검은 화면 정지.
      // Level_1을 배경으로 깔고, 이미 코드 있으면 완주 화면 / 없으면 리워드 팝업 자동 재발동.
      LoadLevel(0);
      StartCoroutine(ShowCompletionOrRewardAfterFrame());
    }
    else
    {
      PickAndLoadNext();
    }
  }

  // RewardManager/CompletionPanel의 Start가 먼저 돌 보장이 없어서 1프레임 양보.
  // 로컬 코드 있음 = 이미 발급받음 → 완주 화면. 없음 = 아직 받아야 함 → 리워드 팝업.
  IEnumerator ShowCompletionOrRewardAfterFrame()
  {
    yield return null;
    bool alreadyIssued = rewardManager != null && !string.IsNullOrEmpty(rewardManager.GetLocalCode());
    if (alreadyIssued)
    {
      if (completionPanel != null) completionPanel.Show();
    }
    else
    {
      if (rewardManager != null) rewardManager.ShowReward();
    }
  }

#if UNITY_EDITOR
  // 에디터 테스트 모드: 진행/저장 무시. 매번 이 레벨만 다시 로드.
  void ShowEditorTestLevel()
  {
    if (levelText != null) levelText.text = "LEVEL TEST";
    board.ShowLevel(editorTestLevel);
  }
#endif

  // Android 재설치 감지: firstInstallTime이 달라지면 새로 설치된 것 → PlayerPrefs 초기화
  // Google 자동 백업이 PlayerPrefs를 복원해도 firstInstallTime은 새 값이므로 올바르게 감지됨
  void ClearPrefsIfReinstalled()
  {
#if UNITY_ANDROID && !UNITY_EDITOR
    long currentInstallTime = GetFirstInstallTime();
    // API 예외로 -1 리턴된 경우 진단 불가로 판단하고 스킵.
    // 저장된 기본값("0")과 우연 매치되어 재설치 감지 놓치는 사고 방지.
    if (currentInstallTime < 0) return;

    long storedInstallTime  = long.Parse(PlayerPrefs.GetString("installTime", "0"));

    if (currentInstallTime != storedInstallTime)
    {
      PlayerPrefs.DeleteAll();
      PlayerPrefs.SetString("installTime", currentInstallTime.ToString());
      PlayerPrefs.Save();
    }
#endif
  }

#if UNITY_ANDROID && !UNITY_EDITOR
  long GetFirstInstallTime()
  {
    try
    {
      using var player  = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
      using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
      using var pm       = activity.Call<AndroidJavaObject>("getPackageManager");
      string    pkg      = activity.Call<string>("getPackageName");
      using var info     = pm.Call<AndroidJavaObject>("getPackageInfo", pkg, 0);
      return info.Get<long>("firstInstallTime");
    }
    catch (System.Exception e)
    {
      Debug.LogWarning("firstInstallTime 조회 실패: " + e.Message);
      return -1L;   // 실패 시 -1: ClearPrefsIfReinstalled가 스킵하도록 유도 (안전 fallback)
    }
  }
#endif

  // 저장된 클리어 상태를 로드. 신규 키가 있으면 그걸, 없고 legacy 키만 있으면 마이그레이션
  void LoadProgress()
  {
    if (levels == null || levels.Length == 0) return;
    cleared = new bool[levels.Length];

    if (PlayerPrefs.HasKey(ClearedCountKey))
    {
      int savedCount = PlayerPrefs.GetInt(ClearedCountKey, 0);
      string mask = PlayerPrefs.GetString(ClearedMaskKey, "");
      int n = Mathf.Min(levels.Length, mask.Length);
      for (int i = 0; i < n; i++) cleared[i] = (mask[i] == '1');

      // 신 그룹핑 마이그레이션. 구 로직에선 clearedCount가 asset [0,110) 전체 기준이라
      // asset 100~109 클리어가 카운트에 포함됐음. 신 로직은 [0,100)만 대상이라 재계산 필요.
      if (savedCount >= TotalDisplayLevels)
      {
        // 구버전 완주자: 진행도 유지가 우선. cleared[0..99] 전부 true로 강제해 완주 상태 보장.
        // (안 그러면 asset 100~109 클리어분이 신 로직에선 무의미해서 92/100 같이 밀림)
        int cap = Mathf.Min(cleared.Length, TotalDisplayLevels);
        for (int i = 0; i < cap; i++) cleared[i] = true;
        clearedCount = TotalDisplayLevels;
        SaveProgress();
      }
      else
      {
        // 미완주자: mask[0..99]의 실제 '1' 개수로 재계산. asset 100~109 클리어 흔적은 카운트에서 제외.
        clearedCount = 0;
        int cap = Mathf.Min(cleared.Length, TotalDisplayLevels);
        for (int i = 0; i < cap; i++) if (cleared[i]) clearedCount++;
      }
    }
    else if (PlayerPrefs.HasKey(LegacyProgressKey))
    {
      // 구버전은 "지금 도전 중인 판의 index"만 저장했음 → 그 값이 곧 클리어한 판 수
      // 어느 index를 클리어했는지 정보가 없으니 앞 N개를 클리어한 걸로 간주 (구버전은 순차 진행이었음)
      int legacy = Mathf.Clamp(PlayerPrefs.GetInt(LegacyProgressKey, 0), 0, TotalDisplayLevels);
      clearedCount = legacy;
      for (int i = 0; i < clearedCount; i++) cleared[i] = true;
      SaveProgress();
    }
  }

  void SaveProgress()
  {
    var sb = new StringBuilder(levels.Length);
    for (int i = 0; i < levels.Length; i++) sb.Append(cleared[i] ? '1' : '0');
    PlayerPrefs.SetInt(ClearedCountKey, clearedCount);
    PlayerPrefs.SetString(ClearedMaskKey, sb.ToString());
    PlayerPrefs.Save();
  }

  // clearedCount 기준으로 다음 도전할 판을 뽑아서 로드
  // 앱을 껐다 켤 때도 이 경로를 타므로 매번 재선택됨 (요구사항: 매판 재선택)
  void PickAndLoadNext()
  {
    if (levels == null || levels.Length == 0) return;
    int idx = PickNextIndex();
    // 방어: 그룹 안 모든 판이 cleared[]=true인데 clearedCount는 그룹 경계 아래인 손상 상태.
    // 그대로 두면 게임 화면이 검게 정지. clearedCount를 다음 그룹 시작으로 승격시켜 복구.
    while (idx < 0)
    {
      int next = NextGroupStart(clearedCount);
      if (next == clearedCount) return;   // 이미 마지막 그룹까지 소진 = 실제 완주 상황
      clearedCount = next;
      SaveProgress();
      idx = PickNextIndex();
    }
    LoadLevel(idx);
  }

  // clearedCount가 속한 그룹의 "다음 그룹" 시작 값 반환. 이미 마지막이면 원래값 그대로.
  int NextGroupStart(int count)
  {
    for (int g = 1; g < GroupBoundaries.Length; g++)
      if (count < GroupBoundaries[g]) return GroupBoundaries[g];
    return count;
  }

  int PickNextIndex()
  {
    // 1~10: 순차 (clearedCount가 곧 다음 asset index)
    if (clearedCount < GroupBoundaries[1]) return clearedCount;

    // 현재 clearedCount가 속한 그룹 찾기
    int groupStart = 0, groupEnd = 0;
    for (int g = 1; g < GroupBoundaries.Length; g++)
    {
      if (clearedCount < GroupBoundaries[g])
      {
        groupStart = GroupBoundaries[g - 1];
        groupEnd = GroupBoundaries[g];
        break;
      }
    }

    // 그룹 안 미클리어 풀에서 랜덤 하나
    int max = Mathf.Min(groupEnd, levels.Length);
    var pool = new List<int>(max - groupStart);
    for (int i = groupStart; i < max; i++)
      if (!cleared[i]) pool.Add(i);

    if (pool.Count == 0) return -1;
    return pool[Random.Range(0, pool.Count)];
  }

  void LoadLevel(int index)
  {
    currentIndex = index;
    // 지금 도전 중인 판은 (clearedCount + 1) 번째. 진행도 표시.
    // asset 개수가 아니라 유저 노출 판 수(100) 기준 — 100판 완주 후 남은 그룹 D 여분 판을 풀 때는 "100"에서 캡
    int display = Mathf.Min(clearedCount + 1, TotalDisplayLevels);
    if (levelText != null) levelText.text = $"LEVEL {display}";

    board.ShowLevel(levels[currentIndex]);
  }

  // BoardRenderer가 레벨을 다 채우면 호출
  public void OnLevelSolved()
  {
#if UNITY_EDITOR
    if (editorTestLevel != null)
    {
      // 테스트 모드: 클리어해도 진행 저장 없이 같은 판 다시
      StartCoroutine(ReloadEditorTestAfterDelay());
      return;
    }
#endif

    // 이번 클리어를 마스크에 반영 (같은 판을 중복 카운트하지 않도록 방어)
    if (!cleared[currentIndex])
    {
      cleared[currentIndex] = true;
      clearedCount++;
      SaveProgress();
    }

    // 유저 관점 완주(100판) = 보상 트리거. asset 110개 중 나머지 10개는 완주 후 자유 플레이용
    // (그 판을 클리어해도 RewardManager가 기기ID로 중복 발급 차단하니 동일 코드만 재표시됨)
    bool allDone = clearedCount >= TotalDisplayLevels;
    StartCoroutine(GoToNextAfterDelay(allDone));
  }

#if UNITY_EDITOR
  IEnumerator ReloadEditorTestAfterDelay()
  {
    yield return new WaitForSeconds(1.0f);
    ShowEditorTestLevel();
  }

#endif

#if UNITY_EDITOR
  // 에디터 전용: 100판 클리어까지 안 가고도 보상 팝업 흐름을 검증할 수 있게.
  // 인스펙터에서 GameManager 컴포넌트 우클릭 → 메뉴로 실행.
  // 첫 회는 신규 저장, 이후엔 "이미 발급됨" 경로. 재저장 검증하려면 Firestore Console에서 rewards/{deviceId} 수동 삭제 후 다시 실행.
  [ContextMenu("[TEST] Show Reward Popup")]
  void TestShowRewardPopup()
  {
    if (rewardManager != null) rewardManager.ShowReward();
    else Debug.LogWarning("rewardManager 인스펙터 연결 필요");
  }

  // 에디터 전용: 100판 완주 상태로 강제 세팅. 설정창 "인증코드 보기" 버튼 노출 조건 만족용.
  // 실행 후 설정창 열면 IsAllCleared=true라 버튼이 뜸.
  [ContextMenu("[TEST] Simulate All Cleared")]
  void TestSimulateAllCleared()
  {
    if (cleared == null || cleared.Length == 0)
    {
      Debug.LogWarning("cleared 배열 초기화 안 됨. 플레이 모드에서 실행하세요.");
      return;
    }
    int cap = Mathf.Min(cleared.Length, TotalDisplayLevels);
    for (int i = 0; i < cap; i++) cleared[i] = true;
    clearedCount = TotalDisplayLevels;
    SaveProgress();
    Debug.Log("100판 완주 상태로 세팅됨 (IsAllCleared=true). 설정창 열어서 인증코드 보기 버튼 확인.");
  }
#endif

  IEnumerator GoToNextAfterDelay(bool allDone)
  {
    yield return new WaitForSeconds(1.0f);

    if (allDone)
    {
      if (rewardManager != null) rewardManager.ShowReward();
    }
    else
    {
      if (adManager != null) adManager.OnLevelCleared();
      PickAndLoadNext();
    }
  }

  // BoardRenderer가 막혀서 리셋될 때 호출 → 목숨 -1
  public void OnStuckReset()
  {
    if (livesSystem != null) livesSystem.Decrement();
  }

  // UI Button의 OnClick에서 호출
  public void RestartLevel()
  {
#if UNITY_EDITOR
    if (editorTestLevel != null) { ShowEditorTestLevel(); return; }
#endif
    LoadLevel(currentIndex);
  }
  public void GoToLevel1() { ResetAndRestart(); }

  // 설정창 "처음부터 시작" → 저장된 진행 상황을 지우고 새로 시작
  public void ResetAndRestart()
  {
    if (cleared != null)
      for (int i = 0; i < cleared.Length; i++) cleared[i] = false;
    clearedCount = 0;
    // DeleteKey 대신 SaveProgress로 명시적 0 저장. 저장 실패/지연 리스크 회피 + LoadProgress가 legacy fallback 안 타게 함.
    SaveProgress();
    PlayerPrefs.DeleteKey(LegacyProgressKey);   // legacy는 재도 필요없으므로 삭제
    PlayerPrefs.Save();

    // UX: 하트 최소 3 보장. 광고로 쌓인 여분(4+)은 보존.
    if (livesSystem != null) livesSystem.RefillOnReset();

    PickAndLoadNext();
  }
}
