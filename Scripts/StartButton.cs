using UnityEngine;

public class StartButton : MonoBehaviour
{
    public ExperimentManager manager;

    void Awake()
    {
        manager = FindObjectOfType<ExperimentManager>();
    }

    // 선택되었을 때 ExperimentManager에게 실험 시작을 알림
    public void Select()
    {
        manager.StartExperiment();
    }
}