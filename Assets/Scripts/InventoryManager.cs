using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    [Header("UI")]
    public GameObject inventoryPanel;

    public static InventoryManager Instance;

    public event Action OnInventoryChanged;

    [SerializeField] private List<ItemData> items = new();
    [SerializeField] private ItemData debugItem;

    public bool IsOpen => inventoryPanel != null && inventoryPanel.activeSelf;
    public IReadOnlyList<ItemData> Items => items;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (debugItem != null)
            AddItem(debugItem);
    }

    public void AddItem(ItemData item)
    {
        if (item == null) return;
        items.Add(item);
        OnInventoryChanged?.Invoke();
    }

    public void SetOpen(bool open)
    {
        if (inventoryPanel == null) return;

        inventoryPanel.SetActive(open);

        Debug.Log($"[Inventory] open={open}");

        Time.timeScale = open ? 0f : 1f;
        Cursor.visible = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
    }

    public bool HasItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return false;

        foreach (ItemData item in items)
        {
            if (item == null) continue;

            if (item.itemId == itemId)
                return true;
        }

        return false;
    }
}