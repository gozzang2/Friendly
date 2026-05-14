using UnityEngine;

public class TrainingManager : MonoBehaviour
{
    [Header("연결 오브젝트")]
    public HorrorDirector horrorDirector;
    public TrainingSignalSimulator simulator;

    [Header("에피소드 설정")]
    public int maxStepsPerEpisode = 200;
    private int _stepCount = 0;

    // 시나리오: 다양한 플레이어 유형 순환
    // (겁쟁이 / 침묵형 / 일반 / 강심장)
    // 파라미터: (micSensitivity, mouseSensitivity)
    private (float mic, float mouse, string name)[] _scenarios =
    {
        (0.9f, 0.9f, "완전겁쟁이"),  // 비명+몸반응 모두 강
        (0.9f, 0.2f, "소리형"),      // 비명은 지르는데 몸은 안 움직임
        (0.2f, 0.9f, "침묵형"),      // 말없이 마우스만 급격히 움직임
        (0.1f, 0.1f, "강심장"),      // 반응 거의 없음
        (0.5f, 0.5f, "일반"),        // 중간
        (0.8f, 0.5f, "반응형"),      // 비명 주로
        (0.5f, 0.8f, "행동형"),      // 움직임 주로
    };

    private int _scenarioIndex = 0;

    void Start()
    {
        ApplyCurrentScenario();
    }

    void Update()
    {
        _stepCount++;
        if (_stepCount >= maxStepsPerEpisode)
        {
            _stepCount = 0;
            NextScenario();
            horrorDirector.EndEpisode();
        }
    }

    void NextScenario()
    {
        _scenarioIndex = (_scenarioIndex + 1) % _scenarios.Length;
        ApplyCurrentScenario();
    }

    void ApplyCurrentScenario()
    {
        var s = _scenarios[_scenarioIndex];
        simulator.ApplyScenario(s.mic, s.mouse, s.name);
        Debug.Log($"[TrainingManager] 시나리오: {s.name}");
    }
}