using UnityEngine;

public class BGMLooper : MonoBehaviour
{
    [SerializeField] private AudioClip defaultBGM;   // 기본 브금
    [SerializeField] private AudioClip triggerBGM;   // 트리거 영역 브금
    [SerializeField] private float crossfadeDuration = 2f;

    private AudioSource _source1;
    private AudioSource _source2;
    private bool _isSource1Active = true;
    private Coroutine _loopCoroutine;
    private AudioClip _currentClip;

    private void Start()
    {
        _source1 = gameObject.AddComponent<AudioSource>();
        _source2 = gameObject.AddComponent<AudioSource>();
        _source1.spatialBlend = 0f;
        _source2.spatialBlend = 0f;

        PlayBGM(defaultBGM);
    }

    public void PlayBGM(AudioClip newClip)
    {
        if (newClip == null) return;
        if (_loopCoroutine != null)
            StopCoroutine(_loopCoroutine);

        _currentClip = newClip;

        AudioSource current = _isSource1Active ? _source1 : _source2;
        AudioSource next = _isSource1Active ? _source2 : _source1;

        next.clip = newClip;
        StartCoroutine(SwitchBGM(current, next));
    }

    public void PlayDefault() => PlayBGM(defaultBGM);
    public void PlayTrigger() => PlayBGM(triggerBGM);

    private System.Collections.IEnumerator SwitchBGM(AudioSource current, AudioSource next)
    {
        next.volume = 0f;
        next.Play();

        float elapsed = 0f;
        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / crossfadeDuration;
            current.volume = 1f - t;
            next.volume = t;
            yield return null;
        }

        current.Stop();
        current.volume = 0f;
        next.volume = 1f;
        _isSource1Active = !_isSource1Active;

        _loopCoroutine = StartCoroutine(CrossfadeLoop());
    }

    private System.Collections.IEnumerator CrossfadeLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(_currentClip.length - crossfadeDuration);

            AudioSource current = _isSource1Active ? _source1 : _source2;
            AudioSource next = _isSource1Active ? _source2 : _source1;

            next.clip = _currentClip;
            next.volume = 0f;
            next.Play();

            float elapsed = 0f;
            while (elapsed < crossfadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / crossfadeDuration;
                current.volume = 1f - t;
                next.volume = t;
                yield return null;
            }

            current.Stop();
            current.volume = 0f;
            next.volume = 1f;
            _isSource1Active = !_isSource1Active;
        }
    }
}