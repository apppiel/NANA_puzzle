using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;

// 인증코드 발급 + Firestore 저장 + 설정창에서 재열람 지원.
//
// 설계 원칙 (2026-08-03 재작성):
//  1) 어떤 경우에도 유저는 코드를 받아야 한다. 초기화/저장 실패해도 로컬에서 발급해 표시.
//  2) 로컬 저장(PlayerPrefs)이 진짜 소스. Firestore는 웹 검증용 사본.
//  3) 서버 미동기화 상태면 앱 실행 때마다 백그라운드로 자동 재시도.
//  4) 팝업은 절대 락다운되지 않는다. restart 버튼 항상 활성화, 모든 대기에 타임아웃.
//
// 이전 버전(v1.0.7)의 결함:
//  - FirebaseApp.CheckAndFixDependenciesAsync()가 특정 유저 환경에서 hang → 콜백 안 옴
//  - initTask.Result 접근 시 예외 → ContinueWithOnMainThread에서 조용히 삼킴 (IsFaulted 미체크)
//  - restart 버튼도 저장 완료 전엔 잠금 → 유저 탈출 불가 락다운
//  - 저장 실패 시 코드 표시 안 함 → 유저가 코드조차 못 봄
public class RewardManager : MonoBehaviour
{
    public GameManager gameManager;  // 레벨 이동용. 인스펙터에서 연결

#if UNITY_EDITOR
    [Header("에디터 전용 (빌드에는 영향 없음)")]
    [Tooltip("체크하면 Firebase 초기화 실패로 강제 처리 → 로컬 fallback 흐름 검증")]
    public bool editorSimulateInitFail;
    [Tooltip("체크하면 Firestore 저장 실패로 강제 처리 → 미동기화 안내/재시도 검증")]
    public bool editorSimulateSaveFail;
#endif

    // 로컬 저장 키 (PlayerPrefs)
    const string LocalCodeKey       = "rewardCode";       // 발급된 코드 문자열
    const string LocalCodeSyncedKey = "rewardCodeSynced"; // 서버 저장 성공 여부 (0/1)

    // 대기 타임아웃 (Firebase가 hang 걸리는 환경 대응)
    const float InitTimeoutSeconds  = 12f;   // 초기화 완료 대기
    const float QueryTimeoutSeconds = 10f;   // Firestore 조회
    const float SaveTimeoutSeconds  = 10f;   // Firestore 저장

    VisualElement root;
    VisualElement overlay;
    Label codeLabel;
    Label statusLabel;
    Button copyButton;
    Button retryButton;
    Button restartButton;
    string currentCode = "";

    FirebaseFirestore db;
    Task<DependencyStatus> initTask;  // Firebase 초기화 Task (필드로 보관해 상태 폴링)

    bool isProcessing = false;         // 팝업 흐름 중복 진입 방지
    Coroutine activeCoroutine;         // 진행 중인 발급/재시도 코루틴 (Show 재호출 시 취소)

    void Start()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        root.pickingMode = PickingMode.Ignore;

        overlay      = root.Q<VisualElement>("overlay");
        codeLabel    = root.Q<Label>("code-text");
        statusLabel  = root.Q<Label>("status-text");
        copyButton   = root.Q<Button>("copy-button");
        retryButton  = root.Q<Button>("retry-button");
        restartButton= root.Q<Button>("restart-button");

        copyButton.clicked    += CopyCode;
        retryButton.clicked   += OnRetry;
        restartButton.clicked += OnRestartFromLevel1;

        // Firebase 초기화. 다른 스크립트(UpdateChecker 등)가 먼저 CheckAndFixDependenciesAsync를
        // 호출 중이면 InvalidOperationException("Don't call other Firebase functions while
        // CheckDependencies is running.") 던짐. 이 경우 코루틴에서 짧게 대기하며 재시도해
        // 다른 초기화가 끝난 뒤 성공하도록 유도. 실기에서 관찰된 초기화 hang 원인이 이거였음(2026-08-03).
        StartCoroutine(InitializeFirebaseCoroutine());

