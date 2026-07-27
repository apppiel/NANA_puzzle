using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
  public BoardRenderer board;        // 판을 그리는 친구. 인스펙터에서 Board 오브젝트 연결
  public LevelData[] levels;         // 레벨 목록 (asset index 0 = Level_1). 인스펙터에서 드래그로 추가
  public TMP_Text levelText;         // 진행도 표시용 (예: "42 / 100")
  public TMP_Text roundCountText;    // 진행도 표시용 (예: "42 / 100")
  public RewardManager rewardManager; // 모든 레벨 클리어 시 보상 처리. 인스펙터에서 연결
  public AdManager adManager;         // 전면 광고 관리. 인스펙터에서 연결

#if UNITY_EDITOR
  [Header("에디터 전용 (빌드에는 영향 없음)")]
  public LevelData editorTestLevel;  // 값이 있으면 랜덤/진행 무시하고 이 레벨만 계속 로드
#endif

  // 그룹 경계. 진입장벽 낮추기 위해 1~10은 순차 고정, 나머지는 그룹 안에서만 랜덤
  // asset index 기준: [0,10) [10,30) [30,70) [70,100)
  static readonly int[] GroupBoundaries = { 0, 10, 30, 70, 100 };

  const string LegacyProgressKey = "currentLevel";  // 구버전 유저 마이그레이션용
  const string ClearedCountKey   = "clearedCount";  // 지금까지 클리어한 판 수
  const string ClearedMaskKey    = "clearedMask";   // "01010..." 형태. 각 자리 = asset index 클리어 여부

  int currentIndex = 0;   // 지금 도전 중인 판의 asset index
  int clearedCount = 0;   // 지금까지 클리어한 판 수 (진행도 표시에 사용)
  bool[] cleared;         // asset index별 클리어 여부

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
    PickAndLoadNext();
  }

#if UNITY_EDITOR
  // 에디터 테스트 모드: 진행/저장 무시. 매번 이 레벨만 다시 로드.
  void ShowEditorTestLevel()
  {
    if (levelText != null)      levelText.text      = "TEST";
    if (roundCountText != null) roundCountText.text = "TEST";
    board.ShowLevel(editorTestLevel);
  }
#endif

  // Android 재설치 감지: firstInstallTime이 달라지면 새로 설치된 것 → PlayerPrefs 초기화
  // Google 자동 백업이 PlayerPrefs를 복원해도 firstInstallTime은 새 값이므로 올바르게 감지됨
  void ClearPrefsIfReinstalled()
  {
#if UNITY_ANDROID && !UNITY_EDITOR
    long currentInstallTime = GetFirstInstallTime();
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
      return 0L;
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
      clearedCount = Mathf.Clamp(PlayerPrefs.GetInt(ClearedCountKey, 0), 0, levels.Length);
      string mask = PlayerPrefs.GetString(ClearedMaskKey, "");
      int n = Mathf.Min(levels.Length, mask.Length);
      for (int i = 0; i < n; i++) cleared[i] = (mask[i] == '1');
    }
    else if (PlayerPrefs.HasKey(LegacyProgressKey))
    {
      // 구버전은 "지금 도전 중인 판의 index"만 저장했음 → 그 값이 곧 클리어한 판 수
      // 어느 index를 클리어했는지 정보가 없으니 앞 N개를 클리어한 걸로 간주 (구버전은 순차 진행이었음)
      int legacy = Mathf.Clamp(PlayerPrefs.GetInt(LegacyProgressKey, 0), 0, levels.Length);
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
    if (idx < 0) return;
    LoadLevel(idx);
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
        groupEnd   = GroupBoundaries[g];
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
    string progress = (clearedCount + 1) + " / " + levels.Length;
    if (levelText != null)      levelText.text      = progress;
    if (roundCountText != null) roundCountText.text = progress;

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

    bool allDone = clearedCount >= levels.Length;
    StartCoroutine(GoToNextAfterDelay(allDone));
  }

#if UNITY_EDITOR
  IEnumerator ReloadEditorTestAfterDelay()
  {
    yield return new WaitForSeconds(1.0f);
    ShowEditorTestLevel();
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

  // BoardRenderer가 막혀서 리셋될 때 호출 → 광고 카운트 증가
  public void OnStuckReset()
  {
    if (adManager != null) adManager.OnStuckReset();
  }

  // UI Button의 OnClick에서 호출
  public void RestartLevel()
  {
#if UNITY_EDITOR
    if (editorTestLevel != null) { ShowEditorTestLevel(); return; }
#endif
    LoadLevel(currentIndex);
  }
  public void GoToLevel1()   { ResetAndRestart(); }

  // 설정창 "처음부터 시작" → 저장된 진행 상황을 지우고 새로 시작
  public void ResetAndRestart()
  {
    PlayerPrefs.DeleteKey(LegacyProgressKey);
    PlayerPrefs.DeleteKey(ClearedCountKey);
    PlayerPrefs.DeleteKey(ClearedMaskKey);
    PlayerPrefs.Save();

    if (cleared != null)
      for (int i = 0; i < cleared.Length; i++) cleared[i] = false;
    clearedCount = 0;
    PickAndLoadNext();
  }
}
