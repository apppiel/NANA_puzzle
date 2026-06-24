using UnityEngine;
using UnityEngine.UI;

// 역할: 설정 팝업 패널의 열고 닫기, 토글 상태 동기화
// 인스펙터에서 panel, soundToggle, vibrationToggle을 연결해야 동작함
public class SettingsPanel : MonoBehaviour
{
  public GameObject panel;          // 설정 팝업 패널 오브젝트. 인스펙터에서 Panel 오브젝트 연결
  public Toggle soundToggle;        // 사운드 on/off 토글. 인스펙터에서 Toggle 연결
  public Toggle vibrationToggle;    // 진동 on/off 토글. 인스펙터에서 Toggle 연결

  void Start()
  {
    panel.SetActive(false);  // 게임 시작 시 설정창 닫혀 있게

    // 저장된 설정값으로 토글 초기 상태 맞추기
    soundToggle.isOn = SettingsManager.Instance.SoundOn;
    vibrationToggle.isOn = SettingsManager.Instance.VibrationOn;

    // 토글 변경 시 SettingsManager에 자동으로 전달
    soundToggle.onValueChanged.AddListener(SettingsManager.Instance.SetSound);
    vibrationToggle.onValueChanged.AddListener(SettingsManager.Instance.SetVibration);
  }

  // ⚙️ 버튼의 OnClick에 연결
  public void Open() => panel.SetActive(true);

  // 닫기 버튼의 OnClick에 연결
  public void Close() => panel.SetActive(false);
}
