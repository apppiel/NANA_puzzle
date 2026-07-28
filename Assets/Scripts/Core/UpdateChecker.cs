using UnityEngine;
using UnityEngine.UI;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;

// 앱 시작 시 Firestore config/app_version 문서를 조회해 최신 버전을 확인.
// 현재 앱 버전(Player Settings의 Version)보다 최신이면 팝업 표시.
// 팝업: [닫기]는 그냥 사라짐, [업데이트]는 스토어 열기.
public class UpdateChecker : MonoBehaviour
{
  const string PackageName = "com.nanaBox.NANApuzzle";
  const string IosAppId    = "6788628965";
  const string ConfigPath  = "config/app_version";  // Firestore 문서 경로

  Canvas overlayCanvas;

  void Start()
  {
#if UNITY_EDITOR
    // 에디터에서는 개발 편의를 위해 업데이트 체크 스킵
    return;
#else
    FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
    {
      if (task.Result != DependencyStatus.Available)
      {
        Debug.LogWarning("[UpdateChecker] Firebase 초기화 실패: " + task.Result);
        return;
      }
      CheckLatestVersion();
    });
#endif
  }

  void CheckLatestVersion()
  {
    var db = FirebaseFirestore.DefaultInstance;
    db.Document(ConfigPath).GetSnapshotAsync().ContinueWithOnMainThread(task =>
    {
      if (!task.IsCompletedSuccessfully || !task.Result.Exists) return;

#if UNITY_ANDROID
      const string field = "androidLatestVersion";
#elif UNITY_IOS
      const string field = "iosLatestVersion";
#else
      return;
#endif

      var snap = task.Result;
      if (!snap.ContainsField(field)) return;

      string latest = snap.GetValue<string>(field);
      if (string.IsNullOrEmpty(latest)) return;

      if (IsNewer(latest, Application.version))
        ShowPopup(latest);
    });
  }

  // "1.0.2" > "1.0.1" 처럼 비교. 파싱 실패 시 팝업 안 띄움
  bool IsNewer(string latest, string current)
  {
    try   { return new System.Version(latest) > new System.Version(current); }
    catch { return false; }
  }

  void ShowPopup(string latestVersion)
  {
    var canvasGo = new GameObject("UpdatePromptCanvas");
    canvasGo.transform.SetParent(transform);
    overlayCanvas = canvasGo.AddComponent<Canvas>();
    overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
    overlayCanvas.sortingOrder = 30000;
    canvasGo.AddComponent<CanvasScaler>();
    canvasGo.AddComponent<GraphicRaycaster>();

    // 반투명 검은 배경 (뒤 UI 터치 차단)
    var bgGo = new GameObject("Background");
    bgGo.transform.SetParent(canvasGo.transform, false);
    var bg = bgGo.AddComponent<Image>();
    bg.color = new Color(0, 0, 0, 0.7f);
    Stretch(bg.rectTransform);

    // 중앙 흰 카드
    var cardGo = new GameObject("Card");
    cardGo.transform.SetParent(canvasGo.transform, false);
    var card = cardGo.AddComponent<Image>();
    card.color = Color.white;
    var cardRt = card.rectTransform;
    cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
    cardRt.anchoredPosition = Vector2.zero;
    cardRt.sizeDelta = new Vector2(720, 400);

    AddText(cardGo, "Title", "업데이트 알림",
      36, Color.black, TextAnchor.MiddleCenter,
      new Vector2(0, 1), new Vector2(1, 1),
      new Vector2(0.5f, 1),
      new Vector2(20, -90), new Vector2(-20, -20));

    AddText(cardGo, "Message",
      "새 버전이 있습니다 (v" + latestVersion + ").\n최신 버전으로 업데이트해주세요.",
      26, new Color(0.2f, 0.2f, 0.2f), TextAnchor.MiddleCenter,
      new Vector2(0, 0.35f), new Vector2(1, 0.75f),
      new Vector2(0.5f, 0.5f),
      new Vector2(30, 0), new Vector2(-30, 0));

    // 강제 업데이트라 [종료]는 앱 프로세스 종료. 다음 실행 때 Start()가 다시 돌면서 팝업도 재표시됨
    AddButton(cardGo, "Close", "종료",
      new Vector2(0.05f, 0.06f), new Vector2(0.48f, 0.28f),
      new Color(0.85f, 0.85f, 0.85f), new Color(0.2f, 0.2f, 0.2f),
      Application.Quit);

    AddButton(cardGo, "Update", "업데이트",
      new Vector2(0.52f, 0.06f), new Vector2(0.95f, 0.28f),
      new Color(1f, 0.54f, 0.54f), Color.white,
      OpenStore);
  }

  static void Stretch(RectTransform rt)
  {
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.offsetMin = rt.offsetMax = Vector2.zero;
  }

  static void AddText(GameObject parent, string name, string content,
    int fontSize, Color color, TextAnchor align,
    Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
    Vector2 offsetMin, Vector2 offsetMax)
  {
    var go = new GameObject(name);
    go.transform.SetParent(parent.transform, false);
    var text = go.AddComponent<Text>();
    text.text = content;
    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    text.fontSize = fontSize;
    text.color = color;
    text.alignment = align;
    var rt = text.rectTransform;
    rt.anchorMin = anchorMin;
    rt.anchorMax = anchorMax;
    rt.pivot = pivot;
    rt.offsetMin = offsetMin;
    rt.offsetMax = offsetMax;
  }

  static void AddButton(GameObject parent, string name, string label,
    Vector2 anchorMin, Vector2 anchorMax,
    Color bgColor, Color textColor,
    UnityEngine.Events.UnityAction onClick)
  {
    var btnGo = new GameObject(name);
    btnGo.transform.SetParent(parent.transform, false);
    var img = btnGo.AddComponent<Image>();
    img.color = bgColor;
    var rt = img.rectTransform;
    rt.anchorMin = anchorMin;
    rt.anchorMax = anchorMax;
    rt.offsetMin = rt.offsetMax = Vector2.zero;

    var btn = btnGo.AddComponent<Button>();
    btn.onClick.AddListener(onClick);

    AddText(btnGo, "Text", label,
      28, textColor, TextAnchor.MiddleCenter,
      Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
      Vector2.zero, Vector2.zero);
  }

  void OpenStore()
  {
#if UNITY_ANDROID
    Application.OpenURL("market://details?id=" + PackageName);
#elif UNITY_IOS
    Application.OpenURL("https://apps.apple.com/app/id" + IosAppId);
#endif
  }
}
