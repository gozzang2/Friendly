using UnityEngine;

public class ScareManager_Training : MonoBehaviour
{
    public static ScareManager_Training Instance;

    void Awake() => Instance = this;

    public void CallMannequin() => Trigger(0.7f, 1);
    public void CallRedLights() => Trigger(0.7f, 2);
    public void CallJumpScare() => Trigger(0.7f, 3);
    public void CallScareSound() => Trigger(0.7f, 4);
    public void CallDoorScare() => Trigger(0.7f, 5);
    public void CallPictureGlitch() => Trigger(0.7f, 6);

    private void Trigger(float intensity, int actionIndex)
    {
        if (TrainingSignalSimulator.Instance != null)
            TrainingSignalSimulator.Instance.TriggerResponse(intensity, actionIndex);
    }
}