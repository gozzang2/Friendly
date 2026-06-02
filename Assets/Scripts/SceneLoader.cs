using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    [Header("이동할 씬 이름")]
    public string sceneName;

    [Header("도착씬의 스폰 지점ID")]
    public string targetSpawnID;

    [Header("Optional transition sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip loadSceneSound;
    [SerializeField] private bool waitForSoundBeforeLoad = true;

    //씬이 넘어가도 지워지지 않는(static) 공용 메모지
    public static string nextSpawnID = "";

    private bool _isLoading;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    public void LoadScene()
    {
        if (_isLoading) return;
        if (string.IsNullOrEmpty(sceneName)) return;

        if (loadSceneSound == null || audioSource == null || !waitForSoundBeforeLoad)
        {
            if (audioSource != null && loadSceneSound != null)
                audioSource.PlayOneShot(loadSceneSound);

            DoSceneLoad();
            return;
        }

        StartCoroutine(LoadSceneAfterSound());
    }

    private IEnumerator LoadSceneAfterSound()
    {
        _isLoading = true;

        audioSource.PlayOneShot(loadSceneSound);
        yield return new WaitForSeconds(loadSceneSound.length);

        DoSceneLoad();
    }

    private void DoSceneLoad()
    {

        nextSpawnID = targetSpawnID;
        SceneManager.LoadScene(sceneName);
    }
}