using System.Collections.Generic;

[System.Serializable]
public class SaveData
{
    public Dictionary<string, bool> flags;
    public Dictionary<string, int> vars;
    public List<string> inventory;

    public Dictionary<string, bool> worldStates;

    public string currentSceneId;
    public string currentNodeId;
}