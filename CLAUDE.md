# NANA_puzzle

## 프로젝트 개요
한 줄로 모든 칸을 채우는 퍼즐 게임 (single line block fill).
고정된 시작 칸에서 출발해 상하좌우 인접 칸을 한 줄로 이어 모든 칸을 채우면 클리어.
모바일(안드로이드) 출시가 최종 목표.

## 개발 환경
- Unity 6000.0.77f1 LTS, Universal 2D 템플릿
- Active Input Handling: Both (고전 Input 사용, 터치를 마우스 클릭으로 인식)
- 타깃: Android + iOS, 세로(Portrait)
- 버전 관리: Git / GitHub
- 에디터: VS Code + Claude Code

## 폴더 구조
- Assets/Scripts/Data  — 데이터 정의 (LevelData)
- Assets/Scripts/Core  — 게임 로직 (BoardRenderer, GameManager, RewardManager)
- Assets/Scripts/UI    — UI (LoadingScreen, SettingsPanel, SettingsManager)
- Assets/Levels        — 레벨 데이터 에셋 (Level_1 등)
- Assets/Art           — 스프라이트/프리팹 (Cell 프리팹), GameTitle.png (로딩화면 이미지)
- Assets/UI            — UI Toolkit 에셋 (SettingsPanel.uxml/uss, RewardPanel.uxml/uss, PanelSettings.asset)
- Assets/Casual Game Sounds U6 — 효과음 에셋 (DM-CGS-01~50.wav)
- Assets/Firebase      — Firebase SDK (FirebaseApp, Firestore)
- Assets/ExternalDependencyManager — Firebase EDM4U
- Assets/GoogleMobileAds — Google Mobile Ads SDK (AdMob)
- Assets/Editor        — 에디터 도구 (iOSPostBuild, LevelSolverWindow)
- Assets/Plugins/iOS   — iOS 네이티브 플러그인 (ATTBridge.mm, ScreenCaptureBridge.mm)

