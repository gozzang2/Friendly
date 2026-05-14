using UnityEngine;
using System.Collections;

public class ScareManager_Training : MonoBehaviour
{
    public static ScareManager_Training Instance;

    public bool isActing = false;

    // 실제 게임과 동일한 연출 지속 시간
    private float _mannequinDuration = 1.5f;
    private float _redLightDuration = 1.5f;
    private float _jumpScareDuration = 0.6f;
    private float _soundDuration = 1.0f;
    private float _doorDuration = 1.0f;
    private float _glitchDuration = 0.5f;

    void Awake()
    {
        Instance = this;
    }

    // Action 1: 마네킹
    public void CallMannequin()
        => StartCoroutine(ActRoutine(_mannequinDuration, 0.8f));

    // Action 2: 조명 깜빡임
    public void CallRedLights()
        => StartCoroutine(ActRoutine(_redLightDuration, 0.5f));

    // Action 3: 점프스케어
    public void CallJumpScare()
        => StartCoroutine(ActRoutine(_jumpScareDuration, 1.0f));

    // Action 4: 소리
    public void CallScareSound()
        => StartCoroutine(ActRoutine(_soundDuration, 0.4f));

    // Action 5: 문
    public void CallDoorScare()
        => StartCoroutine(ActRoutine(_doorDuration, 0.3f));

    // Action 6: 그림 글리치
    public void CallPictureGlitch()
        => StartCoroutine(ActRoutine(_glitchDuration, 0.5f));

    IEnumerator ActRoutine(float duration, float responseIntensity)
    {
        if (isActing) yield break;
        isActing = true;

        if (TrainingSignalSimulator.Instance != null)
            TrainingSignalSimulator.Instance.TriggerResponse(responseIntensity);

        yield return new WaitForSeconds(duration);
        isActing = false;
    }
}