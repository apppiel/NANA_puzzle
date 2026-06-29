using UnityEngine;
using UnityEngine.UIElements;

// 역할: UI Toolkit 기반 설정 팝업 패널의 열고 닫기, 토글 상태 동기화
// UIDocument 컴포넌트가 같은 오브젝트에 있어야 동작함
public class SettingsPanel : MonoBehaviour
{
  public GameManager gameManager;  // 진행 초기화용. 인스펙터에서 GameManager 오브젝트 연결

  VisualElement overlay;
  Toggle soundToggle;
  Toggle vibrationToggle;

  void Start()
  {
    // UIDocument → rootVisualElement는 HTML의 <body>와 같음
    var root = GetComponent<UIDocument>().rootVisualElement;

    // Q<T>("이름") = document.getElementById("이름")과 같음
    overlay = root.Q<VisualElement>("overlay");
    soundToggle = root.Q<Toggle>("sound-toggle");
    vibrationToggle = root.Q<Toggle>("vibration-toggle");
    var closeButton = root.Q<Button>("close-button");

    // 저장된 설정 값으로 토글 초기화 (체크 = 켜짐)
    soundToggle.value = SettingsManager.Instance.SoundOn;
    vibrationToggle.value = SettingsManager.Instance.VibrationOn;

    // 이벤트 연결: RegisterValueChangedCallback = addEventListener("change", ...)과 같음
    soundToggle.RegisterValueChangedCallback(evt => SettingsManager.Instance.SetSound(evt.newValue));
    vibrationToggle.RegisterValueChangedCallback(evt => SettingsManager.Instance.SetVibration(evt.newValue));
    closeButton.clicked += Close;
    root.Q<Button>("reset-button").clicked += ResetProgress;

    overlay.style.display = DisplayStyle.None;  // 시작 시 닫혀 있음
  }

  // ⚙️ 버튼의 OnClick에 연결 (기존과 동일하게 public 유지)
  public void Open()
  {
    // 열 때마다 현재 설정 값으로 동기화
    soundToggle.value = SettingsManager.Instance.SoundOn;
    vibrationToggle.value = SettingsManager.Instance.VibrationOn;
    overlay.style.display = DisplayStyle.Flex;
  }

  // 닫기 버튼의 OnClick에 연결 (기존과 동일하게 public 유지)
  public void Close() => overlay.style.display = DisplayStyle.None;

  void ResetProgress()
  {
    Close();
    if (gameManager != null) gameManager.ResetAndRestart();
  }
}
