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
    // 보상 가중치 (Inspector 튜닝 가능)
    // ────────────────────────────────────────────
    [Header("Fear Signal 가중치")]
    public float micWeight = 0.7f;
    public float mouseWeight = 0.3f;

    [Header("보상 스케일")]
    public float fearRewardScale = 1.5f;
    public float repetitionPenalty = -0.8f;
    public float decayPenalty = -0.5f;
    public float diversityRewardScale = 0.4f;
    public float silenceRewardPerStep = 0.05f;
    public float silenceRewardCap = 0.3f;
    public float wastedActionPenalty = -0.2f;

    [Header("페이싱 설정")]
    public int minSilenceStepsForReward = 3;
    public int maxSilenceStepsBeforePenalty = 20;

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
    // Observation: 총 21개
    // Behavior Parameters > Vector Observation Space Size = 17 로 맞출 것
    // ────────────────────────────────────────────
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(mousePanicValue);   // 1
        sensor.AddObservation(micVolumeValue);    // 2
        sensor.AddObservation(lastFearSignal);    // 3

        // 직전 행동 원-핫 인코딩
        for (int i = 0; i < ACTION_COUNT; i++)
            sensor.AddObservation(lastAction == i ? 1f : 0f); // 4~12

        // 연속 침묵 길이 (정규화)
        sensor.AddObservation(
            (float)consecutiveSilence / maxSilenceStepsBeforePenalty); // 13

        // 각 연출 사용 빈도
        int total = Mathf.Max(1, GetTotalNonIdleActions());
        for (int i = 1; i < ACTION_COUNT; i++)
            sensor.AddObservation((float)actionUsageCount[i] / total); // 14~21

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
            // 실제 씬: 실제 상태 체크
            sensor.AddObservation(ScareManager.Instance != null
                && ScareManager.Instance.HasNearbyMannequin() ? 1f : 0f); // 22
            sensor.AddObservation(ScareManager.Instance != null
                && ScareManager.Instance.HasNearbyLight() ? 1f : 0f);     // 23
            sensor.AddObservation(ScareManager.Instance != null
                && ScareManager.Instance.HasUnlockedDoor() ? 1f : 0f);    // 24
            sensor.AddObservation(ScareManager.Instance != null
                && ScareManager.Instance.CanTriggerGlitch() ? 1f : 0f);   // 25
        }

        // 플레이어 성향 벡터(1부에서 수집, 없으면 중간값 0.5)
        if (PlayerProfiler.Instance != null)
        {
            float[] profile = PlayerProfiler.Instance.GetProfileVector();
            foreach (float v in profile)
                sensor.AddObservation(v); // 26~29
        }
        else
        {
            for (int i = 0; i < 4; i++)
                sensor.AddObservation(0.5f); // 26~29
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

        // ────────────────────────────────
        // 보상 레이어 1: 즉각 반응 보상
        // ────────────────────────────────
        if (action != IDLE_ACTION)
        {
            bool isActingNow = false;

            if (TrainingSignalSimulator.Instance != null)
                isActingNow = ScareManager_Training.Instance != null
                           && ScareManager_Training.Instance.isActing;
            else
                isActingNow = ScareManager.Instance != null
                           && ScareManager.Instance.isActing;

            if (isActingNow)
            {
                // 쿨타임 중 낭비 행동 → 페널티
                AddReward(wastedActionPenalty);
            }
            else
            {
                // 유효한 연출 → Fear Signal에 비례한 보상
                //AddReward(fearSignal * fearRewardScale);
                float fearReward = (fearSignal + 0.3f) * fearRewardScale;
                AddReward(fearReward);
            }
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
        // 보상 레이어 3: 다양성 보상
        // ────────────────────────────────
        if (action != IDLE_ACTION)
        {
            actionUsageCount[action]++;
            UpdateActionHistory(action);

            float diversity = CalculateDiversityScore();
            AddReward(diversity * diversityRewardScale);
        }

        // ────────────────────────────────
        // 보상 레이어 4: 페이싱(침묵) 보상
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
                AddReward(-0.1f);
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

    // ────────────────────────────────────────────
    // 테스트용 Heuristic (키보드 1~4로 공포 연출 수동 테스트)
    // ────────────────────────────────────────────
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var d = actionsOut.DiscreteActions;
        if (Input.GetKey(KeyCode.Alpha1)) d[0] = 1;
        else if (Input.GetKey(KeyCode.Alpha2)) d[0] = 2;
        else if (Input.GetKey(KeyCode.Alpha3)) d[0] = 3;
        else if (Input.GetKey(KeyCode.Alpha4)) d[0] = 4;
        else d[0] = 0;
    }
}