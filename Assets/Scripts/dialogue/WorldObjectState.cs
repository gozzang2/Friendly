using UnityEngine;

public class WorldObjectState : MonoBehaviour
{
    public string worldObjectId;

    private void Start()
    {
        bool active = WorldStateManager.Instance
            .GetWorldObjectState(worldObjectId, true);

        gameObject.SetActive(active);
    }
}