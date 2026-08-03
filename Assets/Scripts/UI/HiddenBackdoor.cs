using UnityEngine;
using UnityEngine.EventSystems;

// v1.0.10 임시 백도어: 완주자였다가 실수로 [1레벨로 돌아가기]로 진행도 리셋된 유저 구제용.
// 상단 하트 아이콘 GameObject에 부착. 10회 연속 탭 시 100판 완주 상태로 세팅 + 리워드 팝업 표시.
// 다음 업데이트에서 파일째 삭제 예정.
//
// 필수 세팅:
//  - 부착 GameObject에 Image(또는 다른 Graphic) 컴포넌트가 있어야 IPointerClickHandler 이벤트 수신됨
//  - Image의 Raycast Target 체크되어 있어야 함 (기본값 true)
//  - 씬에 EventSystem 존재해야 함 (당연히 있음)
public class HiddenBackdoor : MonoBehaviour, IPointerClickHandler
{
    public GameManager gameManager;   // 인스펙터에서 GameManager 연결

    const int TriggerCount = 10;      // 트리거 필요 탭 수
    const float TapWindow = 3f;       // 마지막 탭 이후 이 시간 지나면 카운트 리셋 (오조작 방지)

    int tapCount = 0;
    float lastTapTime = 0f;

    public void OnPointerClick(PointerEventData eventData)
    {
        // 오래 지나면 리셋
        if (Time.unscaledTime - lastTapTime > TapWindow)
            tapCount = 0;
        lastTapTime = Time.unscaledTime;
        tapCount++;

        if (tapCount >= TriggerCount)
        {
            tapCount = 0;
            if (gameManager != null) gameManager.SkipToAllClearedAndShowReward();
        }
    }
}
