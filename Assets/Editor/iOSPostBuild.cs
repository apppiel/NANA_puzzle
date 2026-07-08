// iOS 빌드 후 Info.plist에 ATT 권한 문구를 자동으로 추가하는 에디터 스크립트.
// 에디터에서만 실행되며 실제 빌드에는 포함되지 않음.
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
using System.IO;

public class iOSPostBuild
{
  [PostProcessBuild(999)]
  public static void OnPostProcessBuild(BuildTarget target, string buildPath)
  {
#if UNITY_IOS
    if (target != BuildTarget.iOS) return;

    string plistPath = Path.Combine(buildPath, "Info.plist");
    var plist = new PlistDocument();
    plist.ReadFromFile(plistPath);

    // ATT 권한 팝업에 표시될 문구 (앱 심사 시 Apple이 확인함)
    plist.root.SetString(
      "NSUserTrackingUsageDescription",
      "맞춤형 광고를 제공하기 위해 광고 식별자를 사용합니다."
    );

    plist.WriteToFile(plistPath);
#endif
  }
}
