using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class InteractiveRadio : MonoBehaviour
{
    private TunerKnob tunerKnob;
    private AudioSource audioSource;

    public ToggleSwitch toggleSwitch;
    public List<RadioStation> radioStations;
    public AudioClip whiteNoise;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        tunerKnob = GetComponentInChildren<TunerKnob>();
    }

    void Update()
    {
        if (toggleSwitch != null && !toggleSwitch.IsOn())
        {
            if (audioSource.isPlaying) audioSource.Stop();
            return;
        }

        UpdateStation(tunerKnob.GetFrequency());
    }

    void UpdateStation(float freq)
    {
        RadioStation target = null;
        foreach (RadioStation rs in radioStations)
        {
            if (rs.isTuned(freq)) { target = rs; break; }
        }

        if (target == null)
        {
            PlaySound(whiteNoise);
            return;
        }

        if (target.isEventChannel && !target.isPermanentDisabled)
        {
            target.isPermanentDisabled = true; // 한 번만 실행되도록 바로 잠금
        }

        PlaySound(target.clip);
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource.clip == clip && audioSource.isPlaying) return;
        audioSource.Stop();
        audioSource.clip = clip;
        if (clip != null) audioSource.Play();
    }

}