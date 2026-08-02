using System;
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
    MobileAds.Initialize(_ => instance.LoadAllAds());
  }
#endif
  // 실제 광고 ID. 테스트 중에는 아래 testAdUnitId를 사용하고, 출시 전에 실제 ID로 교체할 것
#if UNITY_IOS
  const string realAdUnitId = "ca-app-pub-3079888946602647/7627888748";
  const string testAdUnitId = "ca-app-pub-3940256099942544/4411468910"; // iOS 구글 공식 테스트 ID
  const string realRewardedAdUnitId = "ca-app-pub-3079888946602647/7791705546";
  const string testRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313"; // iOS Rewarded 구글 공식 테스트 ID
#else
  const string realAdUnitId         = "ca-app-pub-3079888946602647/7294635262";
  const string testAdUnitId         = "ca-app-pub-3940256099942544/1033173712"; // Android 구글 공식 테스트 ID
  const string realRewardedAdUnitId = "ca-app-pub-3079888946602647/1949186070";
  const string testRewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917"; // Android Rewarded 구글 공식 테스트 ID
#endif

  // 에디터/테스트 빌드에서는 테스트 광고를 사용. 출시 빌드로 바꿀 때 false로 변경
  [SerializeField] bool useTestAd = true;

  // 몇 레벨마다 광고를 보여줄지 (3 = 3레벨 클리어마다 1번)
  [SerializeField] int showEveryNLevels = 3;

  InterstitialAd interstitialAd;
  RewardedAd rewardedAd;
  // 광고 표시 임계치 카운트. PlayerPrefs로 세션 간 유지 — 습관적으로 앱 껐다 켜는 유저의 광고 스킵 방지.
  int levelClearCount = 0;
  const string LevelClearCountKey = "adLevelClearCount";

  // 보상형 광고 대기열: 준비 안 됐을 때 유저가 요청한 콜백을 저장해두고, 로드 완료 순간 자동 표시.
  // 유저가 두 번 탭할 필요 없게 만드는 핵심 상태.
  Action pendingRewardCallback;
  const float PendingTimeoutSec = 15f;   // 이 시간 안에 로드 못 하면 대기 취소하고 실패 안내

  // 상태 메시지 상수. LivesSystem이 이 상수와 비교해 로딩 vs 실패 vs idle을 판별.
  public const string RewardedLoadingMessage = "하트 불러오는 중..";
  public const string RewardedFailedMessage = "하트가 길을 잃었어요. 잠시 후 다시 불러주세요.";

  // 대기 상태 UI 갱신용. LivesSystem이 구독해 잠금 오버레이 statusText + 버튼 활성화 상태에 반영.
  // 빈 문자열이면 idle 상태로 복귀.
  public event Action<string> OnRewardedStatus;

  void Start()
  {
    levelClearCount = PlayerPrefs.GetInt(LevelClearCountKey, 0);

#if UNITY_IOS && !UNITY_EDITOR
    instance = this;
    // iOS: Apple 정책상 ATT 권한 팝업을 먼저 띄운 뒤 AdMob 초기화
    _RequestATTPermission(OnATTComplete);
#else
    // MobileAds.Initialize는 앱 전체에서 딱 한 번만 호출하면 됨
    // (에디터에서는 iOS 빌드 타깃이어도 이 브랜치 — 네이티브 ATT 함수 없음)
    MobileAds.Initialize(_ => LoadAllAds());
#endif
  }

  // 전면·보상형 광고를 함께 미리 로드
  void LoadAllAds()
  {
    LoadAd();
    LoadRewardedAd();
  }

  void LoadAd()
  {
    // 예약된 재시도가 있으면 취소해 중복 로드 방지
    CancelInvoke(nameof(LoadAd));
    // 기존 광고 오브젝트가 있으면 메모리 해제 후 새로 로드
    interstitialAd?.Destroy();

    string adUnitId = useTestAd ? testAdUnitId : realAdUnitId;
    var request = new AdRequest();

    InterstitialAd.Load(adUnitId, request, (ad, error) =>
    {
      if (error != null)
      {
        Debug.LogWarning("전면 광고 로드 실패: " + error.GetMessage());
        // 네트워크 일시 장애 등으로 로드 실패 시 30초 뒤 자동 재시도
        Invoke(nameof(LoadAd), 30f);
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
    PlayerPrefs.SetInt(LevelClearCountKey, levelClearCount);

    // 아직 임계치에 못 미치면 대기
    if (levelClearCount < showEveryNLevels) return;

    // 임계치를 넘겼는데 광고가 준비 안 됐으면 카운트는 유지한 채 로드만 시도
    // → 다음 클리어에서 광고 준비되면 즉시 표시됨 (기회 놓치지 않음)
    if (interstitialAd != null && interstitialAd.CanShowAd())
    {
      levelClearCount = 0;
      PlayerPrefs.SetInt(LevelClearCountKey, 0);
      // 광고가 닫히면 다음 광고를 미리 로드
      interstitialAd.OnAdFullScreenContentClosed += () => LoadAd();
      interstitialAd.Show();
    }
    else
    {
      LoadAd();
    }
  }

  // ── 보상형 광고 (LivesSystem에서 목숨 리필 트리거) ─────────────────────

  void LoadRewardedAd()
  {
    CancelInvoke(nameof(LoadRewardedAd));
    rewardedAd?.Destroy();

    string adUnitId = useTestAd ? testRewardedAdUnitId : realRewardedAdUnitId;
    if (string.IsNullOrEmpty(adUnitId))
    {
      Debug.LogWarning("보상형 광고 unit ID가 비어있음 (실제 ID 아직 미입력)");
      return;
    }

    var request = new AdRequest();
    RewardedAd.Load(adUnitId, request, (ad, error) =>
    {
      if (error != null || ad == null)
      {
        Debug.LogWarning("보상형 광고 로드 실패: " + (error?.GetMessage() ?? "ad null"));
        Invoke(nameof(LoadRewardedAd), 30f);  // 전면광고와 동일 재시도 패턴
        // 유저가 대기 중이었으면 실패 안내 (다음 재시도는 30초 뒤라 그때까지 기다리게 두지 않음)
        NotifyPendingFailed();
        return;
      }
      rewardedAd = ad;
      Debug.Log("보상형 광고 로드 완료");
      // 유저가 이미 탭해 대기 중이면 자동 표시
      TryShowPending();
    });
  }

  // 광고 준비돼 있으면 즉시 표시. 준비 안 됐으면 대기열에 넣고 로드 완료 순간 자동 표시.
  // 반환값은 즉시 표시 여부(true=바로 뜸, false=대기 중). 대기 상태는 OnRewardedStatus 이벤트로 UI 갱신.
  public bool ShowRewardedAd(Action onReward)
  {
    // 준비돼 있으면 즉시 표시 — 대기 중이던 요청이 있어도 이걸 우선 처리
    if (rewardedAd != null && rewardedAd.CanShowAd())
    {
      ClearPending();
      // 광고 닫힌 뒤 다음 광고 미리 로드
      rewardedAd.OnAdFullScreenContentClosed += () => LoadRewardedAd();
      // Show 콜백은 유저가 광고 완주(보상 획득)했을 때만 호출. 중도 종료 시 호출 안 됨.
      rewardedAd.Show(_ => onReward?.Invoke());
      return true;
    }

    // 준비 안 됨: 대기열에 넣고 로드 시도. 로드 완료 콜백이 자동으로 Show 처리.
    pendingRewardCallback = onReward;
    CancelInvoke(nameof(PendingTimeout));
    Invoke(nameof(PendingTimeout), PendingTimeoutSec);
    OnRewardedStatus?.Invoke(RewardedLoadingMessage);
    LoadRewardedAd();
    return false;
  }

  // 로드 완료 시점에 대기 중이던 요청이 있으면 자동으로 광고 표시.
  void TryShowPending()
  {
    if (pendingRewardCallback == null) return;
    if (rewardedAd == null || !rewardedAd.CanShowAd()) return;

    var reward = pendingRewardCallback;
    ClearPending();
    rewardedAd.OnAdFullScreenContentClosed += () => LoadRewardedAd();
    rewardedAd.Show(_ => reward?.Invoke());
  }

  // 로드 실패 또는 15초 타임아웃 시 호출. 대기 취소하고 유저에게 실패 안내.
  void NotifyPendingFailed()
  {
    if (pendingRewardCallback == null) return;
    ClearPending();
    OnRewardedStatus?.Invoke(RewardedFailedMessage);
  }

  // Invoke 대상 (nameof 참조 유지 위해 private void 시그니처 필요).
  void PendingTimeout() => NotifyPendingFailed();

  void ClearPending()
  {
    pendingRewardCallback = null;
    CancelInvoke(nameof(PendingTimeout));
    OnRewardedStatus?.Invoke("");
  }

  void OnDestroy()
  {
    interstitialAd?.Destroy();
    rewardedAd?.Destroy();
  }
}
