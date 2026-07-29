using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

// 하트 회복 완료 로컬 알림 담당. 씬에 GameObject 하나 두고 이 스크립트 부착.
// Start()에서 채널 생성 + 권한 요청. 앱 첫 실행 시 자동으로 OS 권한 팝업 뜸.
// LivesSystem이 Instance.ScheduleHeartFull / Cancel을 호출해서 예약 관리.
public class NotificationHelper : MonoBehaviour
{
  public static NotificationHelper Instance { get; private set; }

  // Project Settings > Mobile Notifications > Android > Notification Icons에 등록한 아이콘 ID.
  // 이 필드가 비어 있거나 미등록 이름이면 Android 알림이 안 뜸.
  public string androidSmallIconId = "icon_small";
  public string androidLargeIconId = "";  // 선택. 큰 아이콘

  const string ChannelId = "heart_recovery";
  const int NotificationId = 1001;
  const string Title = "나나박스 퍼즐";
  const string Body = "하트가 가득 찼어요 💝 나나박스 퍼즐을 다시 만나러 오세요!";

  void Awake()
  {
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this;
  }

  IEnumerator Start()
  {
#if UNITY_ANDROID && !UNITY_EDITOR
    // Android 8.0+ 채널. 알림 종류마다 하나씩 생성.
    var channel = new AndroidNotificationChannel
    {
      Id = ChannelId,
      Name = "하트 회복 알림",
      Description = "하트가 가득 찼을 때 알림",
      Importance = Importance.Default,
    };
    AndroidNotificationCenter.RegisterNotificationChannel(channel);

    // Android 13+ POST_NOTIFICATIONS 런타임 권한 요청. 이하 버전은 자동 허용.
    if (AndroidNotificationCenter.UserPermissionToPost != PermissionStatus.Allowed)
    {
      var request = new PermissionRequest();
      while (request.Status == PermissionStatus.RequestPending) yield return null;
    }
#elif UNITY_IOS && !UNITY_EDITOR
    var options = AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound;
    using (var req = new AuthorizationRequest(options, true))
    {
      while (!req.IsFinished) yield return null;
    }
#else
    yield break;
#endif
  }

  // fireAt 시각에 알림 하나 예약. 기존에 예약된 게 있으면 덮어씀.
  public void ScheduleHeartFull(DateTime fireAt)
  {
    Cancel();
    if (fireAt <= DateTime.Now) return;  // 이미 지난 시각이면 skip

#if UNITY_ANDROID && !UNITY_EDITOR
    var notif = new AndroidNotification
    {
      Title = Title,
      Text = Body,
      FireTime = fireAt,
      SmallIcon = androidSmallIconId,  // 등록된 아이콘 ID 필수. 없으면 알림 자체가 표시 안 됨
      LargeIcon = androidLargeIconId,
    };
    AndroidNotificationCenter.SendNotificationWithExplicitID(notif, ChannelId, NotificationId);
#elif UNITY_IOS && !UNITY_EDITOR
    var trigger = new iOSNotificationCalendarTrigger
    {
      Year = fireAt.Year, Month = fireAt.Month, Day = fireAt.Day,
      Hour = fireAt.Hour, Minute = fireAt.Minute, Second = fireAt.Second,
      Repeats = false,
    };
    var notif = new iOSNotification
    {
      Identifier = NotificationId.ToString(),
      Title = Title,
      Body = Body,
      ShowInForeground = false,
      Trigger = trigger,
    };
    iOSNotificationCenter.ScheduleNotification(notif);
#endif
  }

  public void Cancel()
  {
#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidNotificationCenter.CancelScheduledNotification(NotificationId);
    AndroidNotificationCenter.CancelDisplayedNotification(NotificationId);
#elif UNITY_IOS && !UNITY_EDITOR
    iOSNotificationCenter.RemoveScheduledNotification(NotificationId.ToString());
    iOSNotificationCenter.RemoveDeliveredNotification(NotificationId.ToString());
#endif
  }
}
