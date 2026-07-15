using UnityEngine;
using UnityEngine.UIElements;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;
using System.Threading.Tasks;

public class RewardManager : MonoBehaviour
{
    public GameManager gameManager;  // 레벨 이동용. 인스펙터에서 연결

    VisualElement root;
    VisualElement overlay;
    Label codeLabel;
    Label statusLabel;
    string currentCode = "";  // CopyCode()에서 클립보드에 넣을 코드값 보관

    FirebaseFirestore db;
    bool firebaseReady = false;

    void Start()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        root.pickingMode = PickingMode.Ignore;  // 패널 닫힌 동안 터치 차단 방지

        overlay    = root.Q<VisualElement>("overlay");
        codeLabel  = root.Q<Label>("code-text");
        statusLabel = root.Q<Label>("status-text");

        root.Q<Button>("copy-button").clicked    += CopyCode;
        root.Q<Button>("keep-button").clicked    += OnKeepLevel;
        root.Q<Button>("restart-button").clicked += OnRestartFromLevel1;

        // Firebase 초기화. 앱 실행 시 자동으로 한 번만 수행됨
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                db = FirebaseFirestore.DefaultInstance;
                firebaseReady = true;
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
        root.pickingMode = PickingMode.Position;
        overlay.style.display = DisplayStyle.Flex;

        // 기기 고유 ID를 문서 키로 사용해 중복 발급 방지
        string deviceId = SystemInfo.deviceUniqueIdentifier;

        if (!firebaseReady)
        {
            // Firebase 연결 안 됐을 때도 코드는 보여줌 (단, 저장 안 됨)
            ShowCode(GenerateCode());
            SetStatus("네트워크 오류 - 코드를 메모해 두세요");
            return;
        }

        SetStatus("코드 생성 중...");

        // 이미 발급된 코드가 있는지 Firestore에서 확인
        db.Collection("rewards").Document(deviceId).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsCompletedSuccessfully && task.Result.Exists)
            {
                // 기존 코드 재사용 (같은 기기로 재클리어 시)
                ShowCode(task.Result.GetValue<string>("code"));
                SetStatus("이미 발급된 코드입니다");
            }
            else
            {
                // 새 코드 생성 후 Firestore에 저장
                string newCode = GenerateCode();
                ShowCode(newCode);

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

                var rewardsTask = db.Collection("rewards").Document(deviceId).SetAsync(data);
                var indexTask   = db.Collection("code_index").Document(newCode).SetAsync(indexData);

                Task.WhenAll(rewardsTask, indexTask).ContinueWithOnMainThread(saveTask =>
                {
                    if (saveTask.IsCompletedSuccessfully)
                        SetStatus("코드가 발급되었습니다!");
                    else
                        SetStatus("저장 실패 - 코드를 메모해 두세요");
                });
            }
        });
    }

    // "현재 레벨 계속하기" 버튼
    public void OnKeepLevel()
    {
        overlay.style.display = DisplayStyle.None;
        root.pickingMode = PickingMode.Ignore;
        if (gameManager != null) gameManager.RestartLevel();
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
