using UnityEngine;

public class PlayerProfiler : MonoBehaviour
{
    public static PlayerProfiler Instance;

    // 성향 벡터 8차원 (0~1 정규화)
    // [0] 평균 마이크 반응
    // [1] 평균 마우스 반응
    // [2~7] action 1~6 평균 fearSignal
    private float[] profileVector = new float[8]
        { 0.5f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f };

    private int sampleCount = 0;
    private float totalMicResponse = 0f;
    private float totalMouseResponse = 0f;

    // 액션별 fearSignal 누적 및 카운트
    private float[] actionTotalFear = new float[7];
    private int[] actionCount = new int[7];

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    // ScareManager가 연출 실행할 때마다 호출
    public void RecordResponse(int action, float micVal, float mouseVal)
    {
        sampleCount++;
        totalMicResponse += micVal;
        totalMouseResponse += mouseVal;

        // HorrorDirector와 동일한 fearSignal 계산
        float simultaneousBonus = micVal * mouseVal;
        float fearSignal = Mathf.Clamp01(
            (micVal * 0.7f)
          + (mouseVal * 0.3f)
          + (simultaneousBonus * 0.3f));

        // 액션별 fearSignal 누적
        if (action >= 1 && action < actionTotalFear.Length)
        {
            actionTotalFear[action] += fearSignal;
            actionCount[action]++;
        }

        UpdateProfileVector();
    }

    private void UpdateProfileVector()
    {
        if (sampleCount == 0) return;

        profileVector[0] = totalMicResponse / sampleCount;
        profileVector[1] = totalMouseResponse / sampleCount;

        // action 1~6 평균 fearSignal 직접 저장
        for (int i = 1; i < 7; i++)
        {
            profileVector[i + 1] = actionCount[i] > 0
                ? actionTotalFear[i] / actionCount[i]
                : 0f;
        }
    }

    public float[] GetProfileVector() => profileVector;

    public void SaveProfile()
    {
        for (int i = 0; i < profileVector.Length; i++)
            PlayerPrefs.SetFloat($"PlayerProfile_{i}", profileVector[i]);
        PlayerPrefs.Save();
        Debug.Log("[PlayerProfiler] 성향 벡터 저장 완료");
    }

    public void LoadProfile()
    {
        for (int i = 0; i < profileVector.Length; i++)
            profileVector[i] = PlayerPrefs.GetFloat($"PlayerProfile_{i}", 0f);
        Debug.Log("[PlayerProfiler] 성향 벡터 로드 완료");
    }

    public void SetProfileVector(float[] vector)
    {
        for (int i = 0; i < Mathf.Min(vector.Length, profileVector.Length); i++)
            profileVector[i] = vector[i];
    }
}