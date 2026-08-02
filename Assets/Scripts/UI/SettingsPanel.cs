using UnityEngine;
using UnityEngine.UIElements;

// 역할: UI Toolkit 기반 설정 팝업 패널의 열고 닫기, 토글 상태 동기화
// UIDocument 컴포넌트가 같은 오브젝트에 있어야 동작함
public class SettingsPanel : MonoBehaviour
{
  public GameManager gameManager;      // 진행 초기화용. 인스펙터에서 GameManager 오브젝트 연결
  public RewardManager rewardManager;  // 100판 완주자의 인증코드 재열람용. 인스펙터에서 RewardUI 연결

  VisualElement root;
  VisualElement overlay;
  Toggle soundToggle;
  Toggle vibrationToggle;
  Toggle notificationToggle;
  Button rewardButton;

  void Start()
  {
    // UIDocument → rootVisualElement는 HTML의 <body>와 같음
    root = GetComponent<UIDocument>().rootVisualElement;

    // Q<T>("이름") = document.getElementById("이름")과 같음
    overlay = root.Q<VisualElement>("overlay");
    soundToggle = root.Q<Toggle>("sound-toggle");
    vibrationToggle = root.Q<Toggle>("vibration-toggle");
    notificationToggle = root.Q<Toggle>("notification-toggle");
    rewardButton = root.Q<Button>("reward-button");
    var closeButton = root.Q<Button>("close-button");

    // 저장된 설정 값으로 토글 초기화 (체크 = 켜짐)
    soundToggle.value = SettingsManager.Instance.SoundOn;
    vibrationToggle.value = SettingsManager.Instance.VibrationOn;
    notificationToggle.value = SettingsManager.Instance.NotificationOn;

    // 이벤트 연결: RegisterValueChangedCallback = addEventListener("change", ...)과 같음
    soundToggle.RegisterValueChangedCallback(evt => SettingsManager.Instance.SetSound(evt.newValue));
    vibrationToggle.RegisterValueChangedCallback(evt => SettingsManager.Instance.SetVibration(evt.newValue));
    notificationToggle.RegisterValueChangedCallback(evt => SettingsManager.Instance.SetNotification(evt.newValue));
    closeButton.clicked += Close;
    root.Q<Button>("reset-button").clicked += ResetProgress;
    rewardButton.clicked += OnRewardButtonClicked;

    overlay.style.display = DisplayStyle.None;  // 시작 시 닫혀 있음
    // 닫힌 상태에서 rootVisualElement가 풀스크린 터치를 가로채지 않도록 Ignore 처리
    // (iOS에서 overlay가 display:none이어도 root가 Canvas 버튼 터치를 막는 문제 방지)
    root.pickingMode = PickingMode.Ignore;
  }

  // BackButtonHandler에서 뒤로가기 시 열림 상태 확인용
  public bool IsOpen => overlay != null && overlay.resolvedStyle.display == DisplayStyle.Flex;

  // ⚙️ 버튼의 OnClick에 연결 (기존과 동일하게 public 유지)
  public void Open()
  {
    // 열 때마다 현재 설정 값으로 동기화
    soundToggle.value = SettingsManager.Instance.SoundOn;
    vibrationToggle.value = SettingsManager.Instance.VibrationOn;
    notificationToggle.value = SettingsManager.Instance.NotificationOn;

    // 코드 발급 이력이 있는 유저(로컬 코드 존재)에게만 재열람 버튼 노출.
    // IsAllCleared 대신 로컬 코드 유무로 판단하는 이유: 팝업의 [1레벨로 돌아가기]가 진행도를
    // 리셋해도 예전에 받은 코드는 계속 볼 수 있어야 함. gameManager.IsAllCleared는 현재 진행도만
    // 반영해서 리셋 후엔 false가 되므로 부적합.
    bool showReward = rewardManager != null && !string.IsNullOrEmpty(rewardManager.GetLocalCode());
    rewardButton.style.display = showReward ? DisplayStyle.Flex : DisplayStyle.None;

    root.pickingMode = PickingMode.Position;  // 패널 내부 버튼이 터치를 받을 수 있도록 복구
    overlay.style.display = DisplayStyle.Flex;
  }

  // 닫기 버튼의 OnClick에 연결 (기존과 동일하게 public 유지)
  public void Close()
  {
    overlay.style.display = DisplayStyle.None;
    root.pickingMode = PickingMode.Ignore;  // 닫으면 다시 터치 차단 해제
  }

  void ResetProgress()
  {
    Close();
    if (gameManager != null) gameManager.ResetAndRestart();
  }

  // 설정창 닫고 리워드 팝업 열기. RewardManager.ShowReward()가 로컬/서버 상태에 따라 자동 분기함.
  void OnRewardButtonClicked()
  {
    Close();
    if (rewardManager != null) rewardManager.ShowReward();
  }
}