## 핵심 파일
- LevelData.cs: ScriptableObject. 필드 = width, height, startCell, blockedCells.
- BoardRenderer.cs: Board 오브젝트에 부착. 레벨을 격자로 그리고, 마우스/터치 입력으로 칸을 채움. 효과음(fillSound, winSound) 재생. 선·닷 색상은 lineColor 필드 하나로 통합 (#ff8a8a).
- GameManager.cs: 레벨 목록·진행 상태 관리. 랜덤 셔플 로직(1~10 순차 / 11-30·31-70·**71-110** 그룹 안 미클리어 풀 랜덤 — 마지막 그룹은 40개 풀에서 30번 뽑음). 매 판 선택 시점마다 재선택(옵션 B, 앱 재실행·클리어 모두). PlayerPrefs에 clearedCount(int) + clearedMask 저장. **TotalDisplayLevels=100 상수** 도입해 asset 개수(110)와 유저 진행도(N/100)·보상 트리거를 분리. 구버전 currentLevel은 마이그레이션. 100판 클리어 시 RewardManager.ShowReward() 호출. 판 클리어는 하트 상태에 손대지 않음(값·타이머 그대로 이월). 리셋 이벤트는 livesSystem.Decrement()로 이관. **`public bool IsAllCleared` 프로퍼티(v1.0.8)**: `clearedCount >= TotalDisplayLevels`. 지금은 SettingsPanel이 리워드 버튼 조건에서 이걸 안 쓰지만(로컬 코드 유무로 변경됨) API로 유지. `editorTestLevel` 필드(#if UNITY_EDITOR): 값이 있으면 랜덤 무시하고 그 레벨만 반복 로드. `[TEST] Show Reward Popup` 컨텍스트 메뉴 + **`[TEST] Simulate All Cleared`(v1.0.8)**: cleared 배열 전부 true + clearedCount=100 강제 세팅, 실행 후 설정창 열어 리워드 버튼 노출 검증.
- RewardManager.cs: 인증코드 발급 + Firestore 저장 + 설정창 재열람 지원. **v1.0.8 완전 재작성(2026-08-03)**: 로컬 저장(PlayerPrefs `rewardCode` + `rewardCodeSynced`)이 진짜 소스, Firestore는 웹 검증용 사본. 4대 원칙: (1)어떤 실패에서도 유저는 코드를 받는다, (2)로컬이 진짜 소스, (3)서버 미동기화면 앱 실행마다 백그라운드 자동 재시도(`StartupBackgroundSync`), (4)팝업 절대 락다운 안 됨(`restartButton` 항상 활성화·모든 대기에 타임아웃). **핵심 흐름**: `ShowReward()`는 로컬 코드 있으면 즉시 표시 → synced면 완료, 미동기화면 `SyncExistingCodeCoroutine`으로 백그라운드 저장 시도. 로컬 없으면 `IssueFlowCoroutine`이 initTask 대기(12초 자체 타임아웃) → 실패 시 `FallbackIssueLocal`로 로컬 코드 생성/표시 + "스크린샷 해두세요" 안내. `IsFirebaseUsable()`이 `IsFaulted`·`Result`·`db`·`editorSimulateInitFail` 모두 명시 체크(예전 `Result` 직접 접근이 AggregateException 삼키던 버그 해결). 저장은 WriteBatch(rewards + code_index 원자적). `FirebaseApp.LogLevel=Debug` 세팅으로 adb logcat 진단 가능. **에디터 시뮬레이션 스위치**: `editorSimulateInitFail`/`editorSimulateSaveFail`(#if UNITY_EDITOR)로 fallback 흐름 검증. `[TEST] Clear Local Reward Code` 컨텍스트 메뉴로 로컬 초기화. **재작성 배경**: 두 유저(Private DNS 사용 이력)가 v1.0.4에서 100판 클리어→저장 실패("네트워크 오류-코드 메모하세요")→로컬 clearedCount=100 남음→v1.0.5(커밋 cb118ac "저장 성공 뒤에만 코드 표시")로 fallback 제거→v1.0.7 업데이트 시 앱 켤 때마다 자동 리워드 팝업+Firebase 초기화 hang으로 완전 락다운. 원인 진단은 조사 결과 Android 16KB page size·Play Services·Private DNS 세 케이스가 유력. v1.0.8 fallback으로 원인 뭐든 유저는 코드 받을 수 있게 됨.
- AdManager.cs: 전면 광고 + 보상형 광고 로드/표시. **전면 광고**: N레벨마다 표시(기본 3레벨). 카운트 임계치 방식(`count >= N`) — 광고 준비 안 됐으면 카운트 유지한 채 LoadAd()만 호출 → 다음 클리어에서 즉시 표시. 표시 성공 시에만 count=0 리셋. **보상형 광고**: `ShowRewardedAd(Action onReward)` 노출. 완주 콜백에서 onReward 실행(중도 종료 시 미실행). 로드 실패 시 30초 자동 재시도(`CancelInvoke`로 중복 방지). Rewarded unit ID는 Android/iOS 각각 별도. **기존 "4번 리셋 광고" 로직 삭제됨** — 목숨 시스템(LivesSystem)으로 완전 대체. **Pending + 자동 표시**: 유저가 탭했는데 광고 미준비면 `pendingRewardCallback`에 저장 + 15초 타임아웃 Invoke → 로드 완료 순간 자동 표시(재탭 불필요). `OnRewardedStatus` 이벤트로 상태 문자열 브로드캐스트(`RewardedLoadingMessage`/`RewardedFailedMessage` 상수). LivesSystem이 구독해 잠금 오버레이 statusText + `+ 목숨 채우기` 버튼 활성화 상태에 반영. 문구는 하트/과자 세계관 톤 ("하트 불러오는 중..", "하트가 길을 잃었어요..."). "광고" 단어 유저 UI에 노출 금지.
- SettingsPanel.cs: UI Toolkit 기반 설정 패널 제어. SettingsUI GameObject에 UIDocument와 함께 부착. SettingsManager와 분리되어 있어 UI만 담당. **"인증코드 보기" 버튼(v1.0.8)**: `reward-button`(UXML)을 로컬 코드 존재 시(=코드 발급 이력 있는 유저)에만 노출. 조건은 `rewardManager.GetLocalCode() != ""` — `gameManager.IsAllCleared` 대신 로컬 코드 유무로 판단하는 이유: RewardPanel의 [1레벨로 돌아가기]가 진행도를 리셋해서 IsAllCleared=false가 되면 예전 발급 코드를 다시 못 보게 됨. 인스펙터에 `rewardManager` 필드 필수 연결.
- Level_1.asset: 3x3, startCell (0,0). 첫 테스트 레벨.
- Assets/google-services.json: Firebase 프로젝트 설정 파일. 패키지명 com.nanaBox.NANApuzzle.
- LevelSolverWindow.cs: (Editor 툴, Tools > NANA > Level Solver 메뉴) 각 레벨의 Hamiltonian path 개수를 DFS+백트래킹으로 세서 난이도 등급 매김. 30초 타임아웃, 100+ 조기 종료, 연결성 pruning 적용. Level Generator 섹션: shape 템플릿(Rectangle/Diamond/Cross/Hexagon) + 구조적 clustered 배치 → solver 검증해서 목표 정답 범위에 맞는 후보만 뽑음. 후보 채택 시 대상 LevelData asset 덮어쓰기 가능.
- ScreenCaptureProtection.cs: 캡처 방지. Android는 UI 스레드에서 FLAG_SECURE 세팅(스크린샷·녹화·미러링 완전 차단). iOS는 UIScreen.isCaptured 폴링해 감지 시 최상단 검은 오버레이(sortingOrder=32767). 씬에 GameObject 하나 만들어 부착. Android는 runOnUiThread가 비동기라 람다 안에서 activity를 재획득해야 함(dispose 이슈).
- ScreenCaptureBridge.mm: iOS 네이티브 브릿지. `_IsScreenBeingCaptured()` 하나만 노출. UIKit 프레임워크 사용.
- UpdateChecker.cs: 강제 업데이트 유도. 앱 시작 시 **정적 JSON** (`https://nana-no2.web.app/nana-version.json`) 조회 후 `androidLatestVersion`/`iosLatestVersion`을 Application.version과 System.Version으로 비교. 낮으면 UGUI 팝업(반투명 배경 + 흰 카드). 조회 실패(오프라인/HTTP 에러/5초 타임아웃) 시 30초 후 재시도. **버튼 구성은 플랫폼 분기**: Android=[종료]/[업데이트] 좌우, iOS=[업데이트] 하나만 가운데(폭 50%). Android [종료]=`Application.Quit`으로 팝업 우회 차단(다음 실행 때 Start()가 재실행되며 팝업 재표시). iOS는 Apple HIG상 `Application.Quit` 리젝 리스크로 `#if !UNITY_IOS`로 [종료] 버튼 제외 — 우회 차단은 `OnApplicationPause` 재검사(첫 시도 실패 시 30초 폴링 + 백그라운드 복귀 시 재조회)로 대체. [업데이트]는 스토어 URL(Android `market://`, iOS `apps.apple.com/app/id{IosAppId}`). 에디터에선 `#if UNITY_EDITOR return`으로 스킵. **파일 관리**: `nana-version.json`은 `WaterSortPuzzle/hosting/public/`에 위치 (두 게임이 `nana-no2` Firebase Hosting 프로젝트 공유. 각자 파일명 다름: NANA=`nana-version.json`, WaterSort=`version.json`). 값 수정 후 `cd WaterSortPuzzle/hosting && firebase deploy --only hosting`. CDN cache-control `max-age=300` (5분) 걸려있어 배포 5분 이내 전 유저 반영. **왜 정적 JSON**: 예전엔 Firestore `config/app_version` 문서였는데 Firebase SDK 초기화 대기 + hang 리스크로 팝업이 늦게/안 뜨는 이슈 있어 정적 JSON + UnityWebRequest로 전환. Firestore 는 유저별 데이터(`rewards`, `code_index`)만 남김.
- LivesSystem.cs: 목숨(하트) 시스템. **규칙**: 기본 3(최대 99), 리셋마다 -1, **판 클리어 시 상태 그대로 이월(값·회복 타이머 앵커 모두 유지)**, 광고 시청 시 +3(판 진행 중엔 99까지 스택). 정책 히스토리: "누적 유지"(초기) → "매판 3 리셋"(2026-08-02) → "누적 유지 복귀"(2026-08-20, 발주자 재지시). **자동 복구**: 3 미만이면 **10분마다 1개씩** 회복(상한 3). `lastLostAt`은 다음 회복 tick의 앵커로 사용되어, tick 발생 시 그만큼 앵커를 앞으로 이동시켜 남은 카운트 유지. 앱을 오래 껐다 켜도 정확히 계산(예: 25분 후 → 2개 회복 + 5분 남은 카운트 유지). **저장**: PlayerPrefs `livesCurrent` + `livesLostAt` 두 키. **UI**: 상단 하트 표시(TMP_Text) + 하트 옆 `recoveryTimerText`(MM:SS, 하트 < 3일 때만 표시) + [+ 목숨 채우기] 버튼(자발적 광고 시청) + 잠금 팝업(**씬에 만들어둔 `LivesLockCanvas` GameObject를 SerializeField로 참조 → SetActive 토글**). 인스펙터 필드: `lockPopup` / `statusText` / `watchButton`. `HandleAdStatus`가 AdManager.OnRewardedStatus 구독해 로드 중일 때 fillButton.interactable=false + statusText에 상태 문구 표시. `TickRecovery` 코루틴이 항상 돌면서 매초 회복 체크·타이머 갱신·잠금 팝업 자동 해제(하트 0→1로 회복 시 자동 잠금 해제). 로딩 스크린 사라진 뒤에만 팝업 표시. AdManager.ShowRewardedAd 완주 콜백으로 리필. **팝업 UI는 씬 배치로 마이그레이션됨** — 예전엔 코드로 Canvas/Card/Text를 동적 생성했지만 시각 iteration 불편해서 씬 편집 방식으로 전환. UI 헬퍼(BuildRoundedSprite/MakeImage/AddText/AddButton 등) 및 관련 필드 전부 삭제(150+줄). **Task #7 논의**: 하트 4+ 상태에서 광고 강제 로직은 광고 시청 인센티브 소멸 위험으로 폐기 결정.
- BackButtonHandler.cs: 안드로이드 뒤로가기 버튼 처리(KeyCode.Escape 매핑, iOS는 자연스레 no-op). 우선순위: (1)자기 팝업 뜸→닫기 (2)강제 업데이트 팝업(UpdatePromptCanvas)→무시 (3)설정창(SettingsPanel.IsOpen)→닫기 (4)그 외(하트 잠금 포함)→종료 확인 팝업. 팝업: 반투명 배경 + 라운드 흰 카드 + [돌아가기]/[종료하기] 버튼(카드/버튼 모두 procedural rounded sprite). sortingOrder 29500(LivesLock 29000보다 위, UpdatePrompt 30000보다 아래). 씬에 빈 GameObject 만들어 부착, 인스펙터의 Settings Panel 슬롯에 SettingsUI 연결.
- SettingsPanel.cs: BackButtonHandler에서 열림 상태 확인용 `public bool IsOpen` 프로퍼티 노출(`overlay.resolvedStyle.display == Flex`).
- NotificationHelper.cs: 하트 회복 완료 로컬 알림. `com.unity.mobile.notifications` 2.4.3 사용. 씬에 GameObject 부착. LivesSystem이 상태 변경 5곳(Start/Decrement/OnLevelCleared/ApplyRewardedAdReward/TickRecovery)에서 `Instance.ScheduleHeartFull` / `Cancel` 호출. Android Small Icon은 **투명 배경 + 흰 픽셀만** 인식(컬러/미등록 시 알림 안 뜸). `Assets/Editor/NotificationIconTool.cs`가 흰 하트 실루엣 자동 생성 메뉴 제공. 앱 실행 중 하트 3 도달 시 Cancel은 의도된 스팸 방지 동작(유저가 앱에서 직접 확인 가능하므로).
- AdMob Mediation: Android 미디에이션 그룹(NANA-Android-Interstitial/Rewarded)에 Unity Ads 소스 붙임. UnityAds Unity Mediation Plugin v3.19.0을 Source zip → 수동 복사 방식으로 임포트(`Assets/GoogleMobileAds/Mediation/UnityAds/`) — GoogleMobileAds 11.2.0이 Assets 방식이라 v3.19.0의 UPM 배포와 의존성 불일치로 UPM 실패. Unity Ads Dashboard 나나박스 Android(Game ID 800107828, Interstitial_Android/Rewarded_Android placement) 세팅. AdMob 콘솔 + Unity Ads Dashboard 양쪽 다 SJ Phone(광고 ID) 테스트 기기 등록. Ad Inspector Single Ad Source Test로 Unity Ads 응답 검증 완료(재고 있음). iOS 미디에이션은 다음 세션.

## UI Toolkit 구조
- SettingsUI (GameObject): UIDocument + SettingsPanel.cs. Assets/UI/SettingsPanel.uxml/uss 사용.
- RewardUI (GameObject): UIDocument + RewardManager.cs. Assets/UI/RewardPanel.uxml/uss 사용.
- PanelSettings: Assets/UI/PanelSettings.asset. Scale With Screen Size, 1080×1920, Match Height.
- USS에서 폰트: `-unity-font: url("../TextMesh Pro/Fonts/NanumSquareRoundEB.ttf")` 로 연결.
- UIDocument 오브젝트는 눈 아이콘(씬 뷰 숨김)으로 숨기면 런타임에 영향을 주므로 사용 금지. 대신 UXML에서 `style="display: none;"` 으로 초기 숨김 처리.

## UGUI Canvas (HUD) 반응형 세팅
- **주의**: UGUI Canvas와 UI Toolkit PanelSettings는 별개 시스템. PanelSettings 반응형이라도 UGUI Canvas는 별도 세팅 필요.
- **CanvasScaler**: Scale With Screen Size / Reference Resolution 1080×2340 (갤럭시 실기 기준) / Match Width(0). 폭 기준이라 UI 원본 크기 유지, 세로가 긴 폰(폴더블/아이폰)에선 여백만 늘어남.
- **HUD 요소 앵커** (Canvas 자식):
  - 상단 중앙: LevelText, CountText — Top Center (0.5, 1)
  - 좌상단: SettingsButton — Top Left (0, 1)
  - 우상단: Heart, LiveCountText, Timer — Top Right (1, 1)
  - 하단 중앙: PlusHeartButton — Bottom Center (0.5, 0)
  - 전체 stretch: LoadingPanel — Stretch (0,0)/(1,1) + AspectRatioFitter Envelope Parent
- **LoadingPanel AspectRatioFitter**: aspectMode Envelope Parent + aspectRatio 0.4615 (=1080/2340, GameTitle.png 실제 비율). aspectRatio 잘못 잡으면 오버플로우(폭 튀어나옴) — 이미지 비율 바꿀 때 이 값도 같이 바꿔야 함.
- **함정**: CanvasScaler 기본값이 Constant Pixel Size + 800×600(옛 유니티 기본)이라 초기엔 반응형 아님. 잊고 그대로 두면 실기에서 UI가 어긋남 (2026-07-29 발견·수정).

## LivesLockCanvas (하트 잠금 팝업 씬 배치)
- 씬에 별도 UGUI Canvas로 존재 (sortingOrder 29000, ScreenSpaceOverlay, Scale With Screen Size 1080×2340 Match Width). LivesSystem이 SetActive로 열고 닫음. 초기 SetActive false.
- **계층**: `LivesLockCanvas → Background(어둠) / CardBorder(분홍 프레임) / Card(크림핑크) → Title / Divider / Message / StatusText / WatchButton→Text`
- **9-slice 스프라이트**: `Assets/Art/Rectangle 1.png` — Figma에서 100×100 라운드 사각형(코너 10, 흰색) → 2x export → Sprite Editor에서 Border 20 세팅. CardBorder/Card/WatchButton 3개 Image가 이 하나를 공유하고 색은 각자 tint. **함정**: Sprite Editor에서 Multiple 모드로 자동 전환되면 spriteBorder가 최상위에서 0으로 리셋됨 — .meta 파일에서 `spriteMode: 1` + `spriteBorder` 값 확인 필요.
- **문구 톤**: "광고" 단어 유저 UI에 노출 금지. "달콤한 과자가 기다리고 있어요" 같은 카피는 실물 과자 상품 티저 (100판 클리어 보상). 상태 메시지는 `AdManager.RewardedLoadingMessage`/`RewardedFailedMessage` 상수에서 관리.
- **버튼 라벨 스왑 금지**: `+3` 같은 작은 버튼 라벨에 긴 상태 메시지 넣으면 오버플로우로 UI 깨짐. 대신 `Button.interactable` 토글로 로딩 중 회색 처리.

## 게임 규칙 / 데이터 모델
- 칸 좌표는 Vector2Int (x=열, y=행), (0,0)은 왼쪽 아래.
- 시작 칸에서 출발, 상하좌우 인접 칸만 이동, 이미 채운 칸 재방문 불가, 막힌 칸 이동 불가.
- 채운 칸 리스트(path) 길이가 전체 칸 수와 같으면 클리어.

## 레벨 진행 로직 (랜덤 셔플)
- **1~10**: 순차 진행 (진입장벽 유지)
- **11-30 / 31-70**: 각 그룹 안에서 미클리어 판 풀에서 랜덤 뽑기
- **71-100 슬롯 (30번 뽑기) = asset index 70~109 풀 (40개)**: 매 세션마다 유저가 못 본 판 10개가 달라져 재플레이 유도
- **매판 재선택 (옵션 B)**: 앱 재실행할 때마다 미클리어 풀에서 다시 랜덤. 리롤 편법(앱 껐다 켜기) 감수함.
- 화면 표시는 실제 레벨 번호가 아니라 진행도 "N/100" (`TotalDisplayLevels` 상수). asset 개수(110)와 별개.
- **100판 클리어 = 보상 트리거**. 이후에도 그룹 D 미클리어 10개 자유 플레이 가능하지만 RewardManager가 중복 발급 차단.
- 구버전 유저 마이그레이션: legacy `currentLevel` → 앞 N개(index 0~N-1) 클리어로 간주.

## 목숨(하트) 시스템
- **표시**: 상단 `x N` 형태 (하트 아이콘 + TMP_Text) + 옆에 회복 카운트 MM:SS (하트 < 3일 때만 노출)
- **규칙**: 기본 3(상한 99), 리셋 -1, 광고 시청 +3(판 안에선 99까지 스택), **판 클리어 시 상태 그대로 이월**(값·타이머 앵커 유지)
- **잠금**: 목숨 0 → 오버레이(반투명+흰 카드+[하트 채우러 가기]). LoadingScreen 사라진 뒤에만 표시. 하트 0→1 회복 순간 자동 해제
- **자동 복구**: 3 미만 → **10분마다 1개씩** 회복(상한 3). 0에서 3까지 총 30분 소요
- **광고 로드 실패(Private DNS 등)**: 유저는 10분 대기하면 1개는 회복됨 (강경 정책 완화됨)
- **자발적 시청**: `+ 목숨 채우기` 버튼으로 언제든 +3 스택 가능
- **뒤로가기 처리**: [[BackButtonHandler.cs]]가 잠금 상태에서도 뒤로가기 → 종료 팝업 띄워줌(탈출 수단 제공)

## 진행 상황 (완료)
- [x] 폴더 구조, Git 초기화 및 첫 커밋
- [x] LevelData (격자 기반 데이터 모델)
- [x] Level_1 에셋 (3x3)
- [x] BoardRenderer: 격자 렌더링 + 입력 + 칸 채우기 + 되돌리기 + 클리어 판정
- [x] 에디터에서 플레이 가능 (MVP 완성)
- [x] 채워지는 경로를 실제 선(LineRenderer)으로 그리기
- [x] 레벨 선택 + 레벨 여러 개
- [x] blockedCells로 비정형 모양 레벨 만들기
- [x] BoardRenderer.cs, GameManager.cs 분리
- [x] 현재 라운드 저장
- [x] LoadingScreen 추가
- [x] 모바일: 카메라 자동 맞춤, 60fps
- [x] 안드로이드 빌드 & 실기기 테스트
- [x] 색상 테마: 경로 칸 분홍, 경로 선 진한 분홍, 클리어 라벤더
- [x] 효과음: 칸 채울 때 fillSound, 클리어 시 winSound (인스펙터에서 연결)
- [x] 레벨 번호 텍스트(LevelText) UI 표시
- [x] 선 색상 #ff8a8a로 변경, lineColor 필드로 통합
- [x] 시작 칸 중앙 흰 원: 첫 이동 시 선 색으로 채워지며 유지
- [x] 클리어 시 마지막 칸에 같은 색 원 팝 등장
- [x] 리팩터링: path[^1], static readonly dirs, WaitForSeconds 캐싱
- [x] 셋팅창 제작 (사운드, 진동)
- [x] Firebase Firestore 연동 (SDK 13.13.0)
- [x] 모든 레벨 클리어 시 랜덤 인증코드 발급 + Firestore 저장 (중복 방지)
- [x] RewardPanel UI (코드 표시 + 복사 버튼)
- [x] Google AdMob 전면 광고 연동 (SDK 11.2.0, 3레벨마다 표시, 실기기 테스트 완료)
- [x] 막힘 리셋 N번마다 전면 광고 표시 (기본 4번, AdManager.showAdEveryNStucks로 조정)
- [x] 레벨 100개 제작 완료 (Level_1 ~ Level_100)
- [x] SettingsPanel UI Toolkit 전환 (UXML/USS, SettingsUI GameObject + UIDocument)
- [x] RewardPanel UI Toolkit 전환 (UXML/USS, RewardUI GameObject + UIDocument)
- [x] PanelSettings: Scale With Screen Size, 1080×1920 기준, Match Height(1) — 모바일 해상도 대응
- [x] 로딩화면 이미지 GameTitle.png로 교체, Aspect Ratio Fitter(Envelope Parent)로 전체화면 대응
- [x] Firestore 보안 규칙 수정 — rewards 컬렉션 get/create만 허용 (update·delete·list 차단)
- [x] 개인정보처리방침 페이지 제작 — GitHub Pages 배포 (https://apppiel.github.io/NANA_puzzle/privacy-policy.html)
- [x] 설정창 "처음부터 시작" 버튼 추가 (진행 상황 초기화 + 1레벨로 이동)
- [x] 시작 칸 탭 시 경로 초기화 기능 제거
- [x] 재설치 시 진행 상황 초기화 — firstInstallTime 비교로 Google 자동 백업 복원 문제 해결
- [x] iOS ATT(App Tracking Transparency) 구현 — ATTBridge.mm(네이티브 플러그인), iOSPostBuild.cs(프레임워크 자동 링크 + Info.plist 문구 추가 + Unity Ads SKAdNetwork ID 병합)
- [x] iOS App Store 심사 제출 (v1.0 build 1)
- [x] iOS App Store 출시 완료 (v1.0)
- [x] AdMob 앱 등록 (iOS 출시 후 광고 미노출 → 등록으로 해결)
- [x] app-ads.txt 개발자 도메인(nanabox.co.kr)에 배포 및 AdMob 인증 완료
- [x] iOS UIDocument pickingMode 버그 수정 — 설정창 버튼 터치 불가 문제 (SettingsPanel, RewardManager)
- [x] iOS v1.0.1 업데이트 배포 (UIDocument 터치 버그 수정 포함)
- [x] Android Play Store 심사 제출 (v1.0 build 1)
- [x] Play Console 계정 세부정보 — 사업자등록번호·전자상거래 라이선스 입력
- [x] Play Console 스토어 리스팅 (앱 설명, 스크린샷)
- [x] Play Console 데이터 보안 섹션 작성 (AdMob·Firebase 수집 데이터 신고)
- [x] Firestore 보안 규칙 확장 — code_index, claims 컬렉션 추가 (get/create만 허용)
- [x] 웹 경품 신청 페이지 제작 (Cafe24/reward-claim.html) — 코드 검증 + 배송 주소 제출
- [x] RewardManager: code_index/{코드}에 동시 저장 로직 추가 — 웹 검증용 (deviceId, createdAt 필드 포함)
- [x] 기존 발급 코드 2건 code_index/에 수동 백필 (Firebase 콘솔)
- [x] iOS 설정/클리어 버튼 무반응의 진짜 원인 규명 — activeInputHandler가 0(Input Manager only)로 잘못 설정되어 EventSystem의 Input System UI Input Module과 mismatch. Both(2)로 복구. UIDocument pickingMode/display 수정은 실제 원인이 아니었음 (v1.0.1의 pickingMode 수정은 그대로 유지)
- [x] 사고 후 Firebase 네이티브 라이브러리 재복구 (FirebaseCppApp-13_13_0.bundle/.so) — Firebase Unity SDK 13.13.0 재임포트로 복원. 이 파일들은 100MB 초과로 .gitignore 등록되어 있어 로컬만 존재. macOS Gatekeeper quarantine 제거 필요
- [x] Level Solver + Generator 에디터 툴 제작 (Assets/Editor/LevelSolverWindow.cs) — 정답 경로 카운팅, 배치 스캔, shape 템플릿(다이아/십자/육각), clustered 랜덤 배치, cancel 버튼. 전체 100 레벨 등급 매김 완료 (Test/Solver_result.txt)
- [x] 난이도 곡선 검증 — 1~69는 대부분 정답 1~15개(적절한 어려움), 73~90은 대부분 100+(너무 쉬움), 91~98 일부 타임아웃(매우 어려움)
- [x] Shape 가설 검증 완료 — 8×8 다이아몬드(40칸)가 11×11 사각형(121칸)보다 훨씬 어려움. 격자 크기가 아니라 shape 구조가 난이도 지배 요인
- [x] 난이도 조정 스코프 축소 합의 — 73~89는 유지, 90~100 중 "쉬움"인 5개(90, 93, 96, 99, 100)만 재설계 예정
- [x] Level 73, 74 실험적 재설계 — 73은 Diamond 8×8, 74는 Hexagon 8×8. Shape 가설 검증 과정에서 적용된 것. 합의 스코프 밖이라 유지 vs 원복 결정 필요
- [x] 레벨 랜덤 셔플 로직 도입 — 1~10 순차 / 11-30·31-70·71-100 그룹 안 미클리어 풀 랜덤. 매판 재선택(옵션 B). 저장은 clearedCount + clearedMask 100자 문자열. 구버전 마이그레이션 포함.
- [x] 스크린 캡처 방지 (ScreenCaptureProtection + ScreenCaptureBridge.mm) — Android FLAG_SECURE 완전 차단 / iOS 감지 오버레이
- [x] 강제 업데이트 팝업 (UpdateChecker.cs) — Firestore `config/app_version` 문서에서 최신 버전 조회 후 낮으면 [닫기]/[업데이트] 팝업
- [x] 에디터 전용 `editorTestLevel` 필드 — GameManager에 특정 레벨 드래그하면 랜덤 무시하고 그 레벨만 반복(테스트용, 빌드엔 미포함)
- [x] Android v1.0.3 배포 — 랜덤 셔플 + 캡처 방지 + 강제 업데이트 팝업 포함
- [x] Firestore 보안 규칙에 `config` 컬렉션 추가 (get 허용, list/create/update/delete 차단) — UpdateChecker가 config/app_version 문서 조회하기 위함
- [x] 강제 업데이트 팝업 부분 실기 검증 (2026-07-28) — 다른 사용자 폰에선 정상 표시 확인. 다만 [닫기]가 팝업만 없애고 앱은 계속 실행돼서 백그라운드 복귀 시 팝업 재표시 안 되는 문제 발견
- [x] AdManager 광고 카운트 로직 재작성 — 기존 `count % N == 0`은 광고 미준비 시 카운트 소진되어 다음 배수(6, 9)까지 스킵되던 버그. 임계치(`count >= N`) 방식으로 변경 + 표시 성공 시에만 리셋. LoadAd() 실패 시 30초 자동 재시도 추가. 이슈는 셔플과 무관하게 원래부터 존재
- [x] UpdateChecker [닫기]→[종료] + Application.Quit 동작으로 변경 — 강제 업데이트 우회 차단. 다음 실행 때 Start()가 재실행되며 팝업 다시 뜸
- [x] 셔플 그룹 D 확장 — GroupBoundaries `{0,10,30,70,110}`으로 확장, TotalDisplayLevels=100 상수 도입. 71-100 슬롯이 40개 풀에서 30개 랜덤. asset 110개 유지하되 유저 진행도/보상 트리거는 100으로 고정
- [x] AdManager Rewarded 광고 통합 — RewardedAd 로드/표시, ShowRewardedAd(Action) API 노출, 30초 재시도. Android/iOS Rewarded unit ID 각각 입력. 구글 공식 Rewarded 테스트 ID 상수 포함
- [x] LivesSystem 신규 — 목숨 상태 관리, 잠금 오버레이 UI(코드로 Canvas 생성 + procedural 라운드 스프라이트 자동 생성), 자발적 광고 시청 버튼, LoadingScreen 대기 후 표시, PlayerPrefs 저장(livesCurrent + livesLostAt)
- [x] AdManager "4번 리셋 광고" 로직 삭제 — LivesSystem으로 완전 대체 (판 안 3번 실패 = 하트 소진 = 잠금 오버레이 = 광고 시청)
- [x] Task #7 "하트 4+ 상태에서 3번 리셋 광고 강제" 도입 검토 → 폐기 — 광고 시청 인센티브 소멸 위험. 하트 시스템만으로 광고 유도 충분
- [x] LivesSystem 회복 규칙 변경 — 기존 "30분 후 한 번에 3 리필" → "**10분마다 1개씩** 회복(상한 3)". `ApplyAutoRecovery` tick 기반 재작성. 앱 오래 껐다 켜도 정확히 계산됨(elapsed / interval만큼 회복, 앵커 앞으로 이동)
- [x] LivesSystem 하트 옆 회복 타이머 UI — `recoveryTimerText` 인스펙터 필드 추가, MM:SS 표시. 하트 < 3일 때만 노출. `TickRecovery` 코루틴이 항상 돌며 갱신·잠금 자동 해제
- [x] BackButtonHandler 신규 — 안드로이드 뒤로가기 → 종료 확인 팝업. 우선순위(자기팝업/UpdateChecker/설정창/그외) 처리. 라운드 카드+버튼. SettingsPanel.IsOpen 프로퍼티 추가로 연동
- [x] Android Unity Ads Mediation 통합 (2026-07-29) — AdMob 콘솔에 Android Interstitial/Rewarded 미디에이션 그룹 생성 + Unity Ads 소스 매핑. Unity Ads Dashboard 나나박스 프로젝트/placements 확인. UnityAds Mediation Plugin v3.19.0 Source zip에서 `Assets/GoogleMobileAds/Mediation/UnityAds/`로 수동 복사(UPM 방식은 GoogleMobileAds 11.2.0 Assets 방식과 의존성 불일치로 실패). SJ Phone(광고 ID) AdMob·Unity Ads 양쪽 테스트 기기 등록. Ad Inspector Single Ad Source Test에서 Unity Ads 응답 확인. 평상시 mediation에선 AdMob이 eCPM 경쟁으로 이기는 게 자연스러움
- [x] UGUI Canvas 반응형 전환 (2026-07-29) — CanvasScaler를 Constant Pixel Size + 800×600 기본값에서 Scale With Screen Size + 1080×2340 + Match Width로 변경. HUD 8개 중 어긋난 3개(SettingsButton→Top Left, Heart/LiveCountText→Top Right)를 Middle Center에서 재앵커링(화면 좌표는 유지). LoadingPanel AspectRatioFitter aspectRatio 0.5625→0.4615(이미지 실제 비율)로 오버플로우 해소. 실기 검증 대기
- [x] AdManager 보상형 광고 Pending + 자동 표시 흐름 (2026-07-30) — 유저 첫 탭에서 광고 미준비면 대기열에 저장 + 15초 타임아웃 걸고, 로드 완료 순간 자동 재생(재탭 불필요). `OnRewardedStatus` 이벤트로 상태 문자열 브로드캐스트. 로드 실패 시 즉시 실패 안내(30초 재시도까지 안 기다림). LivesSystem이 구독해 fillButton.interactable 토글 + statusText 갱신
- [x] 유저 UI 문구 "광고"→"하트/과자" 세계관으로 통일 (2026-07-30) — RewardedLoadingMessage="하트 불러오는 중..", RewardedFailedMessage="하트가 길을 잃었어요. 잠시 후 다시 불러주세요." 상수화. 발주자 요구: 광고 시청 유도라는 사실을 노골적으로 드러내지 않기
- [x] LivesLockCanvas 씬 배치 마이그레이션 (2026-07-30) — 기존 코드로 Canvas/Card/Text 동적 생성 → 씬에 미리 만들어둔 UI를 SerializeField로 참조 + SetActive 토글로 변경. LivesSystem 150+줄 삭제(BuildRoundedSprite/MakeImage/AddText/AddButton 등 UI 헬퍼 전부). 이유: 매 색·폰트 변경마다 코드 편집+컴파일 반복 지옥에서 벗어나 시각 iteration 가능하게. 팝업 디자인도 개선(딥 로즈 ♥ 심볼 제목, 큰 메인 메시지 세로 중앙 배치, 분홍 프레임 + 크림핑크 카드, 코랄 버튼)
- [x] 라운드 스프라이트 도입 (2026-07-30) — Figma에서 만든 `Assets/Art/Rectangle 1.png` (100×100, corner radius 10 → 2x export → Border 20 9-slice). CardBorder/Card/WatchButton 3개 Image에 공유 + 색은 tint로 다르게. 함정 발견: Sprite Editor에서 Multiple 모드로 자동 전환되면 최상위 spriteBorder가 0으로 리셋됨 → .meta에서 `spriteMode: 1` 직접 세팅으로 복구
- [x] **RewardManager 완전 재작성 (2026-08-03, v1.0.8)** — 두 유저(Private DNS 이력)가 v1.0.7에서 100판 완주 팝업 뜬 뒤 "코드 생성 중..."에서 완전 락다운되는 이슈. 원인: v1.0.5 cb118ac 커밋 "저장 성공 뒤에만 코드 표시"로 fallback 제거 + `initTask.Result` 접근이 AggregateException 삼킴 + restart 버튼도 저장 완료 전 잠금. 재설계: 로컬(PlayerPrefs) 저장이 진짜 소스 + 초기화 12초 자체 타임아웃 + `IsFaulted` 명시 체크 + 실패 시 로컬 fallback + 백그라운드 자동 재시도 + restart 상시 활성화 + `FirebaseApp.LogLevel=Debug` 진단. 시뮬레이션 스위치·`[TEST] Clear Local Reward Code` 컨텍스트 메뉴 포함
- [x] 설정창 "인증코드 보기" 버튼 (2026-08-03, v1.0.8) — 코드 발급 이력이 있는 유저(로컬 코드 존재)에게만 노출. 눌러서 RewardManager.ShowReward() 재호출 → 로컬 코드 즉시 표시(synced면 그대로, 미동기화면 백그라운드 저장 재시도). RewardPanel [1레벨로]가 진행도 리셋해도 로컬 코드는 유지되므로 언제든 재열람 가능
- [x] Android v1.0.8 빌드 & 프로덕션 배포 (2026-08-03) — bundleVersion 1.0.8, AndroidBundleVersionCode 11. Play Console 프로덕션 트랙 직접 배포. 내부 테스트 우회는 발주자 결정(리스크 수용). 배포 후 두 유저 실기 검증 대기
- [x] UpdateChecker iOS 분기 — Apple HIG상 `Application.Quit` 리젝 리스크로 iOS 빌드에선 [종료] 버튼 제외, [업데이트] 하나만 가운데(폭 50%) 배치. `#if !UNITY_IOS`로 Android [종료]/[업데이트] 좌우 배치는 유지. iOS 우회 차단은 `OnApplicationPause` 재검사로 대체 (2026-08-03)
- [x] v1.0.10 하트 10탭 백도어 제거 — `Assets/Scripts/UI/HiddenBackdoor.cs`(+.meta) 삭제, `GameManager.SkipToAllClearedAndShowReward` 메서드 삭제, `SampleScene.unity`의 Heart GameObject에서 HiddenBackdoor MonoBehaviour 참조 정리. v1.0.10에서 실수로 리셋된 유저 구제용 임시 조치였고 목적 달성
- [x] iOS Unity Ads Mediation 통합 (2026-08-04) — Unity Ads Dashboard에 iOS 앱 등록(Game ID `800111550`) + placement 2개 생성(`Interstitial_iOS`, `Rewarded_iOS`). AdMob 콘솔에 iOS 미디에이션 그룹 2개 생성(`NANA-iOS-Interstitial`, `NANA-iOS-Rewarded`) + Unity Ads(입찰) 소스 매핑. `Assets/Editor/iOSPostBuild.cs`에 Unity Ads 요구 SKAdNetwork ID 76개 병합 로직 추가(`[PostProcessBuild(999)]`로 GoogleMobileAds `PListProcessor` 뒤에 실행 + HashSet 중복 스킵으로 idempotent). Unity Ads 목록 갱신 필요 시 배열만 교체하면 됨(출처: Unity Ads Dashboard > iOS 앱 상세 > "Unity Ads SDK 3.5.1 이상용 SKAdNetwork ID" 전체 목록 버튼). Xcode 빌드 후 Info.plist에 76개 실제 반영 실측만 남음
- [x] iOS v1.0.3 빌드 & App Store 심사 제출 (2026-08-04) — bundleVersion 1.0.3, iPhone buildNumber 5. Android v1.0.10과 기능 동등: 랜덤 셔플, 캡처 방지, 강제 업데이트(iOS는 [업데이트]만), 보상형 광고 + Pending 흐름, 목숨 시스템, 셔플 그룹 확장, RewardManager v1.0.8 재작성, 설정창 인증코드 보기, Firebase CheckAndFix 방어(v1.0.9), 완료 화면(v1.0.10), Unity Ads iOS mediation. Xcode Info.plist `SKAdNetworkItems=89` 실측 확인(목표 76+ 통과). iOS 버전은 Android와 독립 트랙(직전 1.0.1 → 1.0.3, 1.0.2 스킵. App Store Connect가 제안한 번호). 앱 암호화 exemption은 대화창에서 "4번(해당 없음)" 선택으로 통과
- [x] UpdateChecker Firestore → 정적 JSON 전환 — 예전 `config/app_version` Firestore 조회 방식은 Firebase SDK 초기화 대기 + hang 리스크로 팝업이 늦게/안 뜨는 이슈. `UnityWebRequest`로 `https://nana-no2.web.app/nana-version.json` 직접 조회하는 방식으로 전환. 5초 타임아웃 + 실패 시 30초 재시도 + `OnApplicationPause` 재조회
- [x] iOS v1.0.4 & Android v1.0.11 배포 완료 — 현재 라이브 최신 버전
- [x] `nana-version.json` 최초 생성 & 배포 (2026-08-17) — 정적 JSON 전환 후 파일 자체가 없어(404) UpdateChecker가 dormant 상태였음. `WaterSortPuzzle/hosting/public/nana-version.json` 생성(`{"androidLatestVersion":"1.0.11","iosLatestVersion":"1.0.4"}`) + `hosting/firebase.json`에 `/nana-version.json` 캐시 헤더(`max-age=300`) 추가. WaterSort와 hosting 공유(nana-no2), 파일명만 분리(WaterSort=`version.json`, NANA=`nana-version.json`) — 배포 명령이 `public/` 전체를 밀어넣는 방식이라 두 파일 모두 같은 폴더에서 관리 필요

## 다음 할 일 (TODO)
- [ ] AdMob 콘솔 Rewarded 광고 unit 실기 노출 검증 — `useTestAd=false` + 자기 기기 테스트 등록. Android/iOS 모두 라이브 배포 완료 상태라 이슈 미보고 시 자동 완료 처리 가능
- [ ] 다음 릴리즈 배포 시 `nana-version.json` 값 갱신 워크플로 — **스토어 심사 통과 후에만** `WaterSortPuzzle/hosting/public/nana-version.json` 값 올리고 `firebase deploy --only hosting`. 심사 통과 전 갱신 금지(리뷰어 기기 팝업 → 리젝 리스크)
- [ ] **디바이스별 UI 대응 (부분 완료)** — CanvasScaler·HUD 앵커·LoadingPanel aspectRatio는 처리됨(2026-07-29). 남은 것: (a) 실기 검증(갤럭시 S22+/S25/폴더블/아이폰), (b) SettingsButton localScale 2.1757 이상값 정상화(sizeDelta로 조절), (c) Safe Area(펀치홀·노치) 대응 여부 결정, (d) BoardRenderer.FitCamera 극단 비율(폴더블) 대응 검토. 참조: [[project-device-ui-adapt]]

## 협업 방식 메모
- 사용자는 Unity 입문자. 한국어로 단계별로 자세히 안내할 것.
- 에디터 GUI 작업(폴더/스크립트 생성, 컴포넌트 부착, 인스펙터 연결)은 사용자가 직접 함.
- 단순 편집은 사용자, 여러 파일에 걸친 코드 작업은 Claude Code가 담당.
- 커밋은 의미 있는 체크포인트마다.
- 코드마다 필요하다고 생각되는 부분에 이해하기 쉽게 추가적인 주석 달아줄 것.

