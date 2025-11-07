using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public struct TrialData
{
    public int Round;
    public float ID, A, W, MT_ms, TP;
    public bool IsError; // ER: true=1, false=0
    public Vector3 TouchPosition;
    public Vector3 TargetCenterPosition;
    public float HeadMovement;
}

public static class DataLogger
{
    private static readonly List<TrialData> trialDataList = new List<TrialData>();

    // 최근 저장 경로(확인용)
    public static string LastLocalPath { get; private set; }
    public static string LastDownloadsPath { get; private set; }

    public static void ClearData() => trialDataList.Clear();
    public static List<TrialData> GetAll() => new List<TrialData>(trialDataList);


    public static void AddTrialData(
        int round, float id, float a, float w, float mt_ms, bool isError, float tp,
        Vector3 touchPos, Vector3 targetCenterPos, float headMovement)
    {
        trialDataList.Add(new TrialData
        {
            Round = round,
            ID = id,
            A = a,
            W = w,
            MT_ms = mt_ms,
            IsError = isError,
            TP = tp,
            TouchPosition = touchPos,
            TargetCenterPosition = targetCenterPos,
            HeadMovement = headMovement
        });
        Debug.Log($"[CSV] Append trial #{trialDataList.Count} (Round={round}, ER={(isError ? 1 : 0)}, MT={mt_ms:F1}ms)");
    }

    /// <summary>
    /// 세션 종료 시 한 번만 호출. 
    /// 1) 앱 샌드박스(Application.persistentDataPath)에 항상 저장
    /// 2) (가능하면) Download/HCIExp로 복사 시도
    /// </summary>
    public static void SaveToCSV(string prefix = "session", string downloadsSubfolder = "HCIExp")
    {
        if (trialDataList.Count == 0)
        {
            Debug.LogWarning("[CSV] 기록이 없어 저장 생략");
            return;
        }

        try
        {
            var inv = CultureInfo.InvariantCulture;
            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"{prefix}_{ts}.csv";

            // 1) 샌드박스에 저장 (항상 성공하는 경로)
            string localPath = Path.Combine(Application.persistentDataPath, fileName);
            using (var sw = new StreamWriter(localPath, false, new UTF8Encoding(false)))
            {
                sw.WriteLine("Round,ID,Amplitude(A),Width(W),MovementTime(MT),ErrorRate(ER),Throughput(TP),TouchPosX,TouchPosY,TouchPosZ,TargetCenterX,TargetCenterY,TargetCenterZ,TouchOffset,HeadMovement");
                foreach (var d in trialDataList)
                {
                    float touchOffset = Vector3.Distance(d.TouchPosition, d.TargetCenterPosition);
                    sw.WriteLine(string.Join(",",
                        d.Round.ToString(inv), d.ID.ToString(inv), d.A.ToString(inv), d.W.ToString(inv),
                        d.MT_ms.ToString(inv), (d.IsError ? 1 : 0).ToString(inv), d.TP.ToString(inv),
                        d.TouchPosition.x.ToString(inv), d.TouchPosition.y.ToString(inv), d.TouchPosition.z.ToString(inv),
                        d.TargetCenterPosition.x.ToString(inv), d.TargetCenterPosition.y.ToString(inv), d.TargetCenterPosition.z.ToString(inv),
                        touchOffset.ToString(inv), d.HeadMovement.ToString(inv)
                    ));
                }
            }
            LastLocalPath = localPath;
            Debug.Log($"[CSV] 샌드박스 저장: {localPath}");
            

            // 2) Downloads/HCIExp로 복사 시도
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // 최신 안드로이드에서 실패할 수 있음(Scoped Storage). 실패해도 샌드박스 파일은 존재.
                string downloadsDir = Path.Combine("/sdcard/Download", downloadsSubfolder);
                Directory.CreateDirectory(downloadsDir); // 없으면 생성 시도
                string dest = Path.Combine(downloadsDir, Path.GetFileName(localPath));
                File.Copy(localPath, dest, true);
                LastDownloadsPath = dest;
                Debug.Log($"[CSV] Downloads 복사 성공: {dest}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CSV] Downloads 복사 실패: {ex.Message} (Android 10+에서 차단될 수 있음)");
            }
#else
            try
            {
                string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string dlDir = Path.Combine(user, "Downloads", downloadsSubfolder);
                Directory.CreateDirectory(dlDir);
                string dest = Path.Combine(dlDir, Path.GetFileName(localPath));
                File.Copy(localPath, dest, true);
                LastDownloadsPath = dest;
                Debug.Log($"[CSV] PC Downloads 복사: {dest}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CSV] PC Downloads 복사 실패: {ex.Message}");
            }
#endif
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CSV] 저장 실패: {ex.Message}");
        }
    }
    
}
