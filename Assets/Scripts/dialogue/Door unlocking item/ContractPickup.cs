using UnityEngine;
using System.Collections.Generic;

public class ContractPickup : ItemPickup
{
    [Header("Story")]
    [SerializeField] private string chaseFlag = "npc_chase_2f_started";

    protected override void OnPickupSuccess()
    {
        base.OnPickupSuccess();

        dialog story = FindFirstObjectByType<dialog>();

        if (story != null &&
            story.data != null &&
            story.data.state != null)
        {
            if (story.data.state.flags == null)
                story.data.state.flags =
                    new Dictionary<string, bool>();

            story.data.state.flags[chaseFlag] = true;

            Debug.Log(
                $"[ContractPickup] Flag Set : {chaseFlag}"
            );
        }

        // 추적 시작
        NPCStoryLoader loader =    FindFirstObjectByType<NPCStoryLoader>();

        loader?.Start2FChase();
    }
}