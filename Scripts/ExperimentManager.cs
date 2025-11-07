using System;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Text;
using System.Globalization;

[System.Serializable]
public class AWBlock
{
    [Tooltip("반지름(Amplitude)")]
    public float amplitude = 0.4f;
    [Tooltip("타겟 폭(Width)")]
    public float width = 0.05f;
    [Tooltip("선택사항: 라벨/메모")]
    public string label = "";
}

public class ExperimentManager : MonoBehaviour
{
    [Header("오브젝트 연결")]
    public TargetLayout targetLayout;
    public GameObject startButtonPrefab;
    public Camera mainCamera;

    [Header("실험 설정")]
    [Tooltip("Recenter 시 카메라 전방 거리(m)")]
    public float recenterDistance = 0.49f;
    [Tooltip("라운드 반복 횟수")]
    public int rounds = 6;

    [Header("블록 (A, W) 리스트 — Inspector에서 직접 입력")]
    public List<AWBlock> awList = new List<AWBlock>()
    {
        new AWBlock{ amplitude=0.20f, width=0.05f,  label="예시 1"},
        new AWBlock{ amplitude=0.20f, width=0.035f, label="예시 2"},
        new AWBlock{ amplitude=0.20f, width=0.025f, label="예시 3"},
    };

    [Header("랜덤 설정")]
    [Tooltip("라운드마다 (A,W) 블록 순서를 셔플")]
    public bool shuffleBlocksPerRound = true;
    [Tooltip("재현 가능한 셔플용 시드 (0이면 매번 다른 순서)")]
    public int randomSeed = 0;

    [Header("세션 CSV 내보내기(모든 라운드 종료 시 1개 파일)")]
    public bool exportCsvAtEnd = true;
    public string csvPrefix = "session";
    public string downloadsSubfolder = "HCIExp";

    // 런타임 상태
    private int roundIdx = 0;          // 0..rounds-1
    private int blockPos = 0;          // 0..(blockOrder.Count-1)
    private int trialsDoneInBlock = 0; // 0..targets.Count (11개)

    // 현재 블록 파라미터
    private float currentA = 0f;
    private float currentW = 0f;
    private float currentID = 0f;      // ID = log2(A/W + 1)

    // 타겟/트라이얼
    private List<GameObject> targets;
    private int currentTargetIndex = 0;
    private Collider currentTargetCollider; // 캐시

    // 트라이얼 타이밍/헤드무브먼트
    private bool isTrialActive = false;
    private float trialStartTime = 0f;   // Time.realtimeSinceStartup
    private Vector3 headPrevPos;
    private float headPathLen = 0f;      // 누적 경로 길이(m)

    // 라운드 내 블록 순서 (awList 인덱스)
    private List<int> blockOrder = new List<int>();

    // 기타
    private GameObject startButtonInstance;
    private bool isExperimentActive = false;

    // 세션 전체 트라이얼 누적 저장
    private readonly List<TrialRecord> sessionRecords = new List<TrialRecord>();
    private bool sessionExported = false;   // 중복 내보내기 방지

    void Start()
    {
        if (targetLayout != null) targetLayout.gameObject.SetActive(false);

        if (startButtonPrefab != null)
        {
            startButtonInstance = Instantiate(startButtonPrefab);
            Recenter(); // 시작 버튼만 전방으로
        }
    }

    void Update()
    {
        // 헤드 무브먼트 누적 (Trial 진행 중에만)
        if (isTrialActive && mainCamera != null)
        {
            Vector3 hp = mainCamera.transform.position;
            float d = Vector3.Distance(hp, headPrevPos);
            headPathLen += d;
            headPrevPos = hp;
        }
    }

    // ========= 공개 API =========

