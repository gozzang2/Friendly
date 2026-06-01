using UnityEngine;

public class ItemPickup : MonoBehaviour, IInteractable
{
    [Header("Assign Item Data")]
    public ItemData item;          // ScriptableObject 아이템
    public int amount = 1;         // 아직 수량 구현 안 함

    [Header("Story Link (optional)")]
    [SerializeField] private dialog story;          // 씬에 있는 dialog 오브젝트를 드래그
    [SerializeField] private bool showInspectLine = true;

    [Header("Door Unlock (optional)")]
    [SerializeField] private string unlockIdOnPickup;

    [Header("Audio")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private float pickupSoundVolume = 1f;

    [Header("Pickup Object")]
    [SerializeField] private bool destroyObjectOnPickup = true;

    private bool _picked;

    private void OnValidate()
    {
        if (item != null && !string.IsNullOrEmpty(item.itemId))
        {
            gameObject.name = item.itemId;
        }
    }

    public void Interact()
    {
        if (_picked) return;
        _picked = true;

        if (item == null)
        {
            Debug.LogError("[ItemPickup] ItemData is empty. Need to distribute the item");
            _picked = false;
            return;
        }

        // 1) add item to inventory using ItemData
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.AddItem(item);  // ItemData로 추가

        // 2) Json
        if (story == null) story = FindFirstObjectByType<dialog>();

        if (story != null && !string.IsNullOrEmpty(item.itemId))
        {
            story.PickupItemById_FromWorld(
                item.itemId,
                showInspectLine
            );
        }
        else
        {
            Debug.LogWarning("Cannot Find Json of [ItemPickUp] or itemId is invalid.");
        }

        PlayPickupSound();

        // 3) Door unlock logic (optional)
        if (!string.IsNullOrWhiteSpace(unlockIdOnPickup))
        {
            unlockingItem.Unlock(unlockIdOnPickup);
            DoorFeedbackUI.Instance?.ShowUnlockingFeedback();
        }

        OnPickupSuccess();

        if (destroyObjectOnPickup)
            gameObject.SetActive(false);
    }

    protected virtual void OnPickupSuccess()
    {
    }


    private void PlayPickupSound()
    {
        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupSoundVolume);
    }


}