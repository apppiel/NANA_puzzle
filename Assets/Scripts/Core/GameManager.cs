using System.Collections;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
  public BoardRenderer board;     // 판을 그리는 친구. 인스펙터에서 Board 오브젝트 연결
  public LevelData[] levels;      // 레벨 목록 (순서대로). 인스펙터에서 드래그로 추가
  public TMP_Text levelText;   // 레벨 번호 표시용

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
    // 레벨 이동마다 저장해 앱 재시작 시 이어서 플레이
    PlayerPrefs.SetInt(ProgressKey, currentIndex);
    PlayerPrefs.Save();

    board.ShowLevel(levels[currentIndex]);   // BoardRenderer에게 이 레벨을 그리라고 시킴
  }

  // BoardRenderer가 레벨을 다 채우면 호출
  public void OnLevelSolved()
  {
    Debug.Log("클리어!");
    StartCoroutine(GoToNextAfterDelay());
  }

  IEnumerator GoToNextAfterDelay()
  {
    yield return new WaitForSeconds(1.0f);
    // ↑ 0.6 → 0.8으로 늘림. AnimateWin 번쩍 연출(~0.35s + stagger 여분)을 다 보고 넘어가게.
    //   연출이 너무 빨리 끊긴다 싶으면 1.0~1.2 정도로 올릴 것.
    NextLevel();
  }

  // UI Button의 OnClick에서 호출
  public void RestartLevel() { LoadLevel(currentIndex); }
  public void NextLevel() { LoadLevel(currentIndex + 1); }



  // 테스트용: 저장 데이터를 지우고 1번 레벨로 돌아감. UI 버튼에 연결하거나 인스펙터 우클릭으로 실행
  public void ResetToFirstLevel()
  {
    PlayerPrefs.DeleteKey(ProgressKey);
    PlayerPrefs.Save();
    LoadLevel(0);
  }

  // 인스펙터에서 이 컴포넌트를 우클릭 → "Reset Progress" 선택 시 실행
  // 저장된 레벨 진행 상황을 삭제해 다음 플레이부터 레벨 1로 시작
  [ContextMenu("Reset Progress")]
  void ResetProgress()
  {
    PlayerPrefs.DeleteKey(ProgressKey);
    PlayerPrefs.Save();
    Debug.Log("진행 상황 초기화됨 (다음 Play부터 레벨 1)");
  }
}
