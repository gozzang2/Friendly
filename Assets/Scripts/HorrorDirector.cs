using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using UnityEngine;
using System.Collections.Generic;

public class HorrorDirector : Agent
{
    // ────────────────────────────────────────────
    // 상수: 연출 추가 시 ACTION_COUNT만 올리고
    //       ExecuteAction에 case 추가하면 끝
    // ────────────────────────────────────────────
    private const int ACTION_COUNT = 7;
    private const int IDLE_ACTION = 0;

    // ────────────────────────────────────────────
    // 보상 가중치
    // ────────────────────────────────────────────
    [Header("Fear Signal 가중치")]
    [SerializeField] private float micWeight = 0.7f;
    [SerializeField] private float mouseWeight = 0.3f;

    [Header("보상 스케일")]
    [SerializeField] private float baseReward = 0.4f;
    [SerializeField] private float fearRewardScale = 2.0f;
    [SerializeField] private float repetitionPenalty = -0.7f;
    [SerializeField] private float decayPenalty = -0.2f;
    [SerializeField] private float diversityRewardScale = 0.6f;
    [SerializeField] private float silenceRewardPerStep = 0.01f;

    [Header("페이싱 설정")]
    [SerializeField] private int minSilenceStepsForReward = 3;
    [SerializeField] private int maxSilenceStepsBeforePenalty = 30;

    // ────────────────────────────────────────────
    // 내부 상태
    // ────────────────────────────────────────────
    private int lastAction = -1;
    private float lastFearSignal = 0f;
    private int consecutiveSilence = 0;

    private const int HISTORY_SIZE = 8;
    private Queue<int> actionHistory = new Queue<int>();
    private int[] actionUsageCount;

    // Update에서 읽어두는 원값 (Observation용)
    private float mousePanicValue;
    private float micVolumeValue;

    // ────────────────────────────────────────────
    // Agent 생명주기
    // ────────────────────────────────────────────
    public override void Initialize()
    {
        actionUsageCount = new int[ACTION_COUNT];

        // 씬 시작 시 성향 벡터 로드
        // 1부: PlayerPrefs에 저장된 값 없으면 기본값 0f
        // 2부: 1부에서 저장한 성향 벡터 로드
        if (PlayerProfiler.Instance != null)
            PlayerProfiler.Instance.LoadProfile();
    }

    public override void OnEpisodeBegin()
    {
        lastAction = -1;
        lastFearSignal = 0f;
        consecutiveSilence = 0;
        actionHistory.Clear();
        actionUsageCount = new int[ACTION_COUNT];
    }

    // ────────────────────────────────────────────
    // Update: 원값만 저장 (0.1초 수집 주기는 SignalCollector 내부에서 처리)
    // ────────────────────────────────────────────
    void Update()
    {
        if (TrainingSignalSimulator.Instance != null)
        {
            // 학습 씬: 시뮬레이터에서 읽음
            mousePanicValue = TrainingSignalSimulator.Instance.currentMouseDelta;
            micVolumeValue = TrainingSignalSimulator.Instance.currentDecibel;
        }
        else if (SignalCollector.Instance != null)
        {
            // 실제 씬: 원값 저장 (정규화는 OnActionReceived에서 GetNormalized로)
            mousePanicValue = SignalCollector.Instance.currentMouseDelta;
            micVolumeValue = SignalCollector.Instance.currentDecibel;
        }
    }

