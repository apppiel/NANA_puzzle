using UnityEngine;

// 역할: 사운드/진동 설정 값을 관리하고 PlayerPrefs에 저장
// 씬 어디서든 SettingsManager.Instance로 접근 가능
public class SettingsManager : MonoBehaviour
{
  public static SettingsManager Instance { get; private set; }

  const string SoundKey = "soundOn";
  const string VibrationKey = "vibrationOn";

  public bool SoundOn { get; private set; }
  public bool VibrationOn { get; private set; }

  void Awake()
  {
    // 씬에 하나만 존재하도록 보장. 중복 생성되면 새 것을 파괴
    if (Instance != null) { Destroy(gameObject); return; }
    Instance = this;

    // 저장된 값 불러오기 (없으면 기본값 on)
    SoundOn = PlayerPrefs.GetInt(SoundKey, 1) == 1;
    VibrationOn = PlayerPrefs.GetInt(VibrationKey, 1) == 1;
    ApplySound();
  }

  // 사운드 on/off 전환. AudioListener.volume으로 게임 내 모든 소리를 한 번에 제어
  public void SetSound(bool on)
  {
    SoundOn = on;
    PlayerPrefs.SetInt(SoundKey, on ? 1 : 0);
    PlayerPrefs.Save();
    ApplySound();
  }

  public void SetVibration(bool on)
  {
    VibrationOn = on;
    PlayerPrefs.SetInt(VibrationKey, on ? 1 : 0);
    PlayerPrefs.Save();
  }

  // BoardRenderer 등 다른 스크립트에서 이 함수만 호출하면 설정에 따라 진동 여부가 결정됨
  public void Vibrate()
  {
    // 에디터에서는 Handheld.Vibrate()가 iPhoneUtils 경고 로그를 띄우므로 실기기에서만 호출
#if !UNITY_EDITOR
    if (VibrationOn) Handheld.Vibrate();
#endif
  }

  void ApplySound()
  {
    AudioListener.volume = SoundOn ? 1f : 0f;
  }
}
