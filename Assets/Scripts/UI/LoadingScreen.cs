using System.Collections;
using UnityEngine;

public class LoadingScreen : MonoBehaviour
{
    public CanvasGroup canvasGroup;  // 이 패널의 투명도 조절용. 인스펙터에서 연결하거나 같은 오브젝트에 있으면 자동 연결됨

    // 현재 1.5초 동안 로딩 화면이 완전히 보임.
    // 줄이면 (예: 0.5f) 화면이 거의 바로 사라지기 시작하고,
    // 늘리면 (예: 3f) 더 오래 보임.
    public float showSeconds = 1.5f;

    // 현재 0.5초에 걸쳐 서서히 사라짐.
    // 줄이면 (예: 0.1f) 순간적으로 뚝 끊기듯 사라지고,
    // 늘리면 (예: 1.5f) 느리고 부드럽게 사라짐.
    public float fadeSeconds = 0.5f;

    void Awake()
    {
        // 인스펙터에서 canvasGroup을 연결하지 않아도 같은 오브젝트에 붙어 있으면 자동으로 찾음
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;  // 게임 시작 직후 로딩 화면이 완전히 보이는 상태로 시작
    }

    void Start()
    {
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(showSeconds);  // showSeconds 동안 화면을 그대로 유지

        float t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;  // 매 프레임마다 경과 시간 누적

            // t=0 → alpha=1(완전 불투명), t=fadeSeconds → alpha=0(완전 투명)
            // 이 공식이 선형 페이드아웃을 만들어 냄.
            // 곡선 효과를 원한다면 Mathf.SmoothStep(1f, 0f, t / fadeSeconds) 으로 바꿀 것
            canvasGroup.alpha = 1f - (t / fadeSeconds);

            yield return null;  // 한 프레임 대기 후 while 루프 재개
        }
        canvasGroup.alpha = 0f;    // while 루프가 끝난 뒤 알파가 정확히 0이 되도록 보정

        gameObject.SetActive(false);  // 투명해진 뒤 오브젝트 자체를 끔 (렌더링 비용 제거)
    }
}
