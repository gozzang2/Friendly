using UnityEngine;

public class CarDoorIntro : MonoBehaviour
{
    [SerializeField] private AudioClip carDoorClip;
    private static bool hasPlayed = false;

    private void Start()
    {
        if (hasPlayed) return;

        hasPlayed = true;
        var source = gameObject.AddComponent<AudioSource>();
        source.clip = carDoorClip;
        source.Play();
    }
}