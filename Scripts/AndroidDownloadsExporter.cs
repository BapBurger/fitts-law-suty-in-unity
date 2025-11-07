// Assets/AndroidDownloadsExporter.cs
using System;
using UnityEngine;

public static class AndroidDownloadsExporter
{
#if UNITY_ANDROID && !UNITY_EDITOR
    // bytes를 Downloads/<subfolder>/<displayName> 로 저장 (API 29+ MediaStore)
    public static string SaveBytesToDownloads(byte[] bytes, string displayName, string mimeType, string subfolder)
    {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
        using (var values = new AndroidJavaObject("android.content.ContentValues"))
        using (var mediaStoreDownloads = new AndroidJavaClass("android.provider.MediaStore$Downloads"))
        {
            // 필수 메타데이터
            values.Call<AndroidJavaObject>("put", "mime_type", mimeType);
            values.Call<AndroidJavaObject>("put", "title", displayName);
            values.Call<AndroidJavaObject>("put", "_display_name", displayName);

            // Download/ 또는 Download/<subfolder>
            string rel = string.IsNullOrEmpty(subfolder) ? "Download" : ("Download/" + subfolder);
            values.Call<AndroidJavaObject>("put", "relative_path", rel);

            // 항목 생성
            var external = mediaStoreDownloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");
            var uri = resolver.Call<AndroidJavaObject>("insert", external, values);
            if (uri == null) throw new Exception("ContentResolver.insert returned null");

            // 쓰기 스트림 열기
            var pfd = resolver.Call<AndroidJavaObject>("openFileDescriptor", uri, "w");
            var fd = pfd.Call<AndroidJavaObject>("getFileDescriptor");
            using (var fos = new AndroidJavaObject("java.io.FileOutputStream", fd))
            {
                fos.Call("write", bytes);
                fos.Call("flush");
                fos.Call("close");
            }
            pfd.Call("close");
            return uri.Call<string>("toString");
        }
    }
#else
    public static string SaveBytesToDownloads(byte[] bytes, string displayName, string mimeType, string subfolder)
    {
        throw new PlatformNotSupportedException("Android only");
    }
#endif
}
