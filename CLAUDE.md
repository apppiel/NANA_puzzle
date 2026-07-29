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
- GameManager.cs: 레벨 목록·진행 상태 관리. 랜덤 셔플 로직(1~10 순차 / 11-30·31-70·**71-110** 그룹 안 미클리어 풀 랜덤 — 마지막 그룹은 40개 풀에서 30번 뽑음). 매 판 선택 시점마다 재선택(옵션 B, 앱 재실행·클리어 모두). PlayerPrefs에 clearedCount(int) + clearedMask 저장. **TotalDisplayLevels=100 상수** 도입해 asset 개수(110)와 유저 진행도(N/100)·보상 트리거를 분리. 구버전 currentLevel은 마이그레이션. 100판 클리어 시 RewardManager.ShowReward(), 판 클리어 시 livesSystem.OnLevelCleared() 호출. 리셋 이벤트는 livesSystem.Decrement()로 이관. `editorTestLevel` 필드(#if UNITY_EDITOR): 값이 있으면 랜덤 무시하고 그 레벨만 반복 로드.
- RewardManager.cs: 모든 레벨 클리어 시 랜덤 인증코드(예: A3K9-XZ21) 생성. Firebase Firestore에 기기 ID 키로 저장(중복 방지). UI Toolkit 기반 RewardPanel 제어. RewardUI GameObject에 UIDocument와 함께 부착.
- AdManager.cs: 전면 광고 + 보상형 광고 로드/표시. **전면 광고**: N레벨마다 표시(기본 3레벨). 카운트 임계치 방식(`count >= N`) — 광고 준비 안 됐으면 카운트 유지한 채 LoadAd()만 호출 → 다음 클리어에서 즉시 표시. 표시 성공 시에만 count=0 리셋. **보상형 광고**: `ShowRewardedAd(Action onReward)` 노출. 완주 콜백에서 onReward 실행(중도 종료 시 미실행). 로드 실패 시 30초 자동 재시도(`CancelInvoke`로 중복 방지). Rewarded unit ID는 Android/iOS 각각 별도. **기존 "4번 리셋 광고" 로직 삭제됨** — 목숨 시스템(LivesSystem)으로 완전 대체.
- SettingsPanel.cs: UI Toolkit 기반 설정 패널 제어. SettingsUI GameObject에 UIDocument와 함께 부착. SettingsManager와 분리되어 있어 UI만 담당.
- Level_1.asset: 3x3, startCell (0,0). 첫 테스트 레벨.
- Assets/google-services.json: Firebase 프로젝트 설정 파일. 패키지명 com.nanaBox.NANApuzzle.
- LevelSolverWindow.cs: (Editor 툴, Tools > NANA > Level Solver 메뉴) 각 레벨의 Hamiltonian path 개수를 DFS+백트래킹으로 세서 난이도 등급 매김. 30초 타임아웃, 100+ 조기 종료, 연결성 pruning 적용. Level Generator 섹션: shape 템플릿(Rectangle/Diamond/Cross/Hexagon) + 구조적 clustered 배치 → solver 검증해서 목표 정답 범위에 맞는 후보만 뽑음. 후보 채택 시 대상 LevelData asset 덮어쓰기 가능.
- ScreenCaptureProtection.cs: 캡처 방지. Android는 UI 스레드에서 FLAG_SECURE 세팅(스크린샷·녹화·미러링 완전 차단). iOS는 UIScreen.isCaptured 폴링해 감지 시 최상단 검은 오버레이(sortingOrder=32767). 씬에 GameObject 하나 만들어 부착. Android는 runOnUiThread가 비동기라 람다 안에서 activity를 재획득해야 함(dispose 이슈).
- ScreenCaptureBridge.mm: iOS 네이티브 브릿지. `_IsScreenBeingCaptured()` 하나만 노출. UIKit 프레임워크 사용.
- UpdateChecker.cs: 강제 업데이트 유도. 앱 시작 시 Firestore `config/app_version` 문서에서 `androidLatestVersion`/`iosLatestVersion` 조회 후 Application.version과 비교. System.Version으로 비교, 낮으면 UGUI 팝업(반투명 배경 + 흰 카드 + [종료]/[업데이트]). **[종료] = Application.Quit** (강제 업데이트라 팝업만 없애는 우회 차단, 다음 실행 때 Start()가 다시 돌면서 팝업 재표시). [업데이트]는 스토어 URL 열기(Android market://, iOS apps.apple.com/app/id{IosAppId}). 에디터에선 `#if UNITY_EDITOR return`으로 스킵. Start()에서만 체크하므로 백그라운드 복귀는 트리거 안 됨 — [종료] 유도로 이 문제 해소.
- LivesSystem.cs: 목숨(하트) 시스템. **규칙**: 기본 3(최대 99), 리셋마다 -1, 판 클리어 시 3 미만이면 3으로 리필/3 이상이면 그대로 유지(누적 자원 소모형), 광고 시청 시 +3. **자동 복구**: 3 미만이면 **10분마다 1개씩** 회복(상한 3). `lastLostAt`은 다음 회복 tick의 앵커로 사용되어, tick 발생 시 그만큼 앵커를 앞으로 이동시켜 남은 카운트 유지. 앱을 오래 껐다 켜도 정확히 계산(예: 25분 후 → 2개 회복 + 5분 남은 카운트 유지). **저장**: PlayerPrefs `livesCurrent` + `livesLostAt` 두 키. **UI**: 상단 하트 표시(TMP_Text) + 하트 옆 `recoveryTimerText`(MM:SS, 하트 < 3일 때만 표시) + [+ 목숨 채우기] 버튼(자발적 광고 시청) + 잠금 오버레이(코드로 Canvas 생성, 라운드 스프라이트도 procedural 생성). `TickRecovery` 코루틴이 항상 돌면서 매초 회복 체크·타이머 갱신·잠금 오버레이 자동 해제(하트 0→1로 회복 시 자동 잠금 해제). 로딩 스크린 사라진 뒤에만 오버레이 표시. AdManager.ShowRewardedAd 완주 콜백으로 리필. **Task #7 논의**: 하트 4+ 상태에서 광고 강제 로직은 광고 시청 인센티브 소멸 위험으로 폐기 결정.
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
- **규칙**: 기본 3(상한 99), 리셋 -1, 광고 시청 +3, 판 클리어 시 3 미만이면 3 리필/3 이상이면 유지
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
- [x] iOS ATT(App Tracking Transparency) 구현 — ATTBridge.mm(네이티브 플러그인), iOSPostBuild.cs(프레임워크 자동 링크 + Info.plist 문구 추가)
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

## 다음 할 일 (TODO)
- [x] **하트 회복 완료 로컬 알림** — Unity `com.unity.mobile.notifications` 2.4.3 도입. NotificationHelper.cs로 예약/취소 캡슐화, LivesSystem이 상태 변경 지점 5곳(Start/Decrement/OnLevelCleared/ApplyRewardedAdReward/TickRecovery)에서 RefreshRecoveryNotification 호출. 설정창에 알림 토글 추가(기본 ON). Android Small Icon은 흰 실루엣 하트(NotificationIconTool 에디터 메뉴로 자동 생성). 앱 실행 중 하트 3 도달 시 알림 취소는 의도된 스팸 방지 동작. 실기 검증 완료 2026-07-29
- [ ] AdMob 콘솔 실제 Rewarded 광고 unit 활성화 확인 — 생성 직후 몇 시간 지연 있을 수 있음. `useTestAd=false` + 자기 기기 테스트 기기로 등록해서 실기 검증
- [ ] Android v1.0.4 빌드 — AdManager 광고 카운트 수정 + UpdateChecker [종료] + 목숨 시스템 + 셔플 그룹 확장 반영. bundleVersion을 1.0.4로 올린 뒤 빌드/배포
- [ ] Android v1.0.3 실기 재검증 — 스크린 캡처 방지가 처음엔 안 걸림(runOnUiThread + using dispose 이슈). 람다 안 activity 재획득으로 수정 후 재빌드 필요할 수 있음. 최근앱 미리보기가 검게 나오는지 확인.
- [ ] v1.0.4 배포 후 [종료] 실기 검증 — 팝업에서 [종료] 탭 시 앱 프로세스 완전 종료(최근 앱 목록에서도 사라짐) → 재실행 시 팝업 재표시 확인
- [ ] iOS v1.0.2 빌드 & App Store 제출 — code_index 저장 로직 + **activeInputHandler=Both 복구** + 랜덤 셔플 + 캡처 방지 + 강제 업데이트 + AdManager 광고 카운트 수정 + UpdateChecker [종료] + 목숨 시스템 + 셔플 그룹 확장 반영. iOS에서 Application.Quit이 심사에 문제되는지 리젝 사례 사전 조사 권장
- [ ] 새 레벨 Level_101~110 제작 완료 — 90~100 난이도 수준(8×8 Diamond/Hexagon/Cross). GameManager `levels` 배열에 뒤로 10개 드래그해서 총 110개 등록. LevelSolverWindow로 정답 1~30개 목표
- [ ] LivesSystem 실기 검증 — 하트 소진 → 잠금 UI → [광고 보기] → 실제 Rewarded 광고 재생 → 완주 → +3 리필. Private DNS 차단 유저 케이스도 확인(30분 대기 동작)
- [ ] iOS 실기 테스트 — 라이트닝 케이블 준비하거나 TestFlight 업로드로 검증
- [x] **Level 90, 93, 96, 99, 100 재설계** — Level Solver Generator 사용, 8×8 shape 위주 (Diamond/Hexagon/Cross). 각각 정답 1~30 목표로 후보 뽑아 채택. 참조: [[project-level-90-100-redesign]]
- [ ] Level 73, 74 실험 변경 처리 — 요구자와 확인 후 유지할지 원복할지 결정. 실기 플레이해서 어려운지 검증 후 판단 권장
- [ ] Level 91, 92, 94, 95, 97, 98 (타임아웃 = 이미 매우 어려움) 유지. 건들지 말 것
- [ ] **Private DNS 광고 차단 유저 대응 회의** — 일부 유저가 갤럭시 개인 DNS를 광고 차단 서버로 지정해서 AdMob 로드 실패. 앱 레벨에서 우회 불가능. 대응 옵션: (1) 감내 (2) Firestore에 실패율 익명 로깅해 규모 파악 (3) 광고 없이 진행 못 하게 강경 대응 (4) 보상형 광고 병행 (5) 광고 제거 인앱결제. 규모 데이터 없이 3~5 진행은 오버엔지니어링일 수 있음
- [ ] **iOS Unity Ads Mediation** — Android 통합과 동일하게 iOS 미디에이션 그룹(Interstitial/Rewarded) 생성, Unity Ads Dashboard에 iOS 앱 등록(Game ID/placements), Adapter 임포트 이미 되어있음(Platforms/iOS 폴더 포함). iOSPostBuild.cs의 SKAdNetwork 자동 반영 여부는 실제 Xcode 빌드 후 Info.plist 확인 필요
- [ ] **디바이스별 UI 대응 (부분 완료)** — CanvasScaler·HUD 앵커·LoadingPanel aspectRatio는 처리됨(2026-07-29). 남은 것: (a) 실기 검증(갤럭시 S22+/S25/폴더블/아이폰), (b) SettingsButton localScale 2.1757 이상값 정상화(sizeDelta로 조절), (c) Safe Area(펀치홀·노치) 대응 여부 결정, (d) BoardRenderer.FitCamera 극단 비율(폴더블) 대응 검토. 참조: [[project-device-ui-adapt]]

## 협업 방식 메모
- 사용자는 Unity 입문자. 한국어로 단계별로 자세히 안내할 것.
- 에디터 GUI 작업(폴더/스크립트 생성, 컴포넌트 부착, 인스펙터 연결)은 사용자가 직접 함.
- 단순 편집은 사용자, 여러 파일에 걸친 코드 작업은 Claude Code가 담당.
- 커밋은 의미 있는 체크포인트마다.
- 코드마다 필요하다고 생각되는 부분에 이해하기 쉽게 추가적인 주석 달아줄 것.

# 공통 행동 지침

LLM의 흔한 코딩 실수를 줄이기 위한 행동 지침. 프로젝트 지침과 함께 사용.

**트레이드오프:** 이 지침은 속도보다 신중함을 우선시함. 사소한 작업은 상황에 맞게 판단할 것.

## 1. 코딩 전에 먼저 생각하기

**가정하지 말 것. 혼란을 숨기지 말 것. 트레이드오프를 드러낼 것.**

구현 전에:
- 가정한 내용을 명확히 밝힐 것. 불확실하면 질문할 것.
- 해석이 여러 가지라면 선택지를 제시할 것. 혼자 선택하지 말 것.
- 더 단순한 방법이 있다면 말할 것. 필요하면 반론을 제기할 것.
- 불분명한 부분이 있으면 멈출 것. 무엇이 헷갈리는지 짚고 질문할 것.

## 2. 단순함 우선

**문제를 해결하는 최소한의 코드. 추측성 코드는 금지.**

- 요청한 것 이상의 기능은 추가하지 말 것.
- 한 번만 쓰는 코드에 추상화 계층 만들지 말 것.
- 요청하지 않은 "유연성"이나 "설정 가능성" 넣지 말 것.
- 불가능한 시나리오에 대한 에러 처리 넣지 말 것.
- 200줄로 짰는데 50줄로 될 것 같으면 다시 짤 것.

스스로에게 물어볼 것: "시니어 엔지니어가 보면 과하다고 할까?" 그렇다면 단순하게.

## 3. 최소한의 변경

**꼭 필요한 부분만 건드릴 것. 내가 만든 문제만 정리할 것.**

기존 코드 수정 시:
- 인접한 코드, 주석, 포맷을 "개선"하지 말 것.
- 안 망가진 건 리팩터링하지 말 것.
- 스타일이 마음에 안 들어도 기존 스타일에 맞출 것.
- 관련 없는 죽은 코드를 발견하면 언급은 하되 삭제하지 말 것.

내 변경으로 생긴 잔재가 있다면:
- 내 변경으로 인해 쓰이지 않게 된 import/변수/함수는 제거할 것.
- 원래부터 있던 죽은 코드는 요청 없이 건드리지 말 것.

기준: 변경된 모든 줄이 사용자의 요청과 직접 연결되어야 함.

