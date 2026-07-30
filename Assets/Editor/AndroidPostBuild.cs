// Android Gradle 프로젝트 생성 후 매니페스트를 수정해 android:allowBackup="false"를 강제.
// 이유: Google Auto Backup이 활성화되어 있으면 앱 재설치 시 PlayerPrefs가 복원되어
// "리셋 후 재설치 시 진행 상태가 되살아나는" 버그 재현 조건이 됨. 원천 차단.
// 에디터에서만 실행되며 실제 빌드에는 포함되지 않음.
#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

public class AndroidPostBuild : IPostGenerateGradleAndroidProject
{
  public int callbackOrder => 0;

  public void OnPostGenerateGradleAndroidProject(string projectPath)
  {
    // Unity가 export한 launcher 매니페스트 경로. path는 unityLibrary가 아니라 프로젝트 루트.
    string manifestPath = Path.Combine(projectPath, "src/main/AndroidManifest.xml");
    if (!File.Exists(manifestPath))
    {
      // 일부 Unity 버전에서 경로가 다름. launcher/src/main/AndroidManifest.xml 시도.
      manifestPath = Path.Combine(projectPath, "../launcher/src/main/AndroidManifest.xml");
      if (!File.Exists(manifestPath))
      {
        Debug.LogWarning("[AndroidPostBuild] AndroidManifest.xml 못 찾음. path=" + projectPath);
        return;
      }
    }

    var doc = new XmlDocument();
    doc.Load(manifestPath);
    var app = doc.SelectSingleNode("//application");
    if (app == null)
    {
      Debug.LogWarning("[AndroidPostBuild] <application> 노드 없음");
      return;
    }

    const string androidNs = "http://schemas.android.com/apk/res/android";
    var attr = doc.CreateAttribute("android", "allowBackup", androidNs);
    attr.Value = "false";
    app.Attributes.SetNamedItem(attr);

    // tools:replace 추가해서 라이브러리 매니페스트가 true로 오버라이드해도 우리 값이 이김.
    const string toolsNs = "http://schemas.android.com/tools";
    if (doc.DocumentElement.GetAttribute("xmlns:tools") == "")
      doc.DocumentElement.SetAttribute("xmlns:tools", toolsNs);
    var replaceAttr = doc.CreateAttribute("tools", "replace", toolsNs);
    replaceAttr.Value = "android:allowBackup";
    app.Attributes.SetNamedItem(replaceAttr);

    doc.Save(manifestPath);
    Debug.Log("[AndroidPostBuild] android:allowBackup=false 강제 세팅됨: " + manifestPath);
  }
}
#endif
