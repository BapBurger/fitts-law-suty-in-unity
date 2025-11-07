using UnityEngine;
using System.Collections.Generic; // HashSet을 사용하기 위해 추가

public class CursorInteraction : MonoBehaviour
{
    [Header("오브젝트 연결")]
    [Tooltip("따라다닐 컨트롤러의 Transform (예: RightHandAnchor)")]
    public Transform controllerTransform;


    [Header("커서 위치 오프셋 (컨트롤러 로컬 기준)")]
    // +Z 가 컨트롤러가 바라보는 앞쪽(팁) 방향입니다.
    public Vector3 localPositionOffset = new Vector3(0f, 0f, 0.08f); 
    public Vector3 localEulerOffset    = Vector3.zero; // 각도 미세조정 필요하면 사용
    // --- 내부 변수 ---
    private ExperimentManager manager;
    private readonly HashSet<Collider> overlappingColliders = new HashSet<Collider>();
    private StartButton currentlyTouchingButton = null;

    // ★ 추가: Rigidbody 캐시 (트리거 겹침 안정화)
    private Rigidbody rb;

    void Start()
    {
        manager = FindObjectOfType<ExperimentManager>();

        // ★ 추가: 커서는 물리엔진 기준으로 움직이도록 설정
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
        else
        {
            Debug.LogWarning("[CursorInteraction] Rigidbody가 없습니다. 트리거 겹침이 불안정할 수 있어요.");
        }
    }

    // ★ 추가: 이동은 FixedUpdate에서 MovePosition/MoveRotation 사용
    void FixedUpdate()
    {
        if (controllerTransform == null) return;

        if (rb != null)
        {
            rb.MovePosition(controllerTransform.position);
            rb.MoveRotation(controllerTransform.rotation);
        }
        else
        {
            transform.SetPositionAndRotation(controllerTransform.position, controllerTransform.rotation);
        }
    }

    void Update()
    {
        // 1. (삭제) transform.position 직접 이동 — FixedUpdate로 이전됨
        if (controllerTransform != null)
        {
            // 위치: 컨트롤러 로컬 오프셋을 월드로 변환
            transform.position = controllerTransform.TransformPoint(localPositionOffset);
            // 회전: 컨트롤러 회전에 오일러 보정 더하기
            transform.rotation = controllerTransform.rotation * Quaternion.Euler(localEulerOffset);
        }
        // 2. B 버튼 입력 처리 (재배치)
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            if (manager != null) manager.Recenter();
        }

        // 3. 트리거 버튼 입력 처리
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
        {
            if (manager == null) return;

            // 3-1. 시작 버튼을 누른 경우
            if (currentlyTouchingButton != null)
            {
                manager.StartExperiment();
                currentlyTouchingButton = null; // ★ 추가: 참조 해제하여 이후 트리거가 타깃 선택으로
                return;
            }

            // 3-2. 실험 중 타겟 선택 시도
            Collider currentTargetCollider = manager.GetCurrentTargetCollider(); // 현재 목표 타겟 콜라이더
            bool isHit = (currentTargetCollider != null) && overlappingColliders.Contains(currentTargetCollider);

            manager.CompleteCurrentTrial(isHit, transform.position);
        }
    }

    // 물리적 충돌이 시작될 때
    private void OnTriggerEnter(Collider other)
    {
        overlappingColliders.Add(other);

        var button = other.GetComponentInParent<StartButton>();
        if (button != null)
        {
            currentlyTouchingButton = button;
        }
    }

    // 물리적 충돌이 끝났을 때
    private void OnTriggerExit(Collider other)
    {
        overlappingColliders.Remove(other);

        var button = other.GetComponentInParent<StartButton>();
        if (button != null && button == currentlyTouchingButton)
        {
            currentlyTouchingButton = null;
        }
    }

    // ★ 추가: 비활성화 시 겹침 세트 정리 (안전)
    private void OnDisable()
    {
        overlappingColliders.Clear();
        currentlyTouchingButton = null;
    }
}
