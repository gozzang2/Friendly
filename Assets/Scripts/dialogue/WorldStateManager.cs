using System.Collections.Generic;
using UnityEngine;

public class WorldStateManager : MonoBehaviour
{
    public static WorldStateManager Instance { get; private set; }

    private Dictionary<string, bool> worldStates = new();

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

    public void SetWorldObjectState(string id, bool active)
    {
        worldStates[id] = active;
    }

    public bool GetWorldObjectState(string id, bool defaultValue = true)
    {
        if (worldStates.TryGetValue(id, out bool value))
            return value;

        return defaultValue;
    }

    public Dictionary<string, bool> GetAllStates()
    {
        return new Dictionary<string, bool>(worldStates);
    }

    public void RestoreStates(Dictionary<string, bool> states)
    {
        worldStates = new Dictionary<string, bool>(states);
    }
}