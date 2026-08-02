using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;

public class RewardManager : MonoBehaviour
{
    public GameManager gameManager;  // 레벨 이동용. 인스펙터에서 연결

    // 저장이 확실히 끝난 뒤에만 코드 보여주기 위한 타임아웃 (Firestore 조회+저장 왕복 대기)
    const float SaveTimeoutSeconds = 15f;

    VisualElement root;
    VisualElement overlay;
    Label codeLabel;
    Label statusLabel;
    Button copyButton;
    Button retryButton;
    Button restartButton;
    string currentCode = "";  // CopyCode()에서 클립보드에 넣을 코드값 보관

    FirebaseFirestore db;
    // Firebase 초기화 Task를 필드로 보관해서 ShowReward가 항상 대기할 수 있게 함
    // (기존 firebaseReady 부울 방식은 초기화 완료 전에 ShowReward 호출되면 저장을 스킵해서 rewards 문서가 안 만들어지는 버그가 있었음)
    Task<DependencyStatus> initTask;

    bool isProcessing = false;   // 중복 실행 방지
    Coroutine timeoutCoroutine;  // 응답 지연 감시

    void Start()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        root.pickingMode = PickingMode.Ignore;  // 패널 닫힌 동안 터치 차단 방지

        overlay      = root.Q<VisualElement>("overlay");
        codeLabel    = root.Q<Label>("code-text");
        statusLabel  = root.Q<Label>("status-text");
        copyButton   = root.Q<Button>("copy-button");
        retryButton  = root.Q<Button>("retry-button");
        restartButton= root.Q<Button>("restart-button");

        copyButton.clicked    += CopyCode;
        retryButton.clicked   += OnRetry;
        restartButton.clicked += OnRestartFromLevel1;

