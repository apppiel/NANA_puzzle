using System.Collections;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
  public BoardRenderer board;        // 판을 그리는 친구. 인스펙터에서 Board 오브젝트 연결
  public LevelData[] levels;         // 레벨 목록 (순서대로). 인스펙터에서 드래그로 추가
  public TMP_Text levelText;         // 레벨 번호 표시용
  public TMP_Text roundCountText;    // 현재라운드 / 전체라운드 표시용. 인스펙터에서 TMP Text 연결
  public RewardManager rewardManager; // 모든 레벨 클리어 시 보상 처리. 인스펙터에서 연결
  public AdManager adManager;         // 전면 광고 관리. 인스펙터에서 연결

  // PlayerPrefs에 저장할 키. 문자열 오타를 방지하기 위해 const로 선언
  const string ProgressKey = "currentLevel";
  int currentIndex = 0;

  void Start()
  {
    Application.targetFrameRate = 60;  // 목표 프레임을 60으로 고정. 30으로 낮추면 배터리 소모 줄어듦
    QualitySettings.vSyncCount = 0;    // vSync를 끄지 않으면 targetFrameRate가 무시되고 화면 주사율에 묶임

    // PlayerPrefs는 앱이 꺼져도 유지되는 단순 저장소 (에디터에서도 유지됨)
    int saved = PlayerPrefs.GetInt(ProgressKey, 0);   // 저장된 레벨 (없으면 0)
    LoadLevel(saved);
  }

  void LoadLevel(int index)
  {
    if (levels == null || levels.Length == 0) return;
    // BoardRenderer와 달리 순환하지 않고 마지막 레벨에서 멈춤
    // (마지막 레벨 클리어 후 "다음" 버튼을 눌러도 같은 레벨 유지)
    if (index >= levels.Length) index = levels.Length - 1;

    currentIndex = index;
    if (levelText != null) levelText.text = "Level " + (currentIndex + 1);
    if (roundCountText != null) roundCountText.text = (currentIndex + 1) + " / " + levels.Length;
    // 레벨 이동마다 저장해 앱 재시작 시 이어서 플레이
    PlayerPrefs.SetInt(ProgressKey, currentIndex);
    PlayerPrefs.Save();

    board.ShowLevel(levels[currentIndex]);   // BoardRenderer에게 이 레벨을 그리라고 시킴
  }

  // BoardRenderer가 레벨을 다 채우면 호출
  public void OnLevelSolved()
  {
    // 마지막 레벨 클리어 여부 판단
    bool isLastLevel = currentIndex == levels.Length - 1;
    StartCoroutine(GoToNextAfterDelay(isLastLevel));
  }

  IEnumerator GoToNextAfterDelay(bool isLastLevel)
  {
    yield return new WaitForSeconds(1.0f);

    if (isLastLevel)
    {
      // 모든 레벨 클리어 → 보상 코드 발급
      if (rewardManager != null)
        rewardManager.ShowReward();
    }
    else
    {
      // 광고 카운트 증가 (N레벨마다 전면 광고 표시)
      if (adManager != null) adManager.OnLevelCleared();
      NextLevel();
    }
  }

  // BoardRenderer가 막혀서 리셋될 때 호출 → 광고 카운트 증가
  public void OnStuckReset()
  {
    if (adManager != null) adManager.OnStuckReset();
  }

  // UI Button의 OnClick에서 호출
  public void RestartLevel() { LoadLevel(currentIndex); }
  public void GoToLevel1() { LoadLevel(0); }
  public void NextLevel()
  {
    int next = currentIndex + 1;
    if (next < levels.Length) LoadLevel(next);
  }
}
