using UnityEngine;

public class SurgeryRoomTrigger : MonoBehaviour
{
    [SerializeField] private BGMLooper bgmLooper;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            bgmLooper.PlayTrigger();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            bgmLooper.PlayDefault();
    }
}