        // Firebase 초기화. 앱 실행 시 자동으로 한 번만 수행됨
        initTask = FirebaseApp.CheckAndFixDependenciesAsync();
        initTask.ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                db = FirebaseFirestore.DefaultInstance;
                Debug.Log("Firebase Firestore 준비 완료");
            }
            else
            {
                Debug.LogError("Firebase 초기화 실패: " + task.Result);
            }
        });
    }

    // 모든 레벨 클리어 시 GameManager에서 호출
    public void ShowReward()
    {
        if (isProcessing) return;  // 중복 방지 (재클리어 연타 등)
        isProcessing = true;

        // 팝업 즉시 열되, 코드는 저장 성공한 뒤에만 표시
        root.pickingMode = PickingMode.Position;
        overlay.style.display = DisplayStyle.Flex;

        BeginLoadingUI();

        // 초기화가 아직 안 끝났으면 여기서 대기. 이미 끝났으면 즉시 다음 단계로 이어짐
        initTask.ContinueWithOnMainThread(initT =>
        {
            if (initT.Result != DependencyStatus.Available || db == null)
            {
                FailWithRetry("네트워크 상태를 확인한 뒤 다시 시도해 주세요");
                return;
            }
            FetchOrCreateCode();
        });
    }

    // 저장 로직 본체. 재시도 버튼도 이 메서드를 다시 호출함
    void FetchOrCreateCode()
    {
        StartTimeoutWatchdog();

        // 기기 고유 ID를 문서 키로 사용해 중복 발급 방지
        string deviceId = SystemInfo.deviceUniqueIdentifier;

        db.Collection("rewards").Document(deviceId).GetSnapshotAsync().ContinueWithOnMainThread(getTask =>
        {
            if (getTask.IsFaulted || getTask.IsCanceled)
            {
                Debug.LogError("rewards GetSnapshot 실패: " + getTask.Exception);
                FailWithRetry("코드 확인에 실패했어요. 다시 시도해 주세요");
                return;
            }

            if (getTask.Result.Exists)
            {
                // 기존 코드 재사용 (같은 기기로 재클리어 시). 이 경로는 이미 저장돼있으니 저장 재시도 불필요
                Succeed(getTask.Result.GetValue<string>("code"), "이미 발급된 코드입니다");
                return;
            }

            // 새 코드 생성 후 Firestore에 저장
            string newCode = GenerateCode();

            var data = new Dictionary<string, object>
            {
                { "code",      newCode },
                { "claimed",   false },
                { "deviceId",  deviceId },
                { "createdAt", FieldValue.ServerTimestamp }
            };

            // rewards: 중복 발급 방지용(기기ID 키)
            // code_index: 웹에서 코드 검증용(코드 키). 웹은 존재 여부만 확인하지만
            //   나중 CS 대응 위해 deviceId·createdAt 함께 저장
            var indexData = new Dictionary<string, object>
            {
                { "deviceId",  deviceId },
                { "createdAt", FieldValue.ServerTimestamp }
            };

            // WriteBatch로 원자적 커밋. 둘 다 성공하거나 둘 다 실패 — 부분 실패 상태가 원천 봉쇄됨.
            // (이전엔 Task.WhenAll로 따로 SetAsync 후 대기했는데, rewards만 저장되고 code_index가 실패하면
            //  유저는 코드 받는데 웹 검증은 실패하는 유령 코드가 발생했었음)
            var batch = db.StartBatch();
            batch.Set(db.Collection("rewards").Document(deviceId), data);
            batch.Set(db.Collection("code_index").Document(newCode), indexData);

            batch.CommitAsync().ContinueWithOnMainThread(saveTask =>
            {
                if (saveTask.IsCompletedSuccessfully)
                {
                    Succeed(newCode, "코드가 발급되었습니다!");
                }
                else
                {
                    // 실패 원인 진단용 상세 로그. 지금까진 이걸 삼켜서 원인 파악이 안 됐음
                    Debug.LogError("코드 저장 실패: " + saveTask.Exception);
                    FailWithRetry("저장에 실패했어요. 다시 시도해 주세요");
                }
            });
        });
    }

    // "다시 시도" 버튼
    void OnRetry()
    {
        if (isProcessing) return;
        isProcessing = true;
        BeginLoadingUI();
        // 초기화가 실패한 상태에서 [다시 시도] 눌린 케이스도 커버
        initTask.ContinueWithOnMainThread(initT =>
        {
            if (initT.Result != DependencyStatus.Available || db == null)
            {
                FailWithRetry("네트워크 상태를 확인한 뒤 다시 시도해 주세요");
                return;
            }
            FetchOrCreateCode();
        });
    }

    void BeginLoadingUI()
    {
        currentCode = "";
        if (codeLabel != null) codeLabel.text = "- - - - -";
        SetStatus("코드 생성 중...");
        retryButton.style.display = DisplayStyle.None;
        // 저장 완료 전엔 유저가 팝업 못 넘기게 잠금 (코드 못 본 채 넘어가면 위험)
        copyButton.SetEnabled(false);
        restartButton.SetEnabled(false);
    }

    void Succeed(string code, string message)
    {
        CancelTimeoutWatchdog();
        ShowCode(code);
        SetStatus(message);
        retryButton.style.display = DisplayStyle.None;
        copyButton.SetEnabled(true);
        restartButton.SetEnabled(true);
        isProcessing = false;
    }

    void FailWithRetry(string message)
    {
        CancelTimeoutWatchdog();
        if (codeLabel != null) codeLabel.text = "- - - - -";
        SetStatus(message);
        retryButton.style.display = DisplayStyle.Flex;
        // 실패 상태에선 코드 없으니 복사는 여전히 잠금. 유저가 나갈 수 있게 restart는 열어둠
        copyButton.SetEnabled(false);
        restartButton.SetEnabled(true);
        isProcessing = false;
    }

    void StartTimeoutWatchdog()
    {
        CancelTimeoutWatchdog();
        timeoutCoroutine = StartCoroutine(TimeoutRoutine());
    }

    void CancelTimeoutWatchdog()
    {
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }
    }

    IEnumerator TimeoutRoutine()
    {
        yield return new WaitForSeconds(SaveTimeoutSeconds);
        // 이 코루틴이 살아있는 채로 여기 도달했다 = Succeed/FailWithRetry가 아직 안 불렸다
        timeoutCoroutine = null;
        FailWithRetry("응답이 지연되고 있어요. 다시 시도해 주세요");
    }

    // "1레벨로 돌아가기" 버튼
    public void OnRestartFromLevel1()
    {
        overlay.style.display = DisplayStyle.None;
        root.pickingMode = PickingMode.Ignore;
        if (gameManager != null) gameManager.GoToLevel1();
    }

    // "코드 복사하기" 버튼
    public void CopyCode()
    {
        if (string.IsNullOrEmpty(currentCode)) return;
        GUIUtility.systemCopyBuffer = currentCode;
        SetStatus("클립보드에 복사되었습니다!", green: true);
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

    void ShowCode(string code)
    {
        currentCode = code;
        if (codeLabel != null) codeLabel.text = code;
    }

    void SetStatus(string msg, bool green = false)
    {
        if (statusLabel == null) return;
        statusLabel.text = msg;
        // green=true면 status-copied 클래스 추가(초록), 아니면 제거(기본 회색)
        if (green)
            statusLabel.AddToClassList("status-copied");
        else
            statusLabel.RemoveFromClassList("status-copied");
    }
}
