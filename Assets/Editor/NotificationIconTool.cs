// 하트 회복 알림용 흰 실루엣 아이콘 자동 생성.
// Android Small Icon 규격: 투명 배경 + 흰 픽셀만 (컬러 픽셀은 무시됨).
using System.IO;
using UnityEditor;
using UnityEngine;

public static class NotificationIconTool
{
  const string SavePath = "Assets/Art/notif_heart_white.png";
  const int Size = 128;

  [MenuItem("Tools/NANA/Notification/Generate White Heart Icon")]
  static void GenerateWhiteHeartIcon()
  {
    var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
    var transparent = new Color(1f, 1f, 1f, 0f);

    for (int py = 0; py < Size; py++)
    {
      for (int px = 0; px < Size; px++)
      {
        // 정규화 좌표. 하트가 화면 대부분을 차지하도록 스케일 조정
        float x = (px - Size / 2f) / (Size / 2.4f);
        float y = -(py - Size * 0.42f) / (Size / 2.4f);
        // 표준 하트 곡선 방정식: 안쪽이면 흰색, 바깥이면 투명
        float eq = Mathf.Pow(x * x + y * y - 1f, 3f) - x * x * y * y * y;
        tex.SetPixel(px, py, eq < 0f ? Color.white : transparent);
      }
    }
    tex.Apply();

    File.WriteAllBytes(SavePath, tex.EncodeToPNG());
    AssetDatabase.ImportAsset(SavePath);

    // Import Setting 자동 세팅: Read/Write Enabled + Alpha Is Transparency
    var importer = AssetImporter.GetAtPath(SavePath) as TextureImporter;
    if (importer != null)
    {
      importer.textureType = TextureImporterType.Default;
      importer.isReadable = true;
      importer.alphaSource = TextureImporterAlphaSource.FromInput;
      importer.alphaIsTransparency = true;
      importer.mipmapEnabled = false;
      importer.SaveAndReimport();
    }

    var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(SavePath);
    EditorGUIUtility.PingObject(loaded);
    Selection.activeObject = loaded;
    Debug.Log($"White heart icon 생성: {SavePath}. 이제 Project Settings > Mobile Notifications > Android > Notification Icons에 등록하세요.");
  }
}
