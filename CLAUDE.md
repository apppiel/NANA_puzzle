# NANA_puzzle

## 프로젝트 개요
한 줄로 모든 칸을 채우는 퍼즐 게임 (single line block fill).
고정된 시작 칸에서 출발해 상하좌우 인접 칸을 한 줄로 이어 모든 칸을 채우면 클리어.
모바일(안드로이드) 출시가 최종 목표.

## 개발 환경
- Unity 6000.0.77f1 LTS, Universal 2D 템플릿
- Active Input Handling: Both (고전 Input 사용, 터치를 마우스 클릭으로 인식)
- 타깃: Android, 세로(Portrait)
- 버전 관리: Git / GitHub
- 에디터: VS Code + Claude Code

## 폴더 구조
- Assets/Scripts/Data  — 데이터 정의 (LevelData)
- Assets/Scripts/Core  — 게임 로직 (BoardRenderer, GameManager, RewardManager)
- Assets/Scripts/UI    — UI (LoadingScreen, SettingsPanel, SettingsManager)
- Assets/Levels        — 레벨 데이터 에셋 (Level_1 등)
- Assets/Art           — 스프라이트/프리팹 (Cell 프리팹)
- Assets/Casual Game Sounds U6 — 효과음 에셋 (DM-CGS-01~50.wav)
- Assets/Firebase      — Firebase SDK (FirebaseApp, Firestore)
- Assets/ExternalDependencyManager — Firebase EDM4U
- Assets/GoogleMobileAds — Google Mobile Ads SDK (AdMob)

## 핵심 파일
- LevelData.cs: ScriptableObject. 필드 = width, height, startCell, blockedCells.
- BoardRenderer.cs: Board 오브젝트에 부착. 레벨을 격자로 그리고, 마우스/터치 입력으로 칸을 채움. 효과음(fillSound, winSound) 재생. 선·닷 색상은 lineColor 필드 하나로 통합 (#ff8a8a).
- GameManager.cs: 레벨 목록과 지금 몇 번째인지, 진행 상황 저장·불러오기(PlayerPrefs), 다음 레벨/다시하기, 레벨 번호 텍스트(LevelText) 표시. 마지막 레벨 클리어 시 RewardManager.ShowReward() 호출.
- RewardManager.cs: 모든 레벨 클리어 시 랜덤 인증코드(예: A3K9-XZ21) 생성. Firebase Firestore에 기기 ID 키로 저장(중복 방지). RewardPanel UI에 코드 표시 및 복사 버튼 제공.
- AdManager.cs: 전면 광고 로드/표시 담당. N레벨마다 광고 표시 (기본 3레벨, 인스펙터 조정 가능). GameManager의 Ad Manager 필드에 연결 필수. useTestAd 체크 해제 시 실제 광고로 전환.
- Level_1.asset: 3x3, startCell (0,0). 첫 테스트 레벨.
- Assets/google-services.json: Firebase 프로젝트 설정 파일. 패키지명 com.nanaBox.NANApuzzle.

## 게임 규칙 / 데이터 모델
- 칸 좌표는 Vector2Int (x=열, y=행), (0,0)은 왼쪽 아래.
- 시작 칸에서 출발, 상하좌우 인접 칸만 이동, 이미 채운 칸 재방문 불가, 막힌 칸 이동 불가.
- 채운 칸 리스트(path) 길이가 전체 칸 수와 같으면 클리어.
- 시작 칸을 다시 누르면 초기화.

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

## 다음 할 일 (TODO)
- [ ] Firestore 보안 규칙 수정 (출시 전 필수 — 현재 테스트 모드, 30일 후 차단됨)
  - Firebase 콘솔 → Firestore → 규칙 탭에서 rewards 컬렉션 쓰기 최초 1회만 허용으로 변경
- [ ] 출시 전 AdManager의 useTestAd 체크 해제 (실제 광고로 전환)
- [ ] (나중에) 절차적 레벨 생성 검토

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