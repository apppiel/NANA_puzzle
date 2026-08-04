// iOS 빌드 후 Info.plist에 ATT 권한 문구 추가 + AppTrackingTransparency 프레임워크 링크
// + Unity Ads 미디에이션용 SKAdNetwork ID 병합.
// 에디터에서만 실행되며 실제 빌드에는 포함되지 않음.
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
using System.Collections.Generic;
#endif
using System.IO;

public class iOSPostBuild
{
#if UNITY_IOS
  // Unity Ads가 iOS14+ SKAdNetwork attribution용으로 요구하는 광고 네트워크 ID 전체 목록.
  // 출처: Unity Ads Dashboard > Monetization > 앱 > (iOS 앱) > "Unity Ads SDK 3.5.1 이상용 SKAdNetwork ID" 전체 목록.
  // GoogleMobileAdsSKAdNetworkItems.xml 에 이미 있는 ID는 아래 병합 로직에서 중복 스킵되므로
  // Unity Ads 요구 목록 전체를 그대로 넣어둔다(SDK 업데이트로 Google XML 내용이 바뀌어도 안전).
  // 목록이 바뀌면 위 경로에서 최신 목록 복사해서 이 배열만 갱신하면 됨.
  static readonly string[] UnityAdsSKAdNetworkIds = new[]
  {
    "av6w8kgt66.skadnetwork",
    "7ug5zh24hu.skadnetwork",
    "3qy4746246.skadnetwork",
    "g6gcrrvk4p.skadnetwork",
    "kbd757ywx3.skadnetwork",
    "5tjdwbrq8w.skadnetwork",
    "p78axxw29g.skadnetwork",
    "uw77j35x4d.skadnetwork",
    "3rd42ekr43.skadnetwork",
    "glqzh8vgby.skadnetwork",
    "3sh42y64q3.skadnetwork",
    "wg4vff78zm.skadnetwork",
    "2u9pt9hc89.skadnetwork",
    "4dzt52r2t5.skadnetwork",
    "zmvfpc5aq8.skadnetwork",
    "tl55sbb4fm.skadnetwork",
    "lr83yxwka7.skadnetwork",
    "v72qych5uu.skadnetwork",
    "yclnxrl5pm.skadnetwork",
    "w9q455wk68.skadnetwork",
    "578prtvx9j.skadnetwork",
    "6yxyv74ff7.skadnetwork",
    "mlmmfzh3r3.skadnetwork",
    "2fnua5tdw4.skadnetwork",
    "prcb7njmu6.skadnetwork",
    "32z4fx6l9h.skadnetwork",
    "9nlqeag3gk.skadnetwork",
    "ppxm28t8ap.skadnetwork",
    "ydx93a7ass.skadnetwork",
    "44jx6755aq.skadnetwork",
    "mqn7fxpca7.skadnetwork",
    "wzmmz9fp6w.skadnetwork",
    "a8cz6cu7e5.skadnetwork",
    "9t245vhmpl.skadnetwork",
    "4w7y6s5ca2.skadnetwork",
    "mj797d8u6f.skadnetwork",
    "vhf287vqwu.skadnetwork",
    "v9wttpbfk9.skadnetwork",
    "5l3tpt7t6e.skadnetwork",
    "feyaarzu9v.skadnetwork",
    "488r3q3dtq.skadnetwork",
    "238da6jt44.skadnetwork",
    "k6y4y55b64.skadnetwork",
    "xga6mpmplv.skadnetwork",
    "f38h382jlk.skadnetwork",
    "x44k69ngh6.skadnetwork",
    "f7s53z58qe.skadnetwork",
    "k674qkevps.skadnetwork",
    "97r2b46745.skadnetwork",
    "e5fvkxwrpn.skadnetwork",
    "22mmun2rn5.skadnetwork",
    "hs6bdukanm.skadnetwork",
    "5lm9lj6jb7.skadnetwork",
    "424m5254lk.skadnetwork",
    "4fzdc2evr5.skadnetwork",
    "4pfyvq9l8r.skadnetwork",
    "n9x2a789qt.skadnetwork",
    "f73kdq92p3.skadnetwork",
    "cstr6suwn9.skadnetwork",
    "9rd848q2bz.skadnetwork",
    "klf5c3l5u5.skadnetwork",
    "mp6xlyr22a.skadnetwork",
    "pwa73g5rt2.skadnetwork",
    "zq492l623r.skadnetwork",
    "m8dbw4sv7c.skadnetwork",
    "a2p9lx4jpn.skadnetwork",
    "5f5u5tfb26.skadnetwork",
    "77y3x8wds4.skadnetwork",
    "t38b2kh725.skadnetwork",
    "v79kvwwj4g.skadnetwork",
    "c6k4g5qg8m.skadnetwork",
    "4468km3ulz.skadnetwork",
    "5a6flpkh64.skadnetwork",
    "s39g8k73mm.skadnetwork",
    "294l99pt4k.skadnetwork",
    "8s468mfl3y.skadnetwork",
  };
#endif

  // order 999로 GoogleMobileAds PListProcessor(기본 order 1)보다 뒤에 실행 → SKAdNetworkItems 배열 append 가능.
  [PostProcessBuild(999)]
  public static void OnPostProcessBuild(BuildTarget target, string buildPath)
  {
#if UNITY_IOS
    if (target != BuildTarget.iOS) return;

    string plistPath = Path.Combine(buildPath, "Info.plist");
    var plist = new PlistDocument();
    plist.ReadFromFile(plistPath);

    // ATT 권한 팝업 문구
    plist.root.SetString(
      "NSUserTrackingUsageDescription",
      "맞춤형 광고를 제공하기 위해 광고 식별자를 사용합니다."
    );

    MergeUnityAdsSKAdNetworkIds(plist);

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

#if UNITY_IOS
  static void MergeUnityAdsSKAdNetworkIds(PlistDocument plist)
  {
    const string arrayKey = "SKAdNetworkItems";
    const string idKey = "SKAdNetworkIdentifier";

    PlistElementArray array;
    PlistElement existingElement;
    if (plist.root.values.TryGetValue(arrayKey, out existingElement))
    {
      array = existingElement.AsArray();
    }
    else
    {
      array = plist.root.CreateArray(arrayKey);
    }

    var alreadyPresent = new HashSet<string>();
    foreach (var elem in array.values)
    {
      PlistElement idElem;
      if (elem.AsDict().values.TryGetValue(idKey, out idElem))
      {
        alreadyPresent.Add(idElem.AsString());
      }
    }

    foreach (var id in UnityAdsSKAdNetworkIds)
    {
      if (!alreadyPresent.Add(id)) continue;
      var dict = array.AddDict();
      dict.SetString(idKey, id);
    }
  }
#endif
}
