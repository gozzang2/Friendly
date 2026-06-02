using UnityEngine;

public class TrainingSignalSimulator : MonoBehaviour
{
    public static TrainingSignalSimulator Instance;

    public float baseNoise = 0.05f;
    public float scareResponsePeak = 0.8f;
    public float stepDecayRate = 0.75f;

    public float currentDecibel { get; private set; }
    public float currentMouseDelta { get; private set; }

    public float baselineDecibel = 0f;
    public float baselineMouseDelta = 0f;
    public bool isBaselineReady = true;

    private float _responseMagnitude = 0f;
    private float _micSensitivity = 0.5f;
    private float _mouseSensitivity = 0.5f;

    // 연출별 피로도 추적
    private float[] fatigue = new float[7]; // action 0~6
    private float fatigueDecay = 0.8f; // 매 스텝 감쇠

    void Awake()
    {
        Instance = this;
        var realCollector = FindFirstObjectByType<SignalCollector>();
        if (realCollector != null) realCollector.enabled = false;
    }

    public void UpdateSimulationStep()
    {
        // 피로도 감쇠
        for (int i = 0; i < fatigue.Length; i++)
            fatigue[i] *= fatigueDecay;

        _responseMagnitude *= stepDecayRate;
        if (_responseMagnitude < 0.01f) _responseMagnitude = 0f;

        currentDecibel = Mathf.Clamp01(
            baseNoise + (_responseMagnitude * _micSensitivity)
            + Random.Range(0f, 0.03f));

        currentMouseDelta = Mathf.Clamp01(
            baseNoise + (_responseMagnitude * _mouseSensitivity)
            + Random.Range(0f, 0.05f));
    }

    public void TriggerResponse(float intensity = 1.0f, int actionIndex = -1)
    {
        float actualIntensity = intensity;
        if (actionIndex >= 0)
        {
            actualIntensity = intensity * (1f - fatigue[actionIndex]);
            fatigue[actionIndex] = Mathf.Clamp01(fatigue[actionIndex] + 0.3f);
        }

        //성향 벡터 반영
        float personalMultiplier = 1.0f;
        if (actionIndex >= 1 && PlayerProfiler.Instance != null)
        {
            float[] profile = PlayerProfiler.Instance.GetProfileVector();
            // profile[2]~[7] = action1~6 평균 FearSignal
            // 성향 높으면 잘 무서워함 → FearSignal 높게
            float sensitivity = profile[actionIndex + 1]; // action1→[2], action2→[3]...
            personalMultiplier = 0.3f + sensitivity * 1.4f;
            // sensitivity=0 → 0.3 (거의 반응 안 함)
            // sensitivity=1 → 1.7 (매우 잘 반응)
        }
        _responseMagnitude = scareResponsePeak * actualIntensity * personalMultiplier;
    }

    // 에피소드마다 랜덤 성향 주입
    public void RandomizeProfile()
    {
        if (PlayerProfiler.Instance == null) return;
        float[] randomProfile = new float[8];
        randomProfile[0] = Random.Range(0.1f, 0.9f); // 평균 마이크
        randomProfile[1] = Random.Range(0.1f, 0.9f); // 평균 마우스
        for (int i = 2; i < 8; i++)
            randomProfile[i] = Random.Range(0f, 0.8f); // action별 fearSignal
        PlayerProfiler.Instance.SetProfileVector(randomProfile);
        Debug.Log($"[Simulator] 랜덤 성향 주입: " +
            $"mic={randomProfile[0]:F2} mouse={randomProfile[1]:F2} " +
            $"actions=[{randomProfile[2]:F2},{randomProfile[3]:F2}," +
            $"{randomProfile[4]:F2},{randomProfile[5]:F2}," +
            $"{randomProfile[6]:F2},{randomProfile[7]:F2}]");
    }

    public void ApplyScenario(float micSens, float mouseSens, string scenarioName)
    {
        _micSensitivity = micSens;
        _mouseSensitivity = mouseSens;
        _responseMagnitude = 0f;
        Debug.Log($"[Simulator] 시나리오 변경: {scenarioName}");
    }

    public float GetNormalizedMic() => currentDecibel;
    public float GetNormalizedMouse() => currentMouseDelta;
}