    public void StartExperiment()
    {
        if (isExperimentActive) return; // 중복 방지
        if (startButtonInstance != null) startButtonInstance.SetActive(false);
        if (targetLayout != null && startButtonInstance != null)
        {
            var t = startButtonInstance.transform;
            targetLayout.transform.SetPositionAndRotation(t.position, t.rotation);
        }
        DataLogger.ClearData(); // ★ 추가

        if (awList == null || awList.Count == 0)
        {
            Debug.LogError("[Experiment] awList가 비어 있습니다. Inspector에서 (A, W)를 추가하세요.");
            return;
        }

        isExperimentActive = true;
        roundIdx = 0;
        blockPos = 0;

        sessionRecords.Clear(); // ★ 세션 누적 초기화
        sessionExported = false;

        BuildBlockOrderForRound(roundIdx);
        StartBlock();

        Debug.Log($"[Experiment] Start. rounds={rounds}, blocksPerRound={awList.Count}");
    }

    public void Recenter()
    {
        GameObject objectToRecenter = isExperimentActive ? targetLayout?.gameObject : startButtonInstance;
        if (objectToRecenter != null && mainCamera != null)
        {
            Transform head = mainCamera.transform;
            objectToRecenter.transform.position = head.position + head.forward * recenterDistance;
            objectToRecenter.transform.rotation = Quaternion.LookRotation(objectToRecenter.transform.position - head.position);
        }
    }

    // CursorInteraction이 트리거 눌렀을 때 호출
    public void CompleteCurrentTrial(bool isHit, Vector3 touchPos)
    {
        if (!isTrialActive || targets == null || targets.Count == 0) return;

        // 1) 타임/지표 계산
        float mtMs = (Time.realtimeSinceStartup - trialStartTime) * 1000f; // ms
        int er = isHit ? 0 : 1;
        float tp = currentID / Mathf.Max(1e-6f, (mtMs / 1000f));           // bits/s

        // 2) 좌표/오프셋
        Vector3 targetCenter = targets[currentTargetIndex].transform.position;
        float offset = Vector3.Distance(touchPos, targetCenter);

        // 3) Trial 레코드 작성
        var rec = new TrialRecord
        {
            Round = roundIdx + 1,   // 1-based
            ID = currentID,
            Amplitude = currentA,
            Width = currentW,
            MT_ms = mtMs,
            ER = er,
            TP = tp,
            TouchPosX = touchPos.x,
            TouchPosY = touchPos.y,
            TouchPosZ = touchPos.z,
            TargetCenterX = targetCenter.x,
            TargetCenterY = targetCenter.y,
            TargetCenterZ = targetCenter.z,
            TouchOffset = offset,
            HeadMovement = headPathLen
        };

        DataLogger.AddTrialData(
            roundIdx + 1,            // Round (1-based)
            currentID,               // ID
            currentA,                // A
            currentW,                // W
            mtMs,                    // MT (ms)
            !isHit,                  // ER: 실패면 true
            tp,                      // TP
            touchPos,                // 터치 위치
            targetCenter,            // 타겟 중심
            headPathLen              // Head movement
        );
        // ★ 세션 누적
        sessionRecords.Add(rec);
        Debug.Log($"[CSV] Append trial #{sessionRecords.Count} (Round={rec.Round}, ER={rec.ER}, MT={rec.MT_ms:F1}ms)");

        // 5) 다음 타겟으로 진행
        isTrialActive = false;

        DehighlightTarget(currentTargetIndex);
        trialsDoneInBlock++;

        if (trialsDoneInBlock >= targets.Count)
        {
            EndBlock(); // 블록 종료 → 다음 (A,W) / 라운드 / 실험 종료
            return;
        }

        // 기존 "반대편 점프" 순서 유지
        int half = targets.Count / 2;
        currentTargetIndex = (currentTargetIndex + half) % targets.Count;

        StartTrial(); // 다음 타겟 시작
    }

    // CursorInteraction이 현재 타겟 콜라이더를 조회할 수 있도록 제공 (캐시 반환)
    public Collider GetCurrentTargetCollider() => currentTargetCollider;

    // ========= 내부 흐름 =========

    private void BuildBlockOrderForRound(int round)
    {
        blockOrder = new List<int>(awList.Count);
        for (int i = 0; i < awList.Count; i++) blockOrder.Add(i);

        if (shuffleBlocksPerRound && awList.Count > 1)
        {
            System.Random rng = (randomSeed != 0)
                ? new System.Random(randomSeed ^ (round * 73856093))
                : new System.Random(Environment.TickCount ^ (round * 19349663));

            for (int i = blockOrder.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (blockOrder[i], blockOrder[j]) = (blockOrder[j], blockOrder[i]);
            }
        }
    }

