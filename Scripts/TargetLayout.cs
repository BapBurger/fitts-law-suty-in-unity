using UnityEngine;

public class TargetLayout : MonoBehaviour
{
    public GameObject targetPrefab;
    public GameObject[] targets;

    private const int NUMBER_OF_TARGETS = 11;

    public void PositionObjectsInCircle(float diameter, float width)
    {

        float radius = diameter * 0.5f;
        // 1) 타겟 배열 준비: 개수 불일치 시 재생성
        if (targets == null || targets.Length != NUMBER_OF_TARGETS)
        {
            // 기존 자식 모두 정리
            foreach (Transform child in transform)
            {
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            // 새로 생성
            targets = new GameObject[NUMBER_OF_TARGETS];
            for (int i = 0; i < NUMBER_OF_TARGETS; i++)
            {
                GameObject newTarget = Instantiate(targetPrefab, transform);
                newTarget.name = "Target " + i;

                // ★ 안전장치: 루트/자식 어딘가에 Target 마커가 없으면 루트에 부착
                if (newTarget.GetComponentInChildren<Target>(true) == null)
                {
                    newTarget.AddComponent<Target>(); // 표식(marker):contentReference[oaicite:1]{index=1}
                }

                targets[i] = newTarget;
            }
        }

        // 2) 위치/크기/색 업데이트 (★ i는 이 for 루프 안에서만 사용)
        float angleStep = 360f / NUMBER_OF_TARGETS;
        for (int i = 0; i < NUMBER_OF_TARGETS; i++)
        {
            // angle 계산 (12시 방향 시작)
            float angleDeg = i * angleStep + 90f;
            float angleRad = angleDeg * Mathf.Deg2Rad;

            // 로컬 위치
            Vector3 localPosition = new Vector3(
                Mathf.Cos(angleRad) * radius,
                Mathf.Sin(angleRad) * radius,
                0f
            );

            // 적용
            GameObject t = targets[i];
            if (t == null) continue; // 방어

            t.transform.localPosition = localPosition;
            t.transform.localScale = new Vector3(width, width, 0.01f);
            t.SetActive(true);

            // ★ 자식까지 모두 회색으로 초기화 (하이라이트 일관성):contentReference[oaicite:2]{index=2}
            var renderers = t.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                // 개별 인스턴스 머티리얼에 색 적용
                r.material.color = Color.gray;
            }
        }

        // 3) 간단 검증 로그
        int realCount = 0;
        foreach (var go in targets) if (go != null) realCount++;
        if (realCount == 0)
        {
            Debug.LogError("[TargetLayout] No targets instantiated. " +
                           "Check targetPrefab assignment and active states.");
        }
    }
}
