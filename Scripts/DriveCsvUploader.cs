using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class DriveCsvUploader : MonoBehaviour
{
    [Header("Apps Script Web App")]
    [Tooltip("Apps Script 웹앱 URL (…/exec)")]
    public string webAppUrl;                 // 예: https://script.google.com/macros/s/XXXXX/exec
    public string secretKey = "YOUR_SECRET_TOKEN";
    public string folderId = "YOUR_FOLDER_ID";

    [Header("옵션")]
    public float timeoutSec = 20f;
    public bool verboseLog = true;

    /// <summary>로컬 CSV 경로를 Google Drive로 업로드</summary>
    public IEnumerator UploadCsv(string localCsvPath)
    {
        if (string.IsNullOrEmpty(localCsvPath) || !File.Exists(localCsvPath))
        {
            Debug.LogWarning("[Drive] CSV 파일이 없습니다: " + localCsvPath);
            yield break;
        }
        if (string.IsNullOrEmpty(webAppUrl))
        {
            Debug.LogError("[Drive] webAppUrl 미설정");
            yield break;
        }

        byte[] bytes = File.ReadAllBytes(localCsvPath);
        string filename = Path.GetFileName(localCsvPath);

        // 쿼리스트링에 메타데이터 전달 (바디는 raw 바이너리)
        string url = $"{webAppUrl}?filename={UnityWebRequest.EscapeURL(filename)}" +
                     $"&mime=text/csv&key={UnityWebRequest.EscapeURL(secretKey)}" +
                     $"&folderId={UnityWebRequest.EscapeURL(folderId)}";

        using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/octet-stream");
            req.timeout = Mathf.CeilToInt(timeoutSec);

            if (verboseLog) Debug.Log($"[Drive] 업로드 시작: {filename} ({bytes.Length}B)");
            yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            bool ok = (req.result == UnityWebRequest.Result.Success);
#else
            bool ok = !(req.isNetworkError || req.isHttpError);
#endif
            if (!ok)
            {
                Debug.LogError($"[Drive] 업로드 실패: {req.error}, code={req.responseCode}");
            }
            else
            {
                if (verboseLog) Debug.Log($"[Drive] 업로드 성공: {req.downloadHandler.text}");
            }
        }
    }
}