    // ────────────────────────────────────────────
    // Observation: 총 29개
    // ────────────────────────────────────────────
    public override void CollectObservations(VectorSensor sensor)
    {
        // 관측 전 시뮬레이터 1스텝 업데이트
        if (TrainingSignalSimulator.Instance != null)
            TrainingSignalSimulator.Instance.UpdateSimulationStep();

        sensor.AddObservation(mousePanicValue);   // 1: 현재 마우스 움직임
        sensor.AddObservation(micVolumeValue);    // 2: 현재 마이크 데시벨
        sensor.AddObservation(lastFearSignal);    // 3: 직전 Fear Signal

        // 직전 행동 원-핫 인코딩 4-10
        for (int i = 0; i < ACTION_COUNT; i++)
            sensor.AddObservation(lastAction == i ? 1f : 0f); 

        // 연속 침묵 길이 (정규화) 11
        sensor.AddObservation(
            (float)consecutiveSilence / maxSilenceStepsBeforePenalty); 

        // 각 연출 사용 빈도 12-17
        int total = Mathf.Max(1, GetTotalNonIdleActions());
        for (int i = 1; i < ACTION_COUNT; i++)
            sensor.AddObservation((float)actionUsageCount[i] / total); 

        // 위치 기반 연출 가능 여부 (학습 씬에서는 항상 true)
        if (TrainingSignalSimulator.Instance != null)
        {
            // 학습 씬: 항상 가능하다고 가정
            sensor.AddObservation(1f); // 마네킹 가능
            sensor.AddObservation(1f); // 조명 가능
            sensor.AddObservation(1f); // 문 가능
            sensor.AddObservation(1f); // 그림 글리치 가능
        }
        else
        {
            // 실제 씬: 실제 상태 체크, 위치기반 18-21
            sensor.AddObservation(ScareManager.Instance != null
                && ScareManager.Instance.HasNearbyMannequin() ? 1f : 0f); 
            sensor.AddObservation(ScareManager.Instance != null
                && ScareManager.Instance.HasNearbyLight() ? 1f : 0f);     
            sensor.AddObservation(ScareManager.Instance != null
                && ScareManager.Instance.HasUnlockedDoor() ? 1f : 0f);   
            sensor.AddObservation(ScareManager.Instance != null
                && ScareManager.Instance.CanTriggerGlitch() ? 1f : 0f); 
        }

        // 플레이어 성향 벡터(1부에서 수집, 8개)22-29
        if (PlayerProfiler.Instance != null)
        {
            float[] profile = PlayerProfiler.Instance.GetProfileVector();
            foreach (float v in profile)
                sensor.AddObservation(v); // 22~29
        }
        else
        {
            for (int i = 0; i < 8; i++)
                sensor.AddObservation(0f); // 22~29
        }
    }

