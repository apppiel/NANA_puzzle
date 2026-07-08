using UnityEngine;
using GoogleMobileAds.Api;
#if UNITY_IOS
using System.Runtime.InteropServices;
using AOT;
#endif

public class AdManager : MonoBehaviour
{
#if UNITY_IOS
  // iOS 네이티브 ATT 권한 요청 함수 (ATTBridge.mm)
  delegate void ATTCallback(int status);

  [DllImport("__Internal")]
  static extern void _RequestATTPermission(ATTCallback callback);

  static AdManager instance;

  [MonoPInvokeCallback(typeof(ATTCallback))]
  static void OnATTComplete(int status)
  {
    MobileAds.Initialize(_ => instance.LoadAd());
  }
#endif
  // 실제 광고 ID. 테스트 중에는 아래 testAdUnitId를 사용하고, 출시 전에 실제 ID로 교체할 것
#if UNITY_IOS
  const string realAdUnitId = "ca-app-pub-3079888946602647/7627888748";
  const string testAdUnitId = "ca-app-pub-3940256099942544/4411468910"; // iOS 구글 공식 테스트 ID
#else
  const string realAdUnitId = "ca-app-pub-3079888946602647/7294635262";
  const string testAdUnitId = "ca-app-pub-3940256099942544/1033173712"; // Android 구글 공식 테스트 ID
#endif

  // 에디터/테스트 빌드에서는 테스트 광고를 사용. 출시 빌드로 바꿀 때 false로 변경
  [SerializeField] bool useTestAd = true;

  // 몇 레벨마다 광고를 보여줄지 (3 = 3레벨 클리어마다 1번)
  [SerializeField] int showEveryNLevels = 3;

  // 몇 번 막혀서 리셋될 때마다 광고를 보여줄지 (4 = 4번 막힐 때마다 1번)
  [SerializeField] int showAdEveryNStucks = 4;

  InterstitialAd interstitialAd;
  int levelClearCount = 0; // 현재 세션에서 클리어한 레벨 수
  int stuckResetCount = 0; // 현재 세션에서 막혀서 리셋된 횟수

  void Start()
  {
#if UNITY_IOS
    instance = this;
    // iOS: Apple 정책상 ATT 권한 팝업을 먼저 띄운 뒤 AdMob 초기화
    _RequestATTPermission(OnATTComplete);
#else
    // MobileAds.Initialize는 앱 전체에서 딱 한 번만 호출하면 됨
    MobileAds.Initialize(_ => LoadAd());
#endif
  }

  void LoadAd()
  {
    // 기존 광고 오브젝트가 있으면 메모리 해제 후 새로 로드
    interstitialAd?.Destroy();

    string adUnitId = useTestAd ? testAdUnitId : realAdUnitId;
    var request = new AdRequest();

    InterstitialAd.Load(adUnitId, request, (ad, error) =>
    {
      if (error != null)
      {
        Debug.LogWarning("전면 광고 로드 실패: " + error.GetMessage());
        return;
      }
      interstitialAd = ad;
      Debug.Log("전면 광고 로드 완료");
    });
  }

  // GameManager의 NextLevel() 직전에 호출. 광고를 보여줄 타이밍인지 판단
  public void OnLevelCleared()
  {
    levelClearCount++;

    // N레벨마다 광고 표시
    if (levelClearCount % showEveryNLevels != 0) return;

    if (interstitialAd != null && interstitialAd.CanShowAd())
    {
      // 광고가 닫히면 다음 광고를 미리 로드
      interstitialAd.OnAdFullScreenContentClosed += () => LoadAd();
      interstitialAd.Show();
    }
    else
    {
      // 광고가 준비 안 됐으면 미리 로드만 해둠
      LoadAd();
    }
  }

  // BoardRenderer가 막혀서 리셋될 때마다 GameManager를 통해 호출
  public void OnStuckReset()
  {
    stuckResetCount++;

    // N번 막힐 때마다 광고 표시
    if (stuckResetCount % showAdEveryNStucks != 0) return;

    if (interstitialAd != null && interstitialAd.CanShowAd())
    {
      interstitialAd.OnAdFullScreenContentClosed += () => LoadAd();
      interstitialAd.Show();
    }
    else
    {
      LoadAd();
    }
  }

  void OnDestroy()
  {
    interstitialAd?.Destroy();
  }
}
