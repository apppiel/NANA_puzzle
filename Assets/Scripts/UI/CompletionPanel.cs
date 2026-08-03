using UnityEngine;
using UnityEngine.UIElements;

// 100판 완주자 전용 확정 화면.
// 표시 조건: clearedCount >= 100 && 로컬 코드 있음
// 표시 시점: (1) 리워드 팝업 [닫기] 시 완주 상태면 이어서, (2) 앱 시작 시 완주 상태면 즉시
// [1레벨로 돌아가기] 누르면 진행도 리셋 후 이 화면 숨김.
public class CompletionPanel : MonoBehaviour
{
    public GameManager gameManager;  // [1레벨로 돌아가기] 눌렀을 때 리셋용. 인스펙터에서 연결

    VisualElement root;
    VisualElement overlay;
    Button restartButton;

    void Start()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        // 초기엔 터치 안 잡음. Show() 시점에 Position으로 전환해 오버레이 뒤 UI 차단.
        root.pickingMode = PickingMode.Ignore;

        overlay = root.Q<VisualElement>("overlay");
        restartButton = root.Q<Button>("restart-button");

        restartButton.clicked += OnRestartFromLevel1;
    }

    public bool IsOpen => overlay != null && overlay.style.display == DisplayStyle.Flex;

    public void Show()
    {
        if (overlay == null) return;
        root.pickingMode = PickingMode.Position;
        overlay.style.display = DisplayStyle.Flex;
    }

    public void Hide()
    {
        if (overlay == null) return;
        overlay.style.display = DisplayStyle.None;
        root.pickingMode = PickingMode.Ignore;
    }

    void OnRestartFromLevel1()
    {
        Hide();
        if (gameManager != null) gameManager.GoToLevel1();
    }
}
