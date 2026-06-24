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

    // 체크 = 음소거, 해제 = 소리 켜짐 → SoundOn과 반대로 초기화
    soundToggle.isOn = !SettingsManager.Instance.SoundOn;
    vibrationToggle.isOn = !SettingsManager.Instance.VibrationOn;

    // 토글 값을 반전해서 전달 (체크됨 = 꺼짐)
    soundToggle.onValueChanged.AddListener(muted => SettingsManager.Instance.SetSound(!muted));
    vibrationToggle.onValueChanged.AddListener(muted => SettingsManager.Instance.SetVibration(!muted));
  }

  // ⚙️ 버튼의 OnClick에 연결
  public void Open() => panel.SetActive(true);

  // 닫기 버튼의 OnClick에 연결
  public void Close() => panel.SetActive(false);
}
