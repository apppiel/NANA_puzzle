// iOS 빌드 후 Info.plist에 ATT 권한 문구 추가 + AppTrackingTransparency 프레임워크 링크.
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

    // Info.plist에 ATT 권한 팝업 문구 추가
    string plistPath = Path.Combine(buildPath, "Info.plist");
    var plist = new PlistDocument();
    plist.ReadFromFile(plistPath);
    plist.root.SetString(
      "NSUserTrackingUsageDescription",
      "맞춤형 광고를 제공하기 위해 광고 식별자를 사용합니다."
    );
    plist.WriteToFile(plistPath);

    // AppTrackingTransparency 프레임워크 링크 (없으면 빌드 시 undefined symbol 오류)
    string projPath = PBXProject.GetPBXProjectPath(buildPath);
    var proj = new PBXProject();
    proj.ReadFromFile(projPath);
    string targetGuid = proj.GetUnityFrameworkTargetGuid();
    proj.AddFrameworkToProject(targetGuid, "AppTrackingTransparency.framework", false);
    proj.WriteToFile(projPath);
#endif
  }
}
