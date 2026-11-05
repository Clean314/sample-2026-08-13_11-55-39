using System;
using System.IO;
using UnityEngine;

/// <summary>
/// PNG 를 기기 갤러리(사진 앱)에 저장한다. 플러그인 없이 안드로이드 API 를 직접 부른다.
///
///  - 안드로이드 10(API 29) 이상: MediaStore 에 넣는다. 권한이 필요 없다. Pictures/MATBLAST 에 들어간다.
///  - 안드로이드 8~9(API 26~28): 저장 권한(WRITE_EXTERNAL_STORAGE)을 물어본 뒤 Pictures/MATBLAST
///    에 파일을 쓰고 미디어 스캔을 걸어 갤러리에 보이게 한다. 권한은
///    Plugins/Android/MatblastPermissions.androidlib 의 매니페스트가 API 28 까지만 선언한다.
///  - 에디터: 프로젝트 폴더의 ShareCards/ 에 쓴다.
///
/// done 은 메인 스레드에서 한 번 불린다. (성공 여부, 실패 이유 키 — 성공이면 null)
/// </summary>
public static class ImageSaver
{
    const string ALBUM = "MATBLAST";

    public const string ERR_DENIED = "share_denied";
    public const string ERR_FAILED = "share_failed";

    public static void SavePng(byte[] png, string fileName, Action<bool, string> done)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        int sdk = SdkInt();
        if (sdk >= 29) { done(SaveMediaStore(png, fileName), ERR_FAILED); return; }

        const string perm = "android.permission.WRITE_EXTERNAL_STORAGE";
        if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(perm))
        {
            done(SaveLegacy(png, fileName), ERR_FAILED);
            return;
        }
        var cb = new UnityEngine.Android.PermissionCallbacks();
        cb.PermissionGranted += _ => done(SaveLegacy(png, fileName), ERR_FAILED);
        cb.PermissionDenied  += _ => done(false, ERR_DENIED);
#pragma warning disable CS0618   // 안드로이드 10 이하에서만 오는 길이라 새 콜백 이름이 없어도 된다
        cb.PermissionDeniedAndDontAskAgain += _ => done(false, ERR_DENIED);
#pragma warning restore CS0618
        UnityEngine.Android.Permission.RequestUserPermission(perm, cb);
#else
        try
        {
            string dir = Path.Combine(Application.dataPath, "..", "ShareCards");
            Directory.CreateDirectory(dir);
            string path = Path.GetFullPath(Path.Combine(dir, fileName));
            File.WriteAllBytes(path, png);
            Debug.Log($"[Share] 카드 저장: {path}");
            done(true, null);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Share] 저장 실패: {e.Message}");
            done(false, ERR_FAILED);
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    static int SdkInt()
    {
        using (var v = new AndroidJavaClass("android.os.Build$VERSION"))
            return v.GetStatic<int>("SDK_INT");
    }

    static AndroidJavaObject Activity()
    {
        using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            return up.GetStatic<AndroidJavaObject>("currentActivity");
    }

    // API 29+. 권한 없이 공용 사진 폴더에 넣는 유일한 길이다.
    static bool SaveMediaStore(byte[] png, string fileName)
    {
        try
        {
            using (var act      = Activity())
            using (var resolver = act.Call<AndroidJavaObject>("getContentResolver"))
            using (var values   = new AndroidJavaObject("android.content.ContentValues"))
            using (var media    = new AndroidJavaClass("android.provider.MediaStore$Images$Media"))
            using (var table    = media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
            {
                values.Call("put", "_display_name", fileName);
                values.Call("put", "mime_type", "image/png");
                values.Call("put", "relative_path", "Pictures/" + ALBUM);

                using (var item = resolver.Call<AndroidJavaObject>("insert", table, values))
                {
                    if (item == null) return false;
                    using (var os = resolver.Call<AndroidJavaObject>("openOutputStream", item))
                    {
                        if (os == null) return false;
                        os.Call("write", (sbyte[])(Array)png);
                        os.Call("close");
                    }
                }
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Share] MediaStore 저장 실패: {e.Message}");
            return false;
        }
    }

    // API 26~28. 권한을 받은 뒤 공용 Pictures 폴더에 파일을 쓰고, 갤러리가 알아채도록 스캔을 건다.
    static bool SaveLegacy(byte[] png, string fileName)
    {
        try
        {
            string pictures;
            using (var env = new AndroidJavaClass("android.os.Environment"))
            {
                string dirName = env.GetStatic<string>("DIRECTORY_PICTURES");
                using (var dir = env.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", dirName))
                    pictures = dir.Call<string>("getAbsolutePath");
            }
            string folder = Path.Combine(pictures, ALBUM);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, fileName);
            File.WriteAllBytes(path, png);

            using (var act    = Activity())
            using (var file   = new AndroidJavaObject("java.io.File", path))
            using (var uriCls = new AndroidJavaClass("android.net.Uri"))
            using (var uri    = uriCls.CallStatic<AndroidJavaObject>("fromFile", file))
            using (var intent = new AndroidJavaObject("android.content.Intent",
                                    "android.intent.action.MEDIA_SCANNER_SCAN_FILE", uri))
                act.Call("sendBroadcast", intent);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Share] 저장 실패: {e.Message}");
            return false;
        }
    }
#endif
}
