using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

// 스크린 캡처 방지.
// Android: FLAG_SECURE로 스크린샷/화면 녹화/화면 공유 완전 차단 (앱 화면이 검게 표시됨)
// iOS: 완전 차단은 Apple 정책상 불가. UIScreen.isCaptured 폴링해서 녹화·미러링 감지 시
//      전체 화면을 덮는 검은 오버레이 표시 (녹화본은 검게 저장됨). 스크린샷 자체는 못 막음.
public class ScreenCaptureProtection : MonoBehaviour
{
#if UNITY_IOS && !UNITY_EDITOR
  [DllImport("__Internal")]
  static extern bool _IsScreenBeingCaptured();

  Canvas overlayCanvas;
  bool overlayVisible;
#endif

  void Start()
  {
#if UNITY_ANDROID && !UNITY_EDITOR
    SetAndroidSecureFlag();
#endif

#if UNITY_IOS && !UNITY_EDITOR
    BuildOverlay();
    UpdateOverlay(_IsScreenBeingCaptured());
#endif
  }

#if UNITY_IOS && !UNITY_EDITOR
  void Update()
  {
    // isCaptured는 상태 조회라 매 프레임 호출해도 가벼움
    bool captured = _IsScreenBeingCaptured();
    if (captured != overlayVisible) UpdateOverlay(captured);
  }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
  void SetAndroidSecureFlag()
  {
    try
    {
      using var player   = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
      using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
      // Window 조작은 UI 스레드에서 해야 함. runOnUiThread는 비동기라
      // 여기서 얻은 activity/player는 함수 반환 시 dispose되므로 람다 안에서 다시 획득한다.
      activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
      {
        try
        {
          using var innerPlayer   = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
          using var innerActivity = innerPlayer.GetStatic<AndroidJavaObject>("currentActivity");
          using var window        = innerActivity.Call<AndroidJavaObject>("getWindow");
          const int FLAG_SECURE = 0x2000;   // WindowManager.LayoutParams.FLAG_SECURE
          window.Call("setFlags", FLAG_SECURE, FLAG_SECURE);
          Debug.Log("[ScreenCaptureProtection] FLAG_SECURE 적용 성공");
        }
        catch (System.Exception e)
        {
          Debug.LogWarning("[ScreenCaptureProtection] FLAG_SECURE 적용 실패(inner): " + e.Message);
        }
      }));
    }
    catch (System.Exception e)
    {
      Debug.LogWarning("[ScreenCaptureProtection] Activity 접근 실패: " + e.Message);
    }
  }
#endif

#if UNITY_IOS && !UNITY_EDITOR
  void BuildOverlay()
  {
    // 최상단에 그려지는 검은 캔버스. Image의 raycastTarget으로 하위 UI 입력도 차단됨.
    var canvasGo = new GameObject("CaptureOverlayCanvas");
    canvasGo.transform.SetParent(transform);
    overlayCanvas = canvasGo.AddComponent<Canvas>();
    overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
    overlayCanvas.sortingOrder = 32767;
    canvasGo.AddComponent<CanvasScaler>();
    canvasGo.AddComponent<GraphicRaycaster>();

    var imageGo = new GameObject("BlackFill");
    imageGo.transform.SetParent(canvasGo.transform, false);
    var img = imageGo.AddComponent<Image>();
    img.color = Color.black;
    var rt = img.rectTransform;
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.offsetMin = Vector2.zero;
    rt.offsetMax = Vector2.zero;

    overlayCanvas.gameObject.SetActive(false);
  }

  void UpdateOverlay(bool show)
  {
    overlayVisible = show;
    if (overlayCanvas != null) overlayCanvas.gameObject.SetActive(show);
  }
#endif
}