    // ────────────────────────────────────────────
    // OnActionReceived: 보상 4레이어
    // ────────────────────────────────────────────
    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];

        // ── 행동 실행 ──
        ExecuteAction(action);

        // ── Fear Signal 계산 (신뢰도 가중치 포함) ──
        float normalizedMic = 0f;
        float normalizedMouse = 0f;

        if (TrainingSignalSimulator.Instance != null)
        {
            normalizedMic = TrainingSignalSimulator.Instance.GetNormalizedMic();
            normalizedMouse = TrainingSignalSimulator.Instance.GetNormalizedMouse();
        }
        else if (SignalCollector.Instance != null)
        {
            normalizedMic = SignalCollector.Instance.GetNormalizedMic();
            normalizedMouse = SignalCollector.Instance.GetNormalizedMouse();
        }

        // 동시 반응 보너스: 마우스+마이크 둘 다 클수록 신뢰도 높음
        // 조작 실수나 생활 소음은 하나만 튀므로 걸러짐
        float simultaneousBonus = normalizedMic * normalizedMouse;
        float fearSignal = Mathf.Clamp01(
            (normalizedMic * micWeight)
          + (normalizedMouse * mouseWeight)
          + (simultaneousBonus * 0.3f));

        Debug.Log($"[HorrorDirector] action: {action}, mic: {normalizedMic:F3}, mouse: {normalizedMouse:F3}, fearSignal: {fearSignal:F3}");

        // ────────────────────────────────
        // 보상 레이어 1: 즉각 반응 보상, 다양성 보상
        // ────────────────────────────────
        if (action != IDLE_ACTION)
        {
                // 기본보상 + 유효한 연출 → Fear Signal에 비례한 보상
                //AddReward(baseReward + fearSignal * fearRewardScale);
                float fearReward = baseReward + fearSignal * fearRewardScale;
                AddReward(fearReward);

                // 실제 실행된 연출만 다양성 카운트에 포함
                actionUsageCount[action]++;
                UpdateActionHistory(action);
                float diversity = CalculateDiversityScore();
                AddReward(diversity * diversityRewardScale);
            }
        

        // ────────────────────────────────
        // 보상 레이어 2: 반복 페널티
        // ────────────────────────────────
        if (lastAction != -1 && action != IDLE_ACTION)
        {
            // 2a) 동일 행동 연속 사용
            if (action == lastAction)
            {
                AddReward(repetitionPenalty);
            }

            // 2b) 같은 행동 + Fear Signal 이전보다 감소 (효과 감소)
            if (action == lastAction && fearSignal < lastFearSignal - 0.1f)
            {
                AddReward(decayPenalty);
            }
        }

        // ────────────────────────────────
        // 보상 레이어 3: 페이싱(침묵) 보상
        // ────────────────────────────────
        if (action == IDLE_ACTION)
        {
            consecutiveSilence++;

            if (consecutiveSilence >= minSilenceStepsForReward
             && consecutiveSilence < maxSilenceStepsBeforePenalty)
            {
                AddReward(silenceRewardPerStep);
            }
            else if (consecutiveSilence >= maxSilenceStepsBeforePenalty)
            {
                // 너무 오래 아무것도 안 함 → 방치 패널티
                AddReward(-0.3f);
            }
        }
        else
        {
            consecutiveSilence = 0;
        }

        // ── 상태 업데이트 ──
        if (action != IDLE_ACTION)
            lastFearSignal = fearSignal;
        lastAction = action;
    }

    // ────────────────────────────────────────────
    // 행동 실행 (연출 추가 시 case만 늘리면 됨)
    // ────────────────────────────────────────────
    private void ExecuteAction(int action)
    {
        bool isTraining = TrainingSignalSimulator.Instance != null;

        if (isTraining)
        {
            if (ScareManager_Training.Instance == null) return;
            var sm = ScareManager_Training.Instance;
            switch (action)
            {
                case 0: break;
                case 1: sm.CallMannequin(); break;
                case 2: sm.CallRedLights(); break;
                case 3: sm.CallJumpScare(); break;
                case 4: sm.CallScareSound(); break;
                case 5: sm.CallDoorScare(); break;
                case 6: sm.CallPictureGlitch(); break;
                default:
                    Debug.LogWarning($"[HorrorDirector] 미처리 action: {action}");
                    break;
            }
        }
        else
        {
            if (ScareManager.Instance == null) return;
            var sm = ScareManager.Instance;
            switch (action)
            {
                case 0: break;
                case 1: sm.CallMannequin(); break;
                case 2: sm.CallRedLights(); break;
                case 3: sm.CallJumpScare(); break;
                case 4: sm.CallScareSound(); break;
                case 5: sm.CallDoorScare(); break;
                case 6: sm.CallPictureGlitch(); break;
                default:
                    Debug.LogWarning($"[HorrorDirector] 미처리 action: {action}");
                    break;
            }
        }
    }

    // ────────────────────────────────────────────
    // 헬퍼: 다양성 점수
    // ────────────────────────────────────────────
    private void UpdateActionHistory(int action)
    {
        actionHistory.Enqueue(action);
        if (actionHistory.Count > HISTORY_SIZE)
            actionHistory.Dequeue();
    }

    private float CalculateDiversityScore()
    {
        if (actionHistory.Count < 2) return 0f;

        int[] counts = new int[ACTION_COUNT];
        foreach (int a in actionHistory) counts[a]++;

        float entropy = 0f;
        int n = actionHistory.Count;
        int nonIdle = ACTION_COUNT - 1;

        for (int i = 1; i < ACTION_COUNT; i++)
        {
            if (counts[i] > 0)
            {
                float p = (float)counts[i] / n;
                entropy -= p * Mathf.Log(p);
            }
        }

        float maxEntropy = Mathf.Log(nonIdle);
        return maxEntropy > 0 ? entropy / maxEntropy : 0f;
    }

    private int GetTotalNonIdleActions()
    {
        int total = 0;
        for (int i = 1; i < ACTION_COUNT; i++)
            total += actionUsageCount[i];
        return total;
    }

}