        // 로컬엔 코드 있는데 서버 저장 안 된 상태면 앱 실행 시 조용히 재시도.
        // 유저 개입 없이 자동 복구되는 게 목표. 실패해도 사용자 UI 영향 없음.
        if (!string.IsNullOrEmpty(GetLocalCode()) && !IsLocalCodeSynced())
        {
            StartCoroutine(StartupBackgroundSync());
        }
        // v1.0.7 이하 유저 마이그레이션: 로컬엔 없지만 Firestore엔 이미 발급된 코드가 있을 수 있음.
        // (v1.0.7 RewardManager는 PlayerPrefs 저장 없이 Firestore만 썼음.)
        // 이 조회를 미리 해두면 ShowReward 호출 시 로컬 즉시 표시 분기를 탈 수 있어 22초 대기 회피.
        else if (string.IsNullOrEmpty(GetLocalCode()))
        {
            StartCoroutine(MigrateFromServerCoroutine());
        }
    }

    // Firebase 초기화 재시도 코루틴. UpdateChecker 등 다른 스크립트가 먼저 초기화 중이면
    // InvalidOperationException이 나므로, 짧게 대기하며 최대 30번(=30초) 재시도.
    // 성공하면 initTask 세팅 + ContinueWithOnMainThread로 db 준비.
    // 최종 실패해도 IssueFlow가 IsFirebaseUsable() 체크 후 FallbackIssueLocal로 안전 종료.
    IEnumerator InitializeFirebaseCoroutine()
    {
        // C# 제약: yield는 catch 블록에 못 들어감. 상태 플래그로 우회.
        bool fatal = false;
        for (int attempt = 1; attempt <= 30 && initTask == null && !fatal; attempt++)
        {
            try
            {
                initTask = FirebaseApp.CheckAndFixDependenciesAsync();
                Debug.Log("Firebase CheckAndFix 시작 성공 (attempt " + attempt + ")");
            }
            catch (System.InvalidOperationException)
            {
                // 다른 스크립트가 아직 초기화 중. 1초 후 재시도.
            }
            catch (System.Exception e)
            {
                Debug.LogError("Firebase 초기화 호출 예외: " + e);
                fatal = true;
            }
            if (initTask == null && !fatal)
                yield return new WaitForSecondsRealtime(1f);
        }

        if (initTask == null)
        {
            Debug.LogError("Firebase 초기화 재시도 실패 — 이후 흐름은 로컬 fallback으로 대체됨");
            yield break;
        }

        initTask.ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("Firebase 초기화 예외: " + task.Exception);
                return;
            }
            if (task.Result != DependencyStatus.Available)
            {
                Debug.LogError("Firebase 초기화 실패: " + task.Result);
                return;
            }
            db = FirebaseFirestore.DefaultInstance;
            Debug.Log("Firebase Firestore 준비 완료");
        });
    }

    // ─── Public API ─────────────────────────────────────────────

    // 로컬에 저장된 코드 (없으면 빈 문자열). 설정창에서 100판 완주자 여부 판단에도 사용 가능.
    public string GetLocalCode() => PlayerPrefs.GetString(LocalCodeKey, "");

    // 서버 저장 성공 여부. false면 웹 검증에 아직 사용 불가.
    public bool IsLocalCodeSynced() => PlayerPrefs.GetInt(LocalCodeSyncedKey, 0) == 1;

    // 100판 완주 시 (GameManager) 또는 설정창 "인증코드 보기" 눌렀을 때 호출.
    // 로컬 코드 유무·동기화 상태에 따라 자동 분기.
    public void ShowReward()
    {
        // 이미 팝업 열려 있고 처리 중이면 재입장 무시. 다만 팝업이 닫힌 상태에서 재호출은 허용.
        if (isProcessing && overlay.style.display == DisplayStyle.Flex) return;

        isProcessing = true;
        root.pickingMode = PickingMode.Position;
        overlay.style.display = DisplayStyle.Flex;

        BeginLoadingUI();

        string localCode = GetLocalCode();
        if (!string.IsNullOrEmpty(localCode))
        {
            // 로컬에 이미 있음 → 즉시 표시 (락다운 없음)
            DisplayCode(localCode);
            EnableCopy();
            if (IsLocalCodeSynced())
            {
                SetStatus("발급된 코드입니다");
                isProcessing = false;
            }
            else
            {
                // 서버 저장 아직 안 됨 → 백그라운드로 조용히 재시도. UI는 이미 코드 보여주고 있음.
                SetStatus("서버 저장 확인 중...");
                activeCoroutine = StartCoroutine(SyncExistingCodeCoroutine(localCode));
            }
            return;
        }

        // 로컬에 없음 → 신규 발급 흐름 (Firestore 조회 + 저장, 실패 시 fallback)
        activeCoroutine = StartCoroutine(IssueFlowCoroutine());
    }

    // ─── 발급/저장 코루틴 ────────────────────────────────────────

    // 신규 발급 흐름. Firestore 조회 → 있으면 재사용, 없으면 생성 + 저장.
    // 어느 단계든 실패하면 로컬 발급으로 폴백해 코드는 반드시 보여줌.
    IEnumerator IssueFlowCoroutine()
    {
        // 초기화 완료 대기 (자체 타임아웃 있음, Firebase Task가 hang 걸려도 여기서 탈출)
        yield return WaitForInitReady(InitTimeoutSeconds);

        if (!IsFirebaseUsable())
        {
            // 초기화 실패/타임아웃 → 로컬 fallback. 코드는 보여주고 나중에 자동 재시도.
            FallbackIssueLocal("서버 연결 실패 - 코드를 꼭 스크린샷 해두세요");
            yield break;
        }

        string deviceId = SystemInfo.deviceUniqueIdentifier;

        // 이미 발급된 코드 조회
        var getTask = db.Collection("rewards").Document(deviceId).GetSnapshotAsync();
        yield return WaitForTaskOrTimeout(getTask, QueryTimeoutSeconds);

        if (!getTask.IsCompleted || getTask.IsFaulted || getTask.IsCanceled)
        {
            Debug.LogError("rewards 조회 실패: " + (getTask.Exception?.ToString() ?? "타임아웃"));
            FallbackIssueLocal("서버 응답 없음 - 코드를 꼭 스크린샷 해두세요");
            yield break;
        }

        if (getTask.Result.Exists)
        {
            // 서버에 이미 있음 → 그거 로컬에도 저장하고 표시
            string existingCode = getTask.Result.GetValue<string>("code");
            SaveLocalCode(existingCode, synced: true);
            DisplayCode(existingCode);
            EnableCopy();
            SetStatus("이미 발급된 코드입니다");
            isProcessing = false;
            activeCoroutine = null;
            yield break;
        }

        // 새 코드 생성. 로컬 먼저 저장(synced=false) 후 서버 저장 시도.
        // 순서 중요: 서버 저장 도중 앱 종료돼도 로컬엔 남아있어 다음 실행 시 백그라운드 재시도됨.
        string newCode = GenerateCode();
        SaveLocalCode(newCode, synced: false);
        DisplayCode(newCode);
        EnableCopy();
        SetStatus("서버에 저장 중...");

        yield return SaveToServerCoroutine(newCode, deviceId, isForeground: true);
    }

    // 서버에 코드 저장. rewards + code_index 원자적 커밋. 실패해도 로컬은 유지.
    // isForeground=true면 UI 상태 갱신, false면 백그라운드 조용히 수행.
    IEnumerator SaveToServerCoroutine(string code, string deviceId, bool isForeground)
    {
        var data = new Dictionary<string, object>
        {
            { "code",      code },
            { "claimed",   false },
            { "deviceId",  deviceId },
            { "createdAt", FieldValue.ServerTimestamp }
        };
        var indexData = new Dictionary<string, object>
        {
            { "deviceId",  deviceId },
            { "createdAt", FieldValue.ServerTimestamp }
        };

        // WriteBatch로 원자적 커밋. rewards만 저장되고 code_index가 실패하면
        // 웹 검증에서 유효하지 않은 유령 코드가 만들어짐 → 둘 다 성공 or 둘 다 실패로 통일.
        var batch = db.StartBatch();
        batch.Set(db.Collection("rewards").Document(deviceId), data);
        batch.Set(db.Collection("code_index").Document(code), indexData);
        var saveTask = batch.CommitAsync();

        yield return WaitForTaskOrTimeout(saveTask, SaveTimeoutSeconds);

        bool ok = saveTask.IsCompleted && !saveTask.IsFaulted && !saveTask.IsCanceled;
#if UNITY_EDITOR
        if (editorSimulateSaveFail) ok = false;
#endif
        if (ok)
        {
            SaveLocalCode(code, synced: true);
            if (isForeground)
            {
                SetStatus("코드가 발급되었습니다!");
                isProcessing = false;
                activeCoroutine = null;
            }
        }
        else
        {
            Debug.LogError("코드 저장 실패: " + (saveTask.Exception?.ToString() ?? "타임아웃"));
            // 로컬엔 이미 저장돼 있음(synced=false). 다음 실행에서 자동 재시도 or 유저가 [다시 시도] 클릭.
            if (isForeground)
            {
                SetStatus("서버 저장 실패 - 코드를 꼭 스크린샷 해두세요");
                retryButton.style.display = DisplayStyle.Flex;
                isProcessing = false;
                activeCoroutine = null;
            }
        }
    }

    // 이미 로컬에 있는 미동기화 코드를 서버 저장 시도 (팝업 표시 상태에서).
    // 조회는 스킵 — 이미 로컬에서 발급했으므로 해당 기기의 소유 코드가 확정된 상태.
    IEnumerator SyncExistingCodeCoroutine(string code)
    {
        yield return WaitForInitReady(InitTimeoutSeconds);

        if (!IsFirebaseUsable())
        {
            SetStatus("서버 연결 실패 - 코드를 꼭 스크린샷 해두세요");
            retryButton.style.display = DisplayStyle.Flex;
            isProcessing = false;
            activeCoroutine = null;
            yield break;
        }

        yield return SaveToServerCoroutine(code, SystemInfo.deviceUniqueIdentifier, isForeground: true);
    }

    // v1.0.7 이하 유저용 마이그레이션. 로컬엔 없지만 서버엔 이미 코드가 있을 수 있어서
    // 앱 시작 시 조용히 조회해서 로컬로 복사. 발견 못 하면(신규 유저 등) 조용히 종료.
    // 성공하면 다음 ShowReward가 로컬 즉시 표시 분기를 탐 → 22초 대기 회피.
    IEnumerator MigrateFromServerCoroutine()
    {
        yield return WaitForInitReady(InitTimeoutSeconds);
        if (!IsFirebaseUsable()) yield break;

        // 중간에 ShowReward가 IssueFlow 통해 로컬 저장했으면 이미 목적 달성 — 중복 조회 스킵
        if (!string.IsNullOrEmpty(GetLocalCode())) yield break;

        var getTask = db.Collection("rewards").Document(SystemInfo.deviceUniqueIdentifier).GetSnapshotAsync();
        yield return WaitForTaskOrTimeout(getTask, QueryTimeoutSeconds);

        if (!getTask.IsCompleted || getTask.IsFaulted || getTask.IsCanceled) yield break;
        if (!getTask.Result.Exists) yield break;

        // 다시 한 번 로컬 체크. 조회 중 ShowReward가 먼저 발급했을 수 있음.
        if (!string.IsNullOrEmpty(GetLocalCode())) yield break;

        string existingCode = getTask.Result.GetValue<string>("code");
        if (string.IsNullOrEmpty(existingCode)) yield break;

        SaveLocalCode(existingCode, synced: true);
        Debug.Log("v1.0.7 코드 로컬 마이그레이션 완료");
    }

    // 앱 시작 시 조용히 도는 백그라운드 동기화. 유저 UI 영향 없음.
    // 로컬엔 있는데 서버엔 없는 상태를 자연 복구하기 위함.
    IEnumerator StartupBackgroundSync()
    {
        yield return WaitForInitReady(InitTimeoutSeconds);
        if (!IsFirebaseUsable()) yield break;

        string code = GetLocalCode();
        if (string.IsNullOrEmpty(code) || IsLocalCodeSynced()) yield break;

        yield return SaveToServerCoroutine(code, SystemInfo.deviceUniqueIdentifier, isForeground: false);
    }

    // Task 완료 or 타임아웃 중 먼저 도달하는 쪽까지 대기.
    // Firebase Task가 hang 걸릴 수 있어서 자체 타임아웃 필수. Task.WhenAny+Task.Delay 대신
    // 코루틴 폴링으로 MainThread 컨텍스트 유지 (Unity API 접근 안전).
    IEnumerator WaitForTaskOrTimeout(Task task, float timeoutSec)
    {
        // task null 방어. Start()가 예외로 중단돼 initTask가 잡히지 않은 케이스가 실기에서 재현됨.
        // 여기서 조용히 break하면 이후 IsFirebaseUsable()이 false 반환 → FallbackIssueLocal 발동.
        if (task == null) yield break;
        float elapsed = 0f;
        while (!task.IsCompleted && elapsed < timeoutSec)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // initTask 전용 대기. InitializeFirebaseCoroutine이 재시도 중이라 initTask 자체가 늦게 할당될 수 있음.
    // 필드를 매 프레임 재평가하며 (할당 + 완료) 두 조건 모두 기다림.
    IEnumerator WaitForInitReady(float timeoutSec)
    {
        float elapsed = 0f;
        while ((initTask == null || !initTask.IsCompleted) && elapsed < timeoutSec)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // Firebase가 사용 가능한 상태인지 최종 체크. IsCompletedSuccessfully만 봐선 부족 —
    // Result 접근이 AggregateException 던질 수 있어서 IsFaulted도 명시 체크.
    bool IsFirebaseUsable()
    {
#if UNITY_EDITOR
        if (editorSimulateInitFail) return false;
#endif
        if (initTask == null || !initTask.IsCompleted) return false;
        if (initTask.IsFaulted || initTask.IsCanceled) return false;
        if (initTask.Result != DependencyStatus.Available) return false;
        return db != null;
    }

    // ─── Fallback / 발급 유틸 ─────────────────────────────────

    // 서버 없이 로컬만으로 발급. 유저는 코드를 어쨌든 받고, 서버 저장은 나중에 자동 재시도.
    void FallbackIssueLocal(string message)
    {
        string newCode = GenerateCode();
        SaveLocalCode(newCode, synced: false);
        DisplayCode(newCode);
        EnableCopy();
        SetStatus(message);
        retryButton.style.display = DisplayStyle.Flex;
        isProcessing = false;
        activeCoroutine = null;
    }

    void SaveLocalCode(string code, bool synced)
    {
        PlayerPrefs.SetString(LocalCodeKey, code);
        PlayerPrefs.SetInt(LocalCodeSyncedKey, synced ? 1 : 0);
        PlayerPrefs.Save();
    }

    // 예: A3K9-XZ21 형식. 헷갈리는 문자(0,O,1,I) 제외
    string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng = new System.Random();
        char[] code = new char[9];
        for (int i = 0; i < 4; i++) code[i] = chars[rng.Next(chars.Length)];
        code[4] = '-';
        for (int i = 5; i < 9; i++) code[i] = chars[rng.Next(chars.Length)];
        return new string(code);
    }

    // ─── UI 상태 헬퍼 ─────────────────────────────────────────

    // 팝업 열자마자 표시할 초기 상태. restart는 항상 활성 유지 (락다운 방지).
    void BeginLoadingUI()
    {
        currentCode = "";
        if (codeLabel != null) codeLabel.text = "- - - - -";
        SetStatus("코드 확인 중...");
        retryButton.style.display = DisplayStyle.None;
        copyButton.SetEnabled(false);
        restartButton.SetEnabled(true);   // ★ 락다운 방지: 이 버튼은 어떤 상태에서도 눌러서 나갈 수 있어야 함
    }

    void DisplayCode(string code)
    {
        currentCode = code;
        if (codeLabel != null) codeLabel.text = code;
    }

    void EnableCopy()
    {
        copyButton.SetEnabled(true);
    }

    void SetStatus(string msg, bool green = false)
    {
        if (statusLabel == null) return;
        statusLabel.text = msg;
        if (green) statusLabel.AddToClassList("status-copied");
        else       statusLabel.RemoveFromClassList("status-copied");
    }

    // ─── 버튼 핸들러 ──────────────────────────────────────────

    // [다시 시도] 버튼. 로컬 코드가 있으면 저장만 재시도, 없으면 발급부터 재시도.
    void OnRetry()
    {
        if (isProcessing) return;
        isProcessing = true;
        retryButton.style.display = DisplayStyle.None;

        string localCode = GetLocalCode();
        if (!string.IsNullOrEmpty(localCode))
        {
            SetStatus("서버 저장 다시 시도 중...");
            activeCoroutine = StartCoroutine(SyncExistingCodeCoroutine(localCode));
        }
        else
        {
            SetStatus("코드 확인 중...");
            activeCoroutine = StartCoroutine(IssueFlowCoroutine());
        }
    }

    // [1레벨로 돌아가기] 버튼. 언제든 눌러서 팝업 탈출 가능 (락다운 방지 안전망).
    public void OnRestartFromLevel1()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }
        isProcessing = false;
        overlay.style.display = DisplayStyle.None;
        root.pickingMode = PickingMode.Ignore;
        if (gameManager != null) gameManager.GoToLevel1();
    }

    // [코드 복사하기] 버튼
    public void CopyCode()
    {
        if (string.IsNullOrEmpty(currentCode)) return;
        GUIUtility.systemCopyBuffer = currentCode;
        SetStatus("클립보드에 복사되었습니다!", green: true);
    }

#if UNITY_EDITOR
    // 에디터 전용: 로컬에 저장된 코드/동기화 상태를 초기화.
    // Firebase 콘솔에서 rewards/code_index 문서 삭제한 뒤 이 메뉴 실행하면 신규 발급 흐름 재현 가능.
    // 인스펙터에서 RewardManager 컴포넌트 우클릭 → 메뉴로 실행.
    [ContextMenu("[TEST] Clear Local Reward Code")]
    void TestClearLocalRewardCode()
    {
        PlayerPrefs.DeleteKey(LocalCodeKey);
        PlayerPrefs.DeleteKey(LocalCodeSyncedKey);
        PlayerPrefs.Save();
        Debug.Log("로컬 리워드 코드 삭제됨. 다음 ShowReward 호출 시 서버 조회 → 신규 발급 흐름 진행.");
    }
#endif
}