    private void StartBlock()
    {
        if (targetLayout == null)
        {
            Debug.LogError("[Experiment] TargetLayout이 지정되지 않았습니다.");
            return;
        }

        int idxInAw = (blockOrder != null && blockOrder.Count > 0)
                        ? blockOrder[Mathf.Clamp(blockPos, 0, blockOrder.Count - 1)]
                        : Mathf.Clamp(blockPos, 0, awList.Count - 1);

        var blk = awList[idxInAw];

        currentA = Mathf.Max(1e-4f, blk.amplitude);
        currentW = Mathf.Max(1e-4f, blk.width);
        currentID = Mathf.Log((currentA / currentW) + 1f, 2f); // Shannon

        // 타겟 배치(색 초기화 포함) — Recenter 자동 호출 없음
        targetLayout.gameObject.SetActive(true);
        targetLayout.PositionObjectsInCircle(currentA, currentW);

        // 타겟 리스트 갱신
        targets = new List<GameObject>();
        if (targetLayout.targets != null)
        {
            foreach (var t in targetLayout.targets)
                if (t != null) targets.Add(t);
        }

        // 블록 카운터 초기화 후 첫 트라이얼 시작
        trialsDoneInBlock = 0;
        currentTargetIndex = 0;
        StartTrial();

        Debug.Log($"[Block Start] Round {roundIdx + 1}/{rounds}, BlockPos {blockPos + 1}/{awList.Count}, (A,W)=({currentA:F3},{currentW:F3}), ID≈{currentID:F3}");
    }

    private void StartTrial()
    {
        // 하이라이트
        HighlightTarget(currentTargetIndex);

        // 현재 타겟 콜라이더 캐시
        currentTargetCollider = null;
        if (currentTargetIndex >= 0 && currentTargetIndex < targets.Count && targets[currentTargetIndex] != null)
        {
            currentTargetCollider = targets[currentTargetIndex].GetComponentInChildren<Collider>();
        }

        // 타이밍/헤드 무브먼트 초기화
        trialStartTime = Time.realtimeSinceStartup;
        headPathLen = 0f;
        if (mainCamera != null) headPrevPos = mainCamera.transform.position;

        isTrialActive = true;
    }

    private void EndBlock()
    {
        blockPos++;

        if (blockPos < (blockOrder?.Count ?? awList.Count))
        {
            StartBlock();            // 같은 라운드의 다음 (A,W)
        }
        else
        {
            // 라운드 종료
            blockPos = 0;
            roundIdx++;

            if (roundIdx < rounds)
            {
                BuildBlockOrderForRound(roundIdx);
                StartBlock();        // 다음 라운드 시작
            }
            else
            {
                EndExperiment();
            }
        }
    }

    private void EndExperiment()
    {
        isExperimentActive = false;
        isTrialActive = false;

        if (targetLayout != null) targetLayout.gameObject.SetActive(false);

        if (startButtonInstance != null)
        {
            startButtonInstance.SetActive(true);
            Recenter(); // 종료 시 시작 버튼만 전방으로
        }

        Debug.Log($"[Experiment] 모든 라운드 종료. sessionRecords={sessionRecords.Count}");

        // ★ 모든 라운드 종료 시 CSV 1개 생성 (중복 방지)
        if (exportCsvAtEnd && !sessionExported)
        {
            ExportSessionCsv();
            sessionExported = true;
        }

        DataLogger.SaveToCSV(csvPrefix, downloadsSubfolder); // ★ 추가
        var uploader = FindAnyObjectByType<DriveCsvUploader>(); // or 인스펙터로 참조
        if (uploader != null && !string.IsNullOrEmpty(DataLogger.LastLocalPath))
        {
            StartCoroutine(uploader.UploadCsv(DataLogger.LastLocalPath));
        }
        
    }

