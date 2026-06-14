using UnityEngine;

public class GameManager : MonoBehaviour
{
    public BoardRenderer board;     // 판을 그리는 친구. 인스펙터에서 Board 오브젝트 연결
    public LevelData[] levels;      // 레벨 목록 (순서대로). 인스펙터에서 드래그로 추가
    public GameObject clearPanel;   // 클리어 화면. 인스펙터에서 연결

    // PlayerPrefs에 저장할 키. 문자열 오타를 방지하기 위해 const로 선언
    const string ProgressKey = "currentLevel";
    int currentIndex = 0;

    void Start()
    {
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
        // 레벨 이동마다 저장해 앱 재시작 시 이어서 플레이
        PlayerPrefs.SetInt(ProgressKey, currentIndex);
        PlayerPrefs.Save();

        if (clearPanel != null) clearPanel.SetActive(false);
        board.ShowLevel(levels[currentIndex]);   // BoardRenderer에게 이 레벨을 그리라고 시킴
    }

    // BoardRenderer가 레벨을 다 채우면 호출
    public void OnLevelSolved()
    {
        Debug.Log("클리어!");
        if (clearPanel != null) clearPanel.SetActive(true);
    }

    // UI Button의 OnClick에서 호출
    public void RestartLevel() { LoadLevel(currentIndex); }
    public void NextLevel()    { LoadLevel(currentIndex + 1); }

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
