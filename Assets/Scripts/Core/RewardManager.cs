using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;

public class RewardManager : MonoBehaviour
{
    [Header("UI 연결 (인스펙터에서 연결)")]
    public GameObject rewardPanel;  // 코드 표시 패널 오브젝트
    public TMP_Text codeText;       // 인증코드 텍스트
    public TMP_Text statusText;     // 상태 메시지 텍스트 ("저장 중...", "발급 완료" 등)
    public Button copyButton;       // 코드 복사 버튼

    FirebaseFirestore db;
    bool firebaseReady = false;

    void Start()
    {
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
        if (rewardPanel != null) rewardPanel.SetActive(true);

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

                db.Collection("rewards").Document(deviceId).SetAsync(data).ContinueWithOnMainThread(saveTask =>
                {
                    if (saveTask.IsCompletedSuccessfully)
                        SetStatus("코드가 발급되었습니다!");
                    else
                        SetStatus("저장 실패 - 코드를 메모해 두세요");
                });
            }
        });
    }

    // 복사 버튼 OnClick에 연결
    public void CopyCode()
    {
        if (codeText == null) return;
        GUIUtility.systemCopyBuffer = codeText.text;
        SetStatus("클립보드에 복사되었습니다!");
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
        if (codeText != null) codeText.text = code;
    }

    void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }
}
