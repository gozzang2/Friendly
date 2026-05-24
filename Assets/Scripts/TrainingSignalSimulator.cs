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
        _responseMagnitude = scareResponsePeak * actualIntensity;
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