using System.Collections.Generic;
using UnityEngine;

public class RollbackManager : MonoBehaviour
{
    public static RollbackManager Instance { get; private set; }

    private ChaseRollbackSnapshot snapshot;
    private dialog story; // 너 프로젝트의 dialog.cs

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // PersistentRoot에 있으니까 씬 바뀔 수 있음 → 항상 찾을 수 있게
        story = FindFirstObjectByType<dialog>(FindObjectsInactive.Include);
    }

    private dialog GetStory()
    {
        if (story == null)
            story = FindFirstObjectByType<dialog>(FindObjectsInactive.Include);

        return story;
    }

    public void SaveChaseSnapshot(string sceneId, string nodeId)
    {
        var s = GetStory();
        if (s == null || s.data == null || s.data.state == null)
        {
            Debug.LogError("[RollbackManager] dialog/story state not found.");
            return;
        }

        snapshot = new ChaseRollbackSnapshot();
        snapshot.sceneId = sceneId;
        snapshot.nodeId = nodeId;
        snapshot.worldStates =    WorldStateManager.Instance.GetAllStates();

        // deep copy
        snapshot.flags = s.data.state.flags != null
            ? new Dictionary<string, bool>(s.data.state.flags)
            : new Dictionary<string, bool>();

        snapshot.vars = s.data.state.vars != null
            ? new Dictionary<string, int>(s.data.state.vars)
            : new Dictionary<string, int>();

        snapshot.inventory = s.data.state.inventory != null
            ? new List<string>(s.data.state.inventory)
            : new List<string>();

        Debug.Log($"[RollbackManager] Snapshot saved ({sceneId}:{nodeId})");
    }

    public void RestoreChaseSnapshot()
    {
        if (snapshot == null)
        {
            Debug.LogWarning("[RollbackManager] No snapshot to restore.");
            return;
        }

        var s = GetStory();
        if (s == null || s.data == null || s.data.state == null)
        {
            Debug.LogError("[RollbackManager] dialog/story state not found.");
            return;
        }

        // restore with deep copy
        s.data.state.flags = new Dictionary<string, bool>(snapshot.flags);
        s.data.state.vars = new Dictionary<string, int>(snapshot.vars);
        s.data.state.inventory = new List<string>(snapshot.inventory);

        WorldStateManager.Instance.RestoreStates(
            snapshot.worldStates
        );

        Debug.Log($"[RollbackManager] Snapshot restored ({snapshot.sceneId}:{snapshot.nodeId})");
    }

    public string GetSavedNodeId()
    {
        return snapshot != null ? snapshot.nodeId : null;
    }

    public string GetSavedSceneId()
    {
        return snapshot != null ? snapshot.sceneId : null;
    }
}