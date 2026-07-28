using UnityEngine;
using UnityEngine.UI;

// 안드로이드 뒤로가기 버튼 처리. KeyCode.Escape로 매핑됨(iOS는 안 눌리므로 자연스레 no-op).
// 우선순위:
//  1. 자기 팝업 떠 있음 → 팝업 닫기(돌아가기)
//  2. 강제 업데이트 팝업(UpdatePromptCanvas) 떠 있음 → 무시(강제 종료 유도라 우회 차단)
//  3. 설정창 열림 → 설정창 닫기
//  4. 그 외(하트 잠금 포함) → 종료 확인 팝업 표시
// [돌아가기] = 팝업 닫기 / [종료하기] = Application.Quit.
public class BackButtonHandler : MonoBehaviour
{
  public SettingsPanel settingsPanel;   // 인스펙터에서 SettingsUI 오브젝트 연결. 없어도 동작(설정창 감지만 스킵)

  Canvas overlayCanvas;
  Sprite roundedSprite;   // 카드·버튼 배경용. ShowPopup 첫 호출 시 procedural 생성

  void Update()
  {
    if (!Input.GetKeyDown(KeyCode.Escape)) return;

    // 1. 자기 팝업 이미 떠 있으면 닫기(뒤로가기 = 돌아가기)
    if (overlayCanvas != null) { HidePopup(); return; }

    // 2. 강제 업데이트 팝업이 뜨면 무시
    if (GameObject.Find("UpdatePromptCanvas") != null) return;

    // 3. 설정창 열려 있으면 설정창 닫기
    if (settingsPanel != null && settingsPanel.IsOpen) { settingsPanel.Close(); return; }

    // 4. 그 외(하트 잠금 상황 포함) → 종료 팝업
    ShowPopup();
  }

  void ShowPopup()
  {
    if (roundedSprite == null) roundedSprite = BuildRoundedSprite(64, 16);

    var canvasGo = new GameObject("ExitConfirmCanvas");
    canvasGo.transform.SetParent(transform);
    overlayCanvas = canvasGo.AddComponent<Canvas>();
    overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
    // LivesLockCanvas(29000)보다 위에 둬서 잠금 상태에서도 종료 팝업이 보이게. 강제 업데이트(30000)보단 낮게.
    overlayCanvas.sortingOrder = 29500;
    canvasGo.AddComponent<CanvasScaler>();
    canvasGo.AddComponent<GraphicRaycaster>();

    var bgGo = new GameObject("Background");
    bgGo.transform.SetParent(canvasGo.transform, false);
    var bg = bgGo.AddComponent<Image>();
    bg.color = new Color(0, 0, 0, 0.7f);
    Stretch(bg.rectTransform);

    var cardGo = new GameObject("Card");
    cardGo.transform.SetParent(canvasGo.transform, false);
    var card = cardGo.AddComponent<Image>();
    card.color = Color.white;
    card.sprite = roundedSprite;
    card.type = Image.Type.Sliced;
    var cardRt = card.rectTransform;
    cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
    cardRt.anchoredPosition = Vector2.zero;
    cardRt.sizeDelta = new Vector2(720, 360);

    AddText(cardGo, "Title", "앱을 종료하시겠어요?",
      34, Color.black, TextAnchor.MiddleCenter,
      new Vector2(0, 0.45f), new Vector2(1, 0.85f),
      new Vector2(0.5f, 0.5f),
      new Vector2(20, 0), new Vector2(-20, 0));

    AddButton(cardGo, "Cancel", "돌아가기",
      new Vector2(0.05f, 0.08f), new Vector2(0.48f, 0.32f),
      new Color(0.85f, 0.85f, 0.85f), new Color(0.2f, 0.2f, 0.2f),
      HidePopup,
      roundedSprite);

    AddButton(cardGo, "Quit", "종료하기",
      new Vector2(0.52f, 0.08f), new Vector2(0.95f, 0.32f),
      new Color(1f, 0.54f, 0.54f), Color.white,
      Application.Quit,
      roundedSprite);
  }

  // LivesSystem과 동일 방식. 9-slice 라운드 사각형 스프라이트를 코드로 생성해 카드·버튼 배경에 재사용.
  static Sprite BuildRoundedSprite(int size, int radius)
  {
    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    tex.filterMode = FilterMode.Bilinear;

    var pixels = new Color32[size * size];
    var transparent = new Color32(255, 255, 255, 0);
    var opaque = new Color32(255, 255, 255, 255);
    int r2 = radius * radius;

    for (int y = 0; y < size; y++)
    {
      for (int x = 0; x < size; x++)
      {
        int dx = 0, dy = 0;
        if (x < radius) dx = radius - x;
        else if (x >= size - radius) dx = x - (size - radius - 1);
        if (y < radius) dy = radius - y;
        else if (y >= size - radius) dy = y - (size - radius - 1);

        pixels[y * size + x] = (dx * dx + dy * dy) <= r2 ? opaque : transparent;
      }
    }
    tex.SetPixels32(pixels);
    tex.Apply();

    return Sprite.Create(
      tex,
      new Rect(0, 0, size, size),
      new Vector2(0.5f, 0.5f),
      100f,
      0,
      SpriteMeshType.FullRect,
      new Vector4(radius, radius, radius, radius));
  }

  void HidePopup()
  {
    if (overlayCanvas == null) return;
    Destroy(overlayCanvas.gameObject);
    overlayCanvas = null;
  }

  // ── UI 헬퍼 (UpdateChecker와 동일 스타일) ────────────────

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
    UnityEngine.Events.UnityAction onClick,
    Sprite bgSprite = null)
  {
    var btnGo = new GameObject(name);
    btnGo.transform.SetParent(parent.transform, false);
    var img = btnGo.AddComponent<Image>();
    img.color = bgColor;
    if (bgSprite != null)
    {
      img.sprite = bgSprite;
      img.type = Image.Type.Sliced;   // 9-slice로 라운드 코너 유지
    }
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
}
