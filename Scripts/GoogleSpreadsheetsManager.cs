using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

public class GoogleSpreadsheetsManager : MonoBehaviour
{
    [Header("Google Form")]
    [Tooltip("반드시 /formResponse 로 끝나야 합니다")]
    public string formResponseUrl = "https://docs.google.com/forms/u/0/d/e/xxxxxxxxxxxxxxxxxxxx/formResponse";

    [Tooltip("연속 제출 간 대기(초) — 0.5 권장")]
    public float submitInterval = 0.5f;

    [Tooltip("실패 시 최대 재시도")]
    public int maxRetry = 3;

    [Header("로그")]
    public bool verboseLog = true;

    // 내부 큐
    private readonly Queue<WWWForm> _queue = new Queue<WWWForm>();
    private bool _pumping = false;

    // ====== 네 폼의 entry 키들 (필요 시 수정) ======
    const string kRound        = "entry.1928880076";
    const string kID           = "entry.1266655026";
    const string kA            = "entry.785124100";
    const string kW            = "entry.1766220430";
    const string kMTms         = "entry.1165693372";
    const string kER           = "entry.1086660397";
    const string kTP           = "entry.1984646573";
    const string kTouchX       = "entry.31553847";
    const string kTouchY       = "entry.18164946";
    const string kTouchZ       = "entry.1873510302";
    const string kTargetX      = "entry.1808663062";
    const string kTargetY      = "entry.683946295";
    const string kTargetZ      = "entry.1205902646";
    const string kTouchOffset  = "entry.1210658792";
    const string kHeadMovement = "entry.1581156500";
    // ============================================

    void Awake()
    {
        DontDestroyOnLoad(gameObject); // 씬 전환 시에도 유지
    }

    // ---- 퍼블릭: TrialRecord 한 건을 큐에 넣기 (세션/참가자 없음) ----
    public void EnqueueTrial(TrialRecord r)
    {
        var inv = CultureInfo.InvariantCulture;

        WWWForm f = new WWWForm();
        f.AddField(kRound,       r.Round.ToString(inv));
        f.AddField(kID,          r.ID.ToString(inv));
        f.AddField(kA,           r.Amplitude.ToString(inv));
        f.AddField(kW,           r.Width.ToString(inv));
        f.AddField(kMTms,        r.MT_ms.ToString(inv));
        f.AddField(kER,          r.ER.ToString(inv));          // 0/1
        f.AddField(kTP,          r.TP.ToString(inv));
        f.AddField(kTouchX,      r.TouchPosX.ToString(inv));
        f.AddField(kTouchY,      r.TouchPosY.ToString(inv));
        f.AddField(kTouchZ,      r.TouchPosZ.ToString(inv));
        f.AddField(kTargetX,     r.TargetCenterX.ToString(inv));
        f.AddField(kTargetY,     r.TargetCenterY.ToString(inv));
        f.AddField(kTargetZ,     r.TargetCenterZ.ToString(inv));
        f.AddField(kTouchOffset, r.TouchOffset.ToString(inv));
        f.AddField(kHeadMovement,r.HeadMovement.ToString(inv));

        _queue.Enqueue(f);
        if (!_pumping) StartCoroutine(PumpQueue());
    }

    // ---- 내부: 큐 펌프(직렬 전송 + 재시도 + 간격) ----
    private IEnumerator PumpQueue()
    {
        if (!ValidateUrl()) yield break;
        _pumping = true;

        int total = _queue.Count;
        int sent = 0;

        while (_queue.Count > 0)
        {
            var form = _queue.Dequeue();
            bool ok = false;

            for (int attempt = 1; attempt <= Mathf.Max(1, maxRetry); attempt++)
            {
                using (UnityWebRequest req = UnityWebRequest.Post(formResponseUrl, form))
                {
                    req.timeout = 12;
                    req.chunkedTransfer = false;
#if UNITY_2020_2_OR_NEWER
                    req.useHttpContinue = false;
#endif
                    yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                    bool success = (req.result == UnityWebRequest.Result.Success);
#else
                    bool success = !(req.isNetworkError || req.isHttpError);
#endif
                    long code = req.responseCode; // 200 또는 302면 성공으로 간주
                    if (success && (code == 200 || code == 302))
                    {
                        ok = true;
                        sent++;
                        if (verboseLog) Debug.Log($"[Form] OK ({sent}/{total}) code={code}");
                        break;
                    }

                    float backoff = 0.5f * Mathf.Pow(2f, attempt - 1); // 0.5,1.0,2.0...
                    Debug.LogWarning($"[Form] 실패 attempt#{attempt} code={code} err={req.error} → {backoff:0.0}s 대기");
                    yield return new WaitForSeconds(backoff);
                }
            }

            if (submitInterval > 0f) yield return new WaitForSeconds(submitInterval);
        }

        if (verboseLog) Debug.Log($"[Form] 전송 완료: {sent}/{total}");
        _pumping = false;
    }

    private bool ValidateUrl()
    {
        if (string.IsNullOrEmpty(formResponseUrl) || !formResponseUrl.Contains("formResponse"))
        {
            Debug.LogError("[Form] URL이 유효하지 않습니다. 반드시 /formResponse 로 끝나야 합니다.");
            return false;
        }
        return true;
    }
}