    private void OnApplicationQuit()
    {
        // 앱 종료 시에도 혹시 남아있으면 한 번 더 보장
        if (exportCsvAtEnd && !sessionExported && sessionRecords.Count > 0)
        {
            Debug.Log("[CSV] OnApplicationQuit → ExportSessionCsv (safety)");
            ExportSessionCsv();
            sessionExported = true;
        }
    }

    // ===== 유틸 (색 하이라이트) =====
    private void HighlightTarget(int index)
    {
        if (index >= 0 && index < (targets?.Count ?? 0) && targets[index] != null)
        {
            var renderer = targets[index].GetComponentInChildren<Renderer>();
            if (renderer != null) renderer.material.color = Color.red;
        }
    }

    private void DehighlightTarget(int index)
    {
        if (index >= 0 && index < (targets?.Count ?? 0) && targets[index] != null)
        {
            var renderer = targets[index].GetComponentInChildren<Renderer>();
            if (renderer != null) renderer.material.color = Color.gray;
        }
    }

    // ===== 세션 전체 CSV 내보내기 =====
    private void ExportSessionCsv()
    {
        try
        {
            if (sessionRecords == null || sessionRecords.Count == 0)
            {
                Debug.LogWarning("[CSV] 기록된 트라이얼이 없어 CSV를 생성하지 않습니다.");
                return;
            }

            string ts = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"{csvPrefix}_{ts}.csv";
            string localPath = Path.Combine(Application.persistentDataPath, fileName);

            using (var sw = new StreamWriter(localPath, false, new UTF8Encoding(false)))
            {
                sw.WriteLine("Round,ID,Amplitude,Width,MT_ms,ER,TP,TouchPosX,TouchPosY,TouchPosZ,TargetCenterX,TargetCenterY,TargetCenterZ,TouchOffset,HeadMovement");
                var inv = CultureInfo.InvariantCulture;
                foreach (var r in sessionRecords)
                {
                    sw.WriteLine(string.Join(",",
                        r.Round.ToString(inv), r.ID.ToString(inv), r.Amplitude.ToString(inv), r.Width.ToString(inv),
                        r.MT_ms.ToString(inv), r.ER.ToString(inv), r.TP.ToString(inv),
                        r.TouchPosX.ToString(inv), r.TouchPosY.ToString(inv), r.TouchPosZ.ToString(inv),
                        r.TargetCenterX.ToString(inv), r.TargetCenterY.ToString(inv), r.TargetCenterZ.ToString(inv),
                        r.TouchOffset.ToString(inv), r.HeadMovement.ToString(inv)
                    ));
                }
            }

            Debug.Log($"[CSV] 세션 파일 저장: {localPath}");

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                byte[] bytes = File.ReadAllBytes(localPath);
                string uri = AndroidDownloadsExporter.SaveBytesToDownloads(
                    bytes, Path.GetFileName(localPath), "text/csv", downloadsSubfolder);
                Debug.Log($"[CSV] Downloads로 내보냄: {uri} (폴더: Download/{downloadsSubfolder})");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CSV] Downloads 내보내기 실패: {ex.Message}");
            }
#else
            try
            {
                string user = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
                string destDir = Path.Combine(user, "Downloads", downloadsSubfolder);
                Directory.CreateDirectory(destDir);
                string dest = Path.Combine(destDir, Path.GetFileName(localPath));
                File.Copy(localPath, dest, true);
                Debug.Log($"[CSV] PC Downloads로 복사: {dest}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CSV] PC Downloads 복사 실패: {ex.Message}");
            }
#endif
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[CSV] 세션 내보내기 실패: {ex.Message}");
        }
    }

    // 에디터에서 즉시 내보내기 테스트
    [ContextMenu("Export Session CSV Now (Test)")]
    private void ExportSessionCsvNowTest() => ExportSessionCsv();
}

// === 업로드용 데이터 모델 ===
[Serializable]
public class TrialRecord
{
    public int Round;
    public float ID;
    public float Amplitude;
    public float Width;
    public float MT_ms;
    public int ER;
    public float TP;
    public float TouchPosX, TouchPosY, TouchPosZ;
    public float TargetCenterX, TargetCenterY, TargetCenterZ;
    public float TouchOffset;
    public float HeadMovement;
}
