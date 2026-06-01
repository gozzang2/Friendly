using UnityEngine;
using UnityEngine.SceneManagement;

public class ChaseManager : MonoBehaviour
{
    public static ChaseManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void OnPlayerCaught()
    {
        var story = FindFirstObjectByType<dialog>();

        if (story != null)
        {
            story.data.state.flags["npc_chase_started"] = false;
            story.data.state.flags["chase_started"] = false;
            story.data.state.flags["alarm_started"] = false;
        }

        story.data.state.flags["npc_chase_started"] = false;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().name
        );
    }

    private void OnReloadFinished(
        Scene scene,
        LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnReloadFinished;

        dialog story = FindFirstObjectByType<dialog>();

        if (story != null)
        {
            story.StartScene(
                "S07_CCTV_ROOM",
                "S07_N8"
            );
        }
    }
}