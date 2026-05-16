using UnityEngine;

using System;
using System.Collections.Generic;

[System.Serializable]
public class ChaseRollbackSnapshot
{
    public string sceneId;
    public string nodeId;

    public Dictionary<string, bool> worldStates;
    public Dictionary<string, bool> flags;
    public Dictionary<string, int> vars;
    public List<string> inventory;

    public Dictionary<string, bool> flagStates = new();
    public List<string> inventoryItemIds = new();

    public Dictionary<string, bool> doorLockStates = new();
    public Dictionary<string, bool> itemActiveStates = new();

    public bool hasFlashlight;
    public Vector3 playerPosition;
    public Quaternion playerRotation;
}