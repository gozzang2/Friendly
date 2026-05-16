using UnityEngine;

public class NPCGameOverHitbox : MonoBehaviour
{
    private NPCStoryLoader loader;

    public void Init(NPCStoryLoader owner)
    {
        loader = owner;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() != null || other.CompareTag("Player"))
        {
            loader.TriggerGameOver();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<PlayerController>() != null || collision.gameObject.CompareTag("Player"))
        {
            loader.TriggerGameOver();
        }
    }
}