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
- GameManager.cs: 레벨 목록·진행 상태 관리. 랜덤 셔플 로직(1~10 순차 / 11-30·31-70·71-100 그룹 안 미클리어 풀 랜덤). 매 판 선택 시점마다 재선택(옵션 B, 앱 재실행·클리어 모두). PlayerPrefs에 clearedCount(int) + clearedMask(100자 "01" 문자열) 저장. 구버전 currentLevel은 마이그레이션(앞 N개 클리어 처리). 진행도 표시 "N/100"을 levelText·roundCountText 두 곳에. 100판 클리어 시 RewardManager.ShowReward() 호출. `editorTestLevel` 필드(#if UNITY_EDITOR): 값이 있으면 랜덤 무시하고 그 레벨만 반복 로드(테스트용, 빌드엔 미포함).
- RewardManager.cs: 모든 레벨 클리어 시 랜덤 인증코드(예: A3K9-XZ21) 생성. Firebase Firestore에 기기 ID 키로 저장(중복 방지). UI Toolkit 기반 RewardPanel 제어. RewardUI GameObject에 UIDocument와 함께 부착.
- AdManager.cs: 전면 광고 로드/표시 담당. N레벨마다 광고 표시 (기본 3레벨) + 막혀서 리셋 N번마다 광고 표시 (기본 4번). 인스펙터에서 횟수 조정 가능. useTestAd 체크 해제 시 실제 광고로 전환.
- SettingsPanel.cs: UI Toolkit 기반 설정 패널 제어. SettingsUI GameObject에 UIDocument와 함께 부착. SettingsManager와 분리되어 있어 UI만 담당.
- Level_1.asset: 3x3, startCell (0,0). 첫 테스트 레벨.
- Assets/google-services.json: Firebase 프로젝트 설정 파일. 패키지명 com.nanaBox.NANApuzzle.
- LevelSolverWindow.cs: (Editor 툴, Tools > NANA > Level Solver 메뉴) 각 레벨의 Hamiltonian path 개수를 DFS+백트래킹으로 세서 난이도 등급 매김. 30초 타임아웃, 100+ 조기 종료, 연결성 pruning 적용. Level Generator 섹션: shape 템플릿(Rectangle/Diamond/Cross/Hexagon) + 구조적 clustered 배치 → solver 검증해서 목표 정답 범위에 맞는 후보만 뽑음. 후보 채택 시 대상 LevelData asset 덮어쓰기 가능.
- ScreenCaptureProtection.cs: 캡처 방지. Android는 UI 스레드에서 FLAG_SECURE 세팅(스크린샷·녹화·미러링 완전 차단). iOS는 UIScreen.isCaptured 폴링해 감지 시 최상단 검은 오버레이(sortingOrder=32767). 씬에 GameObject 하나 만들어 부착. Android는 runOnUiThread가 비동기라 람다 안에서 activity를 재획득해야 함(dispose 이슈).
- ScreenCaptureBridge.mm: iOS 네이티브 브릿지. `_IsScreenBeingCaptured()` 하나만 노출. UIKit 프레임워크 사용.
- UpdateChecker.cs: 강제 업데이트 유도. 앱 시작 시 Firestore `config/app_version` 문서에서 `androidLatestVersion`/`iosLatestVersion` 조회 후 Application.version과 비교. System.Version으로 비교, 낮으면 UGUI 팝업(반투명 배경 + 흰 카드 + [닫기]/[업데이트]). [업데이트]는 스토어 URL 열기(Android market://, iOS apps.apple.com/app/id{IosAppId}). 에디터에선 `#if UNITY_EDITOR return`으로 스킵. 팝업은 매번 뜸(닫아도 세션 안 저장).

## UI Toolkit 구조
- SettingsUI (GameObject): UIDocument + SettingsPanel.cs. Assets/UI/SettingsPanel.uxml/uss 사용.
- RewardUI (GameObject): UIDocument + RewardManager.cs. Assets/UI/RewardPanel.uxml/uss 사용.
- PanelSettings: Assets/UI/PanelSettings.asset. Scale With Screen Size, 1080×1920, Match Height.
- USS에서 폰트: `-unity-font: url("../TextMesh Pro/Fonts/NanumSquareRoundEB.ttf")` 로 연결.
- UIDocument 오브젝트는 눈 아이콘(씬 뷰 숨김)으로 숨기면 런타임에 영향을 주므로 사용 금지. 대신 UXML에서 `style="display: none;"` 으로 초기 숨김 처리.

## 게임 규칙 / 데이터 모델
- 칸 좌표는 Vector2Int (x=열, y=행), (0,0)은 왼쪽 아래.
- 시작 칸에서 출발, 상하좌우 인접 칸만 이동, 이미 채운 칸 재방문 불가, 막힌 칸 이동 불가.
- 채운 칸 리스트(path) 길이가 전체 칸 수와 같으면 클리어.

## 레벨 진행 로직 (랜덤 셔플)
- **1~10**: 순차 진행 (진입장벽 유지)
- **11-30 / 31-70 / 71-100**: 각 그룹 안에서 미클리어 판 풀에서 랜덤 뽑기
- **매판 재선택 (옵션 B)**: 앱 재실행할 때마다 미클리어 풀에서 다시 랜덤 → 하던 판이 그대로 안 나올 수 있음. 리롤 편법(앱 껐다 켜기) 감수함.
- 화면 표시는 실제 레벨 번호가 아니라 진행도 "N/100" (levelText·roundCountText 둘 다).
- 구버전 유저 마이그레이션: legacy `currentLevel` → 앞 N개(index 0~N-1) 클리어로 간주.

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

## 다음 할 일 (TODO)
- [ ] Android v1.0.3 실기 재검증 — 스크린 캡처 방지가 처음엔 안 걸림(runOnUiThread + using dispose 이슈). 람다 안 activity 재획득으로 수정 후 재빌드 필요할 수 있음. 최근앱 미리보기가 검게 나오는지 확인.
- [ ] 강제 업데이트 팝업 실기 검증 — Firestore에서 `androidLatestVersion`을 잠깐 v1.0.3보다 높게 바꿔서 팝업 뜨는지 확인, 확인 후 원복
- [ ] iOS v1.0.2 빌드 & App Store 제출 — code_index 저장 로직 + **activeInputHandler=Both 복구** + 랜덤 셔플 + 캡처 방지 + 강제 업데이트 반영
- [ ] iOS 실기 테스트 — 라이트닝 케이블 준비하거나 TestFlight 업로드로 검증
- [ ] **Level 90, 93, 96, 99, 100 재설계** — Level Solver Generator 사용, 8×8 shape 위주 (Diamond/Hexagon/Cross). 각각 정답 1~30 목표로 후보 뽑아 채택. 참조: [[project-level-90-100-redesign]]
- [ ] Level 73, 74 실험 변경 처리 — 요구자와 확인 후 유지할지 원복할지 결정. 실기 플레이해서 어려운지 검증 후 판단 권장
- [ ] Level 91, 92, 94, 95, 97, 98 (타임아웃 = 이미 매우 어려움) 유지. 건들지 말 것

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

