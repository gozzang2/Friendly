using UnityEngine;

using System;
using System.Collections.Generic;

public class ItemRegistry : MonoBehaviour
{
    public static ItemRegistry Instance { get; private set; }

    private Dictionary<string, GameObject> items = new();

    private void Awake()
    {
        Instance = this;
    }

    public void Register(string itemId, GameObject obj)
    {
        if (!items.ContainsKey(itemId))
            items.Add(itemId, obj);
    }

    public Dictionary<string, bool> GetItemActiveStates(string sceneId)
    {
        var result = new Dictionary<string, bool>();
        foreach (var kv in items)
        {
            result[kv.Key] = kv.Value.activeSelf;
        }
        return result;
    }

    public void RestoreItemActiveStates(Dictionary<string, bool> states)
    {
        foreach (var kv in states)
        {
            if (items.TryGetValue(kv.Key, out var obj))
                obj.SetActive(kv.Value);
        }
    